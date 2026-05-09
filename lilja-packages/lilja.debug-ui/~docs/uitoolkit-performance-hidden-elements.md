# UIToolkit: 非表示要素のパフォーマンス最適化

## 背景と問題

初回ページ遷移時（特に要素数の多いページ）にフレームスパイクが発生していた。
調査の結果、「非表示管理に `display: none` を使っていること」と「ページの DOM アタッチが初回ナビゲーションまで遅延していること」が原因と判明した。

---

## display: none が引き起こす問題

UIToolkit は `display: none` の要素とその子孫を**レイアウト計算・スタイル解決・フォントアトラス生成の対象から除外**する。

このため：
- `DebugMenuWindow` が `display: none` で隠されている間は、配下の全ページが UIToolkit に処理されない
- `display: flex` に切り替えたフレームで、配下の全要素のレイアウト・スタイル・メッシュ生成が一括で走る → スパイク

---

## 非表示要素を扱う各手法の比較

Unity 公式ドキュメント "Best practices for managing elements" に記載の各手法の特性：

| 手法                           | レイアウト計算 | フォントアトラス処理 | ポインターブロック                                   | GPU コスト     |
| ------------------------------ | -------------- | -------------------- | ---------------------------------------------------- | -------------- |
| `display: none`                | **スキップ**   | **スキップ**         | しない                                               | なし           |
| `visibility: hidden`           | される         | される               | **子要素まで有効**（回避不可）                       | 低             |
| `opacity: 0`                   | される         | される               | `pickingMode: Ignore` で回避可だが**子に伝播しない** | 頂点シェーダー |
| **Translate outside Viewport** | **される**     | **される**           | **物理的に画面外のため発生しない**                   | 頂点シェーダー |

### pickingMode: Ignore の伝播問題

`pickingMode = PickingMode.Ignore` は**その要素自身を hit-test から除外するだけ**であり、子要素には伝播しない。
UIToolkit の Pick 処理は `Ignore` な親の中も子要素を探索し続けるため、
子要素に `pickingMode = Position` が残っていると pointer event を受け取ってしまう。

```
panel.Pick(position)
  └── Root (Ignore) → 自分はスキップ、でも子は走査する
        └── ChildButton (Position) → ヒット！
```

このため `opacity: 0` + `pickingMode: Ignore` をウィンドウに設定しても、
配下のボタン類がクリックを受け取りアンダーレイのUIを操作できなくなる問題は解決しない。

---

## 採用した解決策: Translate outside of the Viewport

```csharp
// 非表示時
style.translate = new StyleTranslate(new Translate(-5000, -5000));
style.opacity = 0f;

// 表示時（translate をリセット）
style.translate = StyleKeyword.None;
```

`translate` を使った画面外移動は：
- レイアウト・スタイル・フォントアトラス処理が**起動時に実行される**（display: none と異なり子要素をスキップしない）
- 要素が物理的に画面外なので pointer event が**自然に当たらない**
- `UsageHints.DynamicTransform` と組み合わせることで transform が GPU 側で処理され CPU 再テッセレーションが発生しない

**トレードオフ**: メニューが隠れている間も GPU が頂点シェーダーを処理する。
デバッグメニューはゲームの本番パフォーマンスに影響する場面では使われないため、許容範囲。

### UsageHints.DynamicTransform

translate を頻繁に変更する要素に設定する Usage Hint。
要素の transform が GPU メモリ上で直接管理され、translate 変更のたびに CPU でメッシュを再生成しない。

```csharp
public DebugMenuWindow()
{
    usageHints = UsageHints.DynamicTransform; // translate の頻繁な変更を最適化
    ...
}
```

---

## 事前アタッチとの組み合わせ

`display: none` を廃止しただけでは不十分。ページが `_contentContainer` に未アタッチのまま初回ナビゲーションを迎えると、アタッチ時にスパイクが発生する。

**ページを起動時に全て `_contentContainer` へ事前アタッチ**することで、
UIToolkit の処理コストをウィンドウ生成フレームに分散させる。

```csharp
// InitRootPage() 末尾で全プールページを OutR (left: 100%) にアタッチ
foreach (var page in _pagePool.GetAllPooledPages())
{
    if (page.parent != _contentContainer)
    {
        _contentContainer.Add(page);
        ShowPageImmediately(page, PagePosition.OutR); // left: 100%（画面右外）
    }
}
```

ポイント：
- `display: none` のウィンドウ配下では事前アタッチは無意味（UIToolkit がスキップするため）
- translate 方式に変更することで、事前アタッチが初めて意味を持つ
- ページのスライドアニメーションは `from` 位置（OutR）を再設定してから始まるため、事前アタッチ後も正常動作する

---

## pickingMode の初期化

`Hide()` では `_menuRoot.pickingMode = PickingMode.Ignore` が設定されるが、
アプリ起動直後（`Initialize()` 完了後・最初の `Show()` 呼び出し前）はこの設定がなかった。

`Initialize()` 内の `SetHidden()` 直後に明示的に設定する：

```csharp
window.SetHidden();
menuRoot.pickingMode = PickingMode.Ignore; // 初期状態でゲームUIをブロックしないよう設定
```

---

## 実装場所まとめ

| ファイル                                  | 変更内容                                                                                  |
| ----------------------------------------- | ----------------------------------------------------------------------------------------- |
| `Scripts/Core/DebugMenu.cs`               | `Initialize()` で `_menuRoot.pickingMode = Ignore` を追加、`Show()` で translate リセット |
| `Scripts/Core/View/DebugMenuWindow.cs`    | `UsageHints.DynamicTransform` を設定、`SetHidden()` を translate 方式に変更               |
| `Scripts/Core/View/DebugPageNavigator.cs` | `InitRootPage()` 末尾で全プールページを事前アタッチ                                       |
| `Scripts/Core/DebugPagePool.cs`           | `GetAllPooledPages()` メソッドを追加                                                      |


## 参考

Best practices for managing elements
https://docs.unity3d.com/Manual/UIE-best-practices-for-managing-elements.html