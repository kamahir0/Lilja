# ScreenManagement ドキュメント

## 1. 設計思想と哲学 (Philosophy)

ScreenManagementは、Unityアプリケーションにおける画面遷移とゲーム進行ロジックを、**「非同期関数による手続き型記述」**として再定義するフレームワークです。

従来の「Updateループ + フラグ管理」や「巨大なステートマシン（State Pattern）」が抱える以下の本質的な課題を解決するために設計されました。

### 1.1. 変数スコープの局所化（Localization of Scope）

従来のステートパターンでは、状態を維持するためにクラスのフィールド（メンバ変数）を多用する必要がありました。画面遷移が複雑になるにつれ、フィールドが増大し、どのメソッドがどの変数を変更したか追跡不能になる「変数の寿命管理」の問題が発生します。

ScreenManagementでは、C#の `async/await` を活用することで、**変数の寿命をメソッド（ブロック）のスコープ内に閉じ込めます**。

```csharp
// ScreenManagementのスタイル
var result = await overlay.CallAsync(args, cancellationToken);
if (result) { ... }
// result変数はここで寿命を終える。他のメソッドからは見えないため安全。
```

これにより、副作用が予期せぬ場所に波及することを構造的に防ぎ、堅牢なロジック記述を可能にします。

### 1.2. 魂（Controller）と肉体（View）の分離

ScreenManagementは、画面を構成する要素を以下の2つに明確に分離します。

* **魂 (Controller / Logic):** C#クラス。スタック上で生存し、文脈とデータを保持する。
* **肉体 (View / GameObject):** Unityコンポーネント。描画と演出のみを担当する使い捨ての「端末」。

この分離により、**「ロジック（魂）をメモリに残したまま、重いアセット（肉体）だけを破棄してメモリを空ける」** といった高度なメモリ管理を自動化しています。

### 1.3. 画面遷移は「イベント」ではなく「関数呼び出し」

画面遷移を `Show()` してイベント購読で結果を待つのではなく、サブルーチン（関数）として呼び出します。
`var result = await overlay.CallAsync(args, cancellationToken);` のように記述できるため、コールバック地獄から解放され、コードの記述順と実行順が一致します。

---

## 2. クイックスタート (Minimal Usage)

最もシンプルな構成で ScreenManagement を使用する手順です。

### ステップ 1: World（土台となる画面）の作成

`WorldBase<TArgs>` を継承し、ルートとなる画面を作ります。

```csharp
using Kamahir0.ScreenManagement;
using Cysharp.Threading.Tasks;
using System.Threading;

// 引数なしの場合は ValueTuple を指定
public class TitleWorld : WorldBase<ValueTuple> 
{
    // InitializeAsync や EnterAsync をオーバーライドして処理を記述可能
    protected override UniTask InitializeAsync(ValueTuple args, CancellationToken cancellationToken)
    {
        // 初期化処理
        return UniTask.CompletedTask;
    }
}
```

### ステップ 2: システムの初期化

アプリケーションのエントリーポイント（`Boot.cs` など）で `ScreenManager.Initialize` を呼び出します。

```csharp
using System;
using UnityEngine;
using Kamahir0.ScreenManagement;

public class GameBoot : MonoBehaviour
{
    void Start()
    {
        // Worldを登録して起動
        ScreenManager.Initialize(builder => 
        {
            builder.Register(() => new TitleWorld());
            builder.Register(() => new ExploreWorld());
        }, 
        typeof(TitleWorld),  // 最初のWorld
        new ValueTuple(),    // 引数
        myTransition);       // トランジション
    }
}
```

これだけで、`TitleWorld` に対応するシーン（クラス名から "World" を除いた "Title" シーン）がロードされ、表示されます。

---

## 3. 実践的な使い方 (Practical Usage)

### 3.1. Overlay の作成と呼び出し

軽量なウィンドウ（ポップアップ等）には `PrefabOverlayBase<TArgs, TResult>` を使用します。

**定義:**
`Resources/Overlay/NoticeOverlay.prefab` を用意し、以下のようにクラスを定義します。

```csharp
using Kamahir0.ScreenManagement;
using Cysharp.Threading.Tasks;
using System.Threading;

// TArgs: 引数の型, TResult: 戻り値の型
public class NoticeOverlay : PrefabOverlayBase<string, Unit>
{
    // [UnityView] 属性でPrefab内のコンポーネントを自動注入
    [UnityView] private UnityEngine.UI.Button _closeButton;
    [UnityView] private UnityEngine.UI.Text _messageText;

    private string _message;

    // InitializeAsync で引数を受け取る
    public override UniTask InitializeAsync(string message, CancellationToken cancellationToken)
    {
        _message = message;
        return UniTask.CompletedTask;
    }

    protected override void OnViewInstanced()
    {
        _messageText.text = _message;
        _closeButton.onClick.AddListener(() => Close(Unit.Default)); // 閉じる
    }
}
```

**呼び出し (World内など):**

```csharp
// Overlayをインスタンス化して呼び出し
var overlay = new NoticeOverlay();
await overlay.CallAsync("保存しました", DisposeCancellationToken);
```

> **Note:** 汎用的なダイアログ機能（Yes/No確認、Fluent API、Fallback機能等）については、[Dialog Module Documents](Scripts/Module/Dialog/README.md) を参照してください。

### 3.2. SceneOverlay（重量級オーバーレイ）

3Dバトルやミニゲームなど、別シーンをロードする必要がある場合は `SceneOverlayBase<TArgs, TResult>` を使用します。

```csharp
public class BattleSceneOverlay : SceneOverlayBase<BattleArgs, BattleResult>
{
    // SceneOverlayBase の IsHeavy はデフォルトで true
    // 表示前に背後のViewが自動で破棄され、終了後に復元されます

    public override UniTask InitializeAsync(BattleArgs args, CancellationToken cancellationToken)
    {
        // バトル準備
        return UniTask.CompletedTask;
    }
}
```

---

## 4. アーキテクチャ (Architecture)

### 4.1. ディレクトリ構成

```
Scripts/
├── Api/                    # 公開API（ScreenManager, World, Transition）
├── Domain/
│   ├── Enums/              # EnterType, ExitType
│   ├── Interfaces/         # IScreen, IWorld, IOverlay, IViewHandle, etc.
│   ├── Repository/         # Repository, ScreenStack
│   ├── Services/           # OverlayService, WorldService
│   └── Utility/
├── Implementation/
│   ├── Attributes/         # UnityViewAttribute
│   ├── Helper/
│   ├── Infrastructure/     # ViewHandle実装群
│   ├── Screens/            # ScreenBase, WorldBase, OverlayBase, etc.
│   └── Utility/
└── Module/
    ├── Addressables/       # Addressablesサポート（オプション）
    └── Dialog/             # ダイアログモジュール
```

### 4.2. 継承ツリー

```text
IScreen (インターフェース)
├── IWorld
│   └── WorldBase<TArgs> (場所: スタック不可・ルートのみ)
└── IOverlay
    └── OverlayBase<TArgs, TResult> (文脈: スタック可能)
         ├── PrefabOverlayBase<TArgs, TResult> (軽量: Prefab生成)
         └── SceneOverlayBase<TArgs, TResult>  (重量: Scene加算ロード)
```

### 4.3. World と Overlay の関係

* **World:** システムに一つだけ存在する「場所」。スタックに積むことはできません（型システムによる制約）。`World.Switch` でのみ切り替わります。
* **Overlay:** 現在のWorldの上に積み上がる「文脈」。`overlay.CallAsync()` でスタックされ、結果を返すとスタックから除去されます。

---

## 5. ライフサイクル (Lifecycle)

### 5.1. メソッド実行順序

```
InitializeAsync → LoadViewAsync → [OnViewLoaded] → OpenAsync
    ↓ (処理中: WaitForResultAsync)
CloseAsync → [OnViewUnloaded] → UnloadView → Dispose
```

| メソッド                   | 説明                                                                 |
| -------------------------- | -------------------------------------------------------------------- |
| `InitializeAsync`          | データロード、API通信などの初期化処理                                |
| `LoadViewAsync`            | View（GameObject/Scene）をロード                                     |
| `OnViewLoaded`             | Viewロード完了後のセットアップ（ボタン登録など）                     |
| `OpenAsync` / `CloseAsync` | 入場/退場アニメーション（`EnterAsync` / `ExitAsync` を内部呼び出し） |
| `OnViewUnloaded`           | View破棄直前のクリーンアップ                                         |
| `Dispose`                  | リソース解放                                                         |

### 5.2. EnterType / ExitType

`EnterAsync` と `ExitAsync` は、呼び出し元に応じて異なるタイプが渡されます。

| EnterType  | 説明                                |
| ---------- | ----------------------------------- |
| `OnOpen`   | 画面を新規オープン時                |
| `OnResume` | スタック上位のOverlayが閉じて復帰時 |

| ExitType  | 説明                                    |
| --------- | --------------------------------------- |
| `OnClose` | 画面を完全に閉じる時                    |
| `OnPause` | 新しいOverlayが上に乗ってきて一時停止時 |

### 5.3. ライフサイクルメソッド内の制限

ScreenManagementでは、スタックの整合性を保つため、以下のライフサイクルメソッド内での別Overlayの呼び出しを **禁止** しています。

*   `InitializeAsync`
*   `OpenAsync` / `CloseAsync` (内部の `EnterAsync` / `ExitAsync`)
*   `OnViewLoaded`
*   `OnViewUnloaded`

もしこれらのメソッド実行中に `CallAsync` を呼び出すと、**`InvalidOperationException`** がスローされます。

**推奨実装:**
画面表示直後に別の画面を出したい場合は、ライフサイクルメソッドではなく、ボタンクリックなどの **イベント駆動** で実装してください。

---

## 6. パフォーマンスとメモリ管理 (Performance & Memory)

ScreenManagementは、Overlayの種類に応じてメモリ管理戦略を自動で切り替えます。

### 6.1. SceneOverlay による「サスペンド（Suspend）」

重量級の画面（3Dバトルやミニゲームなど）を `SceneOverlayBase` で実装して呼び出すと、以下のメモリ最適化が自動的に働きます。

1. **呼び出し時:** 新しいシーンをロードする直前に、**背後にある全てのView（GameObject）を破棄・アンロード**します。
   * *Controller（C#インスタンス）はスタックに残ったままです（魂は維持）。*
2. **実行中:** メモリは新しいシーンのためだけに使用されます。
3. **終了時:** 重いシーンを破棄した後、**背後にあったViewを自動的に再生成・再初期化**します。

これにより、モバイル端末などメモリ制約の厳しい環境でも、巨大な画面遷移を安全に行えます。

### 6.2. PrefabOverlay の軽量スタック

`PrefabOverlayBase` を呼び出す場合は、背後のViewは維持されます。頻繁なメニュー開閉などで負荷をかけません。

### 6.3. World遷移時の完全破棄

`World.Switch` を実行すると、スタックされている全てのOverlayが破棄され、メモリがクリーンな状態になってから次のWorldへ遷移します。これにより「消し忘れ」によるメモリリークを防ぎます。

---

## 7. API リファレンス (API Reference)

### 7.1. ScreenManager (Static Entry Point)

システム全体の初期化を行います。

```csharp
// 同期版初期化（fire-and-forget）
ScreenManager.Initialize(
    Action<IWorldBuilder> configuration,
    Type worldType,
    object args,
    ITransition transition
);

// デバッグ用 async版初期化
await ScreenManager.Debug.InitializeAsync(
    configuration, worldType, args, transition, cancellationToken
);
```

