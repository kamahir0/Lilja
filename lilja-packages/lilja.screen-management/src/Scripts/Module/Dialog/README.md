# Dialog Module

汎用的なダイアログ機能を提供するモジュールです。
`PrefabOverlayBase` を拡張し、Frame（枠）とContent（中身）の分離、アニメーション、Fluent APIによる動的生成などをサポートしています。

## 1. 特徴 (Features)

*   **Fluent API:** クラスを定義せず、メソッドチェーンでダイアログを構築可能 (`VariableDialog`)。
*   **Fallback UI:** Prefabが見つからない場合、コードベースで簡易UIを自動生成して表示。プロトタイピングに最適。
*   **Frame & Content 分離:** 共通の「枠」と個別の「中身」を組み合わせてダイアログを構成。
*   **Async/Await:** `CallOverlayAsync` で呼び出し、結果を非同期で待機。

---

## 2. 使い方 (Usage)

### 2.1. VariableDialog (Fluent API)

クラスを継承することなく、その場でダイアログを生成して呼び出せます。

```csharp
using Kamahir0.ScreenManagement.Dialog;

// ...

var result = await CallOverlayAsync<bool>(
    VariableDialog<bool>.Create("確認", enableOutsideButton: true, outsideButtonResult: false)
        .AddText("本当に購入しますか？")
        .AddButton("はい", true)
        .AddButton("いいえ", false)
);

if (result)
{
    // 購入処理
}
```

### 2.2. SimpleDialogBase (簡易継承)

`SimpleDialogBase<TResult>` を継承すると、標準の Frame (`SimpleDialogFrame`) と Content (`SimpleDialogContent`) を使用したダイアログを簡単に定義できます。

```csharp
public class ErrorDialog : SimpleDialogBase<Unit>
{
    private readonly string _message;

    public ErrorDialog(string message)
    {
        _message = message;
    }

    protected override void Build()
    {
        Frame.SetTitle("エラー");
        Content.AddText(_message);
        Frame.AddButton("閉じる", Unit.Default);
    }
}
```

### 2.3. DialogBase (フルカスタマイズ)

Frame や Content の Prefab を完全にカスタムしたい場合は `DialogBase` を継承します。

```csharp
// 独自のFrameとContentを指定
public class ShopDialog : DialogBase<ShopArgs, int, ShopFrame, ShopContent>
{
    // FrameKey => "DialogFrame/ShopFrame"
    // ContentKey => "DialogContent/ShopContent"
    // Resources/DialogFrame/ShopFrame.prefab 等が必要
    
    protected override void OnViewLoaded()
    {
        base.OnViewLoaded();
        // Frame や Content へのアクセスとイベント登録
        Content.SetItems(...);
    }
}
```

---

## 3. Fallback System (Prototyping)

本モジュールには強力な**フォールバック機能**が搭載されています。
`Resources` フォルダに Prefab が存在しない場合、システムが自動的にプロシージャルなUI（白い矩形や標準ボタン）を生成して表示します。

これにより、**UnityエディタでPrefabを作る前に、コードだけでロジックと遷移のテストを完了させる**ことができます。
デザインは後からPrefabを配置するだけで自動的に差し替わります。

*   **Frameが見つからない:** `DefaultSimpleFrame` (タイトルバー付きウィンドウ) を生成
*   **Contentが見つからない:** `DefaultSimpleContent` (テキストと画像プレースホルダー) を生成

---

## 4. アーキテクチャ (Architecture)

### クラス階層
```text
ScreenBase
 └── OverlayBase
      └── PrefabOverlayBase
           └── DialogBase<TArgs, TResult, TFrame, TContent>
                └── SimpleDialogBase<TResult>
                     └── VariableDialog<TResult>
```

### Viewの構造
ダイアログのViewは以下の階層で生成されます。

*   **DialogRoot:** 画面全体を覆うCanvas
    *   **Backdrop:** 背景を暗くする半透明レイヤー
    *   **Outside:** 枠外クリック判定用の不可視ボタン
    *   **Frame:** ダイアログのウィンドウ枠 (Title, ButtonContainer等)
        *   **Content:** ダイアログの中身 (Message, Image等)

### アニメーション
`EnterAsync`, `ExitAsync` に加えて、スタック変更時の `PushAsync` (奥に引っ込む), `PopAsync` (手前に戻る) アニメーションをサポートしています。
