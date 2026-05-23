# Lilja.ScreenManagement

`Lilja.ScreenManagement` は、UniTask をベースにした Unity 用の階層型画面遷移（スクリーンマネジメント）システムです。メモリ最適化、重なり順（Canvas sortingOrder）の制御、UI コンポーネントの自動依存注入（DI）など、Unity での UI 開発における複雑な課題を強力に解決します。

---

## 主な特徴

*   **階層木構造（ツリー構造）による遷移管理**: 画面の重ね合わせや親子関係を双方向リンクで厳格に管理します。親画面が閉じられると、その子孫画面も安全に再帰破棄されます。
*   **多彩な画面タイプ**:
    *   `GameScreen<TArgs>`: グループ内で排他管理される標準的な画面。
    *   `AwaitableGameScreen<TArgs, TResult>`: モーダルダイアログのように呼び出し、ユーザーの操作結果を非同期で待機・回収できる画面。
*   **ビュー実装の抽象化 (`IViewHandle`)**: 画面の表示実体をプレハブ（`PrefabViewHandle`）またはシーン加算ロード（`SceneViewHandle`）から柔軟に選択・混在可能です。
*   **Ancestor Unloading（メモリ最適化）**: 全画面を覆う画面をロードする際、背後にある先祖（親）画面のビュー（GameObject）を自動的にアンロードしてメモリを解放します。上の画面が閉じられた際には、並列ロードを用いて超高速で先祖ビューを自動復元します。
*   **Canvas の自動ソート ＆ 入力遮断**: 画面の階層（Layer）に応じて `sortingOrder` を自動的に補正し、重なり順のバグを防ぎます。また、前面に重ねられた画面の背後へのクリック入力を遮断するブロッカーを自動生成します。
*   **`[View]` 属性による自動依存注入とメモリリーク防止**: 画面クラスのフィールドに `[View]` 属性を付与するだけで、ビュー内の UI コンポーネントを自動で探索・バインドします。画面のアンロード時には自動で参照が `null` クリアされ、メモリリークを防止します。

---

## 導入方法

### 動作環境
*   Unity 6000.3 以上
*   [UniTask](https://github.com/Cysharp/UniTask) がプロジェクトに導入されていること

---

## 使い方

### 1. 画面クラスとビューの定義

プレハブからロードする通常の画面を作成するには、`GameScreen<TArgs>` を継承し、`ViewHandle` プロパティで `PrefabViewHandle` を指定します。

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using Lilja.ScreenManagement;
using UnityEngine.UI;

namespace MyGame
{
    // 画面に渡す引数の型（不要な場合は ValueTuple などを指定）
    public record MyScreenArgs(string TitleMessage);

    public class MyAwesomeScreen : GameScreen<MyScreenArgs>
    {
        // クラス名に基づき "Screens/MyAwesome" プレハブが自動ロードされます
        protected internal override IViewHandle ViewHandle { get; } = PrefabViewHandle.Default;

        // [View] 属性を付けると、プレハブ内の同名または同型のコンポーネントが自動でバインドされます
        [View] private Text _titleText;
        [View] private Button _closeButton;

        protected override UniTask InitializeAsync(MyScreenArgs args, CancellationToken cancellationToken)
        {
            // 引数を用いた初期化
            if (_titleText != null)
            {
                _titleText.text = args.TitleMessage;
            }

            _closeButton.onClick.AddListener(() =>
            {
                // 画面を閉じる、またはグループを終了する
                Group.Complete();
            });

            return UniTask.CompletedTask;
        }

        protected override void DisposeCore()
        {
            // イベントリスナーの解除などのクリーンアップ
            _closeButton.onClick.RemoveAllListeners();
        }
    }
}
```

> 💡 **自動キー解決**: `PrefabViewHandle.Default` や `SceneViewHandle.Default` を使用すると、画面のクラス名から末尾の `Screen` を除いた名前（例: `MyAwesomeScreen` -> `Screens/MyAwesome`）で自動的にアセットキーが解決されます。

---

### 2. 画面グループ（GameScreenGroup）による画面遷移

複数の画面を切り替えるスタック（ステートマシン）を定義するには、`GameScreenGroup` を継承して画面をレジストリに登録します。

```csharp
using Lilja.ScreenManagement;

namespace MyGame
{
    public class LobbyScreenGroup : GameScreenGroup
    {
        protected override void Configure(IGameScreenRegistry registry)
        {
            // グループ内で使用する画面を登録
            registry.Register<MyAwesomeScreen, MyScreenArgs>();
            registry.Register<OtherScreen, OtherScreenArgs>();
        }
    }
}
```

#### グループの起動と画面切り替え

画面グループは、画面コンテキストの下で起動（`CallAsync`）し、グループが終了するまで非同期で待機できます。

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using Lilja.ScreenManagement;

namespace MyGame
{
    public class GameSequence
    {
        public async UniTask StartLobbyAsync(GameScreenContext rootContext, CancellationToken cancellationToken)
        {
            var lobbyGroup = new LobbyScreenGroup();

            // 初期画面を指定してグループを起動し、完了するまで非同期で待機
            await lobbyGroup.CallAsync<MyAwesomeScreen, MyScreenArgs>(
                rootContext,
                new MyScreenArgs("ようこそ！ロビーへ"),
                cancellationToken
            );
            
            // グループ内の画面から Group.Complete() が呼ばれると、ここに到達します
        }
    }
}
```

グループ起動後は、各画面から `Group.SwitchAsync` を呼ぶことで、現在のアクティブ画面を破棄して別の画面へ切り替えることができます。

```csharp
// MyAwesomeScreen 内から別画面へ排他切り替え
await Group.SwitchAsync<OtherScreen, OtherScreenArgs>(new OtherScreenArgs());
```

---

### 3. 結果を待機できる画面（AwaitableGameScreen）

確認ダイアログのように、「表示して、結果が確定するまで呼び出し元を待機させたい」画面は `AwaitableGameScreen<TArgs, TResult>` を使用します。

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using Lilja.ScreenManagement;
using UnityEngine.UI;

namespace MyGame
{
    public class ConfirmDialogScreen : AwaitableGameScreen<string, bool>
    {
        protected internal override IViewHandle ViewHandle { get; } = PrefabViewHandle.Default;

        [View] private Text _messageText;
        [View] private Button _yesButton;
        [View] private Button _noButton;

        protected override UniTask InitializeAsync(string message, CancellationToken cancellationToken)
        {
            _messageText.text = message;

            _yesButton.onClick.AddListener(() => Complete(true));  // 結果 true を返して閉じる
            _noButton.onClick.AddListener(() => Complete(false)); // 結果 false を返して閉じる

            return UniTask.CompletedTask;
        }
    }
}
```

#### 呼び出し側での待機方法

呼び出し元の画面の `Context` を渡して `CallAsync` を実行するだけで、表示から結果の回収、画面の自動破棄までが一行で行われます。

```csharp
// 呼び出し元の画面クラス内から
var isYes = await new ConfirmDialogScreen().CallAsync(Context, "本当にゲームを終了しますか？", cancellationToken);

if (isYes)
{
    // 終了処理
}
```

---

## 詳細仕様と最適化

### メモリ最適化: Ancestor Unloading
シーン単位（`SceneViewHandle`）で画面を構築する場合など、画面が背後にある親画面群を完全に覆い隠すときは、ビューハンドルのコンストラクタで `unloadsAncestors = true` を指定します。

```csharp
protected internal override IViewHandle ViewHandle { get; } = new SceneViewHandle("LobbyScene", unloadsAncestors: true);
```
これにより、この画面のロード時に親画面のビュー（GameObject）が一旦破棄され、メモリが解放されます。上の画面が閉じられた際には、自動的に先祖のビューが並列ロードされ、元の状態で復帰します。

### 自動 Canvas ソート ＆ レイキャストブロック
*   **ソート補正**: 画面が重ねられるたびに内部レイヤー値がインクリメントされ、配下にあるすべての Canvas の `sortingOrder` に `1000 * レイヤー` のオフセットが自動適用されます。
*   **入力遮断**: レイヤーが1以上の画面（重ね合わせ画面）がロードされると、その最背面に描画負荷ゼロ（メッシュ生成なし）の `InvisibleGraphic` を持つ `RaycastBlocker` GameObject を自動生成し、背後の別画面への入力を遮断します。

### メモリリーク防止 (Nullify)
`[View]` 属性によって自動注入されたフィールドの参照は、画面のアンロード処理（`Teardown`）の際に自動的に `null` クリアされます。これにより、C# 側のインスタンスが Unity の破棄されたアセットへの参照を保持し続け、メモリリークが発生するのを未然に防ぎます。

---

## ライセンス

[MIT License](LICENSE)