### 7.2. IWorldBuilder

Worldのファクトリを登録するインターフェースです。

```csharp
public interface IWorldBuilder
{
    // ジェネリック版
    void Register<TWorld>(Func<TWorld> factory) where TWorld : IWorld;

    // 非ジェネリック版
    void Register(Type worldType, Func<IWorld> factory);
}
```

### 7.3. World (Static Navigation API)

World間の遷移を制御します。

```csharp
// 同期版（fire-and-forget）
World.Switch(Type worldType, object args);

// デバッグ用 async版
await World.Debug.SwitchAsync(worldType, args, cancellationToken);
```

### 7.4. ScreenBase (Abstract)

Viewを持つ画面の共通基底クラスです。

| メンバ                                                               | 説明                                   |
| -------------------------------------------------------------------- | -------------------------------------- |
| `protected virtual UniTask EnterAsync(EnterType, CancellationToken)` | 入場演出                               |
| `protected virtual UniTask ExitAsync(ExitType, CancellationToken)`   | 退場演出                               |
| `protected virtual void OnViewLoaded()`                              | View生成後の初期化                     |
| `protected virtual void OnViewUnloaded()`                            | View破棄前のクリーンアップ             |
| `protected virtual void Dispose()`                                   | 破棄時処理                             |
| `protected abstract IViewHandle ViewHandle`                          | View管理ハンドル                       |
| `protected CancellationToken DisposeCancellationToken`               | 画面寿命に紐づくCancellationToken      |
| `public int LayerIndex`                                              | Canvas sortOrder用レイヤーインデックス |

### 7.5. WorldBase\<TArgs\> (Abstract)

「場所」を表すクラスです。

```csharp
public abstract class WorldBase<TArgs> : ScreenBase, IWorld
{
    // 引数を受け取る初期化（オーバーライド推奨）
    protected virtual UniTask InitializeAsync(TArgs args, CancellationToken cancellationToken);

    // LayerIndex は常に 0
    public override int LayerIndex => 0;
}
```

シーン名は、クラス名から "World" を除いた名前が自動的に使用されます。（例: `TitleWorld` → `Title` シーン）

### 7.6. OverlayBase\<TArgs, TResult\> (Abstract)

「文脈」を表すクラスです。

| メンバ                                                             | 説明                                       |
| ------------------------------------------------------------------ | ------------------------------------------ |
| `public UniTask<TResult> CallAsync(TArgs, CancellationToken)`      | Overlayを呼び出して結果を待つ              |
| `public virtual UniTask InitializeAsync(TArgs, CancellationToken)` | 引数を受け取る初期化                       |
| `protected void Close(TResult result)`                             | 結果を返してOverlayを閉じる                |
| `protected void Throw(Exception exception)`                        | 呼び出し元に例外を送出                     |
| `public virtual bool IsHeavy`                                      | 重いOverlayかどうか（デフォルト: `false`） |

### 7.7. PrefabOverlayBase\<TArgs, TResult\>

`OverlayBase` を継承し、Prefabをロードする軽量Overlayです。

```csharp
// プレハブキーは自動生成: "Overlay/{クラス名 - 'Overlay'}"
// 例: NoticeOverlay → "Overlay/Notice"

// ビューの事前ロード
await overlay.PreloadViewAsync(cancellationToken);
```

### 7.8. SceneOverlayBase\<TArgs, TResult\>

`OverlayBase` を継承し、シーンをロードする重量Overlayです。

```csharp
// IsHeavy はデフォルトで true
// シーン名は自動生成: "{クラス名 - 'Overlay'}"
// 例: BattleOverlay → "Battle" シーン
```

### 7.9. IViewHandle

ビューの読み込み/解放を抽象化するインターフェースです。

```csharp
public interface IViewHandle
{
    GameObject[] RootObjects { get; }
    UniTask LoadAsync(CancellationToken cancellationToken);
    void Unload();
}
```

### 7.10. Attributes

```csharp
// View生成時に GetComponentInChildren を使用して自動注入
[UnityView]
private Button _closeButton;
```

---

## 8. Addressablesサポート

ScreenManagementは `Addressables` パッケージをオプショナルにサポートしています。

### 8.1. 動作原理

* Addressablesパッケージがプロジェクトにインストールされていない場合：
  * `Module/Addressables` 内のコードはコンパイルされません
  * デフォルトで `ResourcesPrefabProvider`（Resources API）が使用されます

* Addressablesパッケージがインストールされている場合：
  * `AddressablesPrefabProvider` が利用可能になります
  * 手動で切り替えることで Addressables 経由でプレハブをロードできます

### 8.2. カスタムプロバイダー

`IPrefabHandle` を実装することで、独自のロード方式を使用することも可能です。

```csharp
// Repository に独自のプレハブハンドルファクトリを設定
Repository.Instance.PrefabHandleFactory = prefabKey => new MyCustomPrefabHandle(prefabKey);
```

---

## 9. 実践例: RPGコマンドバトル (Example)

ScreenManagementの真価は、複雑なステート管理が必要な場面で発揮されます。

### 9.1. 設計のポイント

* **Whileループによる待機:** プレイヤーがコマンドを決定するまで無限ループで入力を待ち受けます。
* **null によるキャンセル表現:** Overlayが `null` を返した場合を「戻るボタンが押された」とみなし、`continue` でループの先頭に戻ります。
* **サブルーチン化:** 複雑な処理は別のメソッドに切り出し、階層化します。

### 9.2. 実装例

```csharp
public class BattleCommandHandler
{
    private readonly CancellationToken _cancellationToken;

    public async UniTask<BattleResult> SelectCommandAsync()
    {
        // 【第1階層】カテゴリ選択
        while (true)
        {
            var menuOverlay = new CommandMenuOverlay();
            var category = await menuOverlay.CallAsync(Unit.Default, _cancellationToken);

            // キャンセル → 終了
            if (category == null) return null;

            // パターンA: 「たたかう」
            if (category == CommandCategory.Attack)
            {
                var targetOverlay = new TargetSelectOverlay();
                var target = await targetOverlay.CallAsync(Unit.Default, _cancellationToken);

                // ターゲット選択でキャンセル → カテゴリ選択に戻る
                if (target == null) continue;

                return new BattleResult(CommandCategory.Attack, target);
            }
            // パターンB: 「スキル」
            else if (category == CommandCategory.Skill)
            {
                var skillResult = await SelectSkillSequenceAsync();
                if (skillResult != null) return skillResult;
                // nullなら第1階層ループを継続 → カテゴリ選択に戻る
            }
        }
    }

    // 【第2階層】スキル選択のサブルーチン
    private async UniTask<BattleResult> SelectSkillSequenceAsync()
    {
        while (true)
        {
            var skillOverlay = new SkillListOverlay();
            var skillId = await skillOverlay.CallAsync(Unit.Default, _cancellationToken);

            // キャンセル → 呼び出し元（カテゴリ選択）へ
            if (skillId == null) return null;

            var targetOverlay = new TargetSelectOverlay();
            var target = await targetOverlay.CallAsync(skillId.Value, _cancellationToken);

            // ターゲット選択でキャンセル → スキルリストに戻る
            if (target == null) continue;

            return new BattleResult(CommandCategory.Skill, target, skillId.Value);
        }
    }
}
```

### 9.3. この実装のメリット

1. **ステート変数がゼロ:** `CurrentState` のような変数は一切存在しません。現在の実行行そのものが状態を表します。
2. **文脈の可視化:** コードのネスト構造が、そのまま画面の階層構造と一致するため、ロジックの流れが一目で理解できます。
3. **柔軟な戻り挙動:** `continue` と `return null` の使い分けで、戻り先を直感的に制御できます。