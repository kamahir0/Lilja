# 自作デバッグメニューパッケージと UIToolkit 実戦知見

15〜20分想定の社内発表スライド案。

形式:

- `本文`: スライドに表示する内容
- `備考`: 発表者ノート。口頭で補足する内容、実装背景、話す順番

---

## 1. タイトル

### 本文

# 自作デバッグメニューパッケージと UIToolkit 実戦知見

- Lilja.DebugUI
- 技術仕様・特徴紹介
- UIToolkit の良かったところ / 実際に使って分かったツラいところ

### 備考

最初に「UIToolkitの紹介」ではなく「実際にパッケージを作って分かった話」と位置づける。

今日の価値は、公式ドキュメントや軽い入門記事に載っている内容よりも、実装して初めて見えた挙動、回避策、設計判断にある。

---

## 2. 今日の論旨

### 本文

## 今日の論旨

UIToolkit は、デバッグメニューのような「ツールUI」を作るにはかなり相性が良い。

ただし、製品品質の操作感まで作り込むと、以下の壁に当たる。

- 標準コントロールの内部イベントが素直に取れない
- Runtime と EditorWindow でテーマ・描画・レイアウトの前提が違う
- 非表示管理や初回生成でパフォーマンススパイクが出る
- USS だけでは制御しきれず、C# 側の補助が必要になる

### 備考

このスライドで発表全体の軸を置く。

ポジティブな話だけだと「UIToolkit便利だね」で終わる。今回は「便利だけど、ここまでやるならこのへんを踏む」という実戦知見に価値がある。

---

## 3. 作ったもの

### 本文

## Lilja.DebugUI

ゲーム実行中に表示できるデバッグメニュー用パッケージ。

- Runtime 上にデバッグメニューを表示
- `DebugPage` 単位で画面を構築
- Builder API で UI を C# から追加
- ページ遷移、戻る、Root へ戻る
- ボタン、入力欄、スライダー、Enum、Vector、Rect、Bounds などを用意
- EditorWindow から Runtime の DebugPage を借用して確認可能
- Theme Style Sheet を差し替えて見た目をカスタム可能

### 備考

「何を作ったか」を短く伝える。

ポイントは、単なる画面1枚ではなく、複数ページを持つデバッグツールの土台として作っていること。

テーマは `PanelSettings.themeUss` に設定する `.tss` で切り替える。既定では `DebugMenuDefaultTheme.tss` があり、Dark 用には `DebugMenuDarkTheme.tss` を用意している。

`DebugMenuDarkTheme.tss` は `unity-theme://default`、`DebugMenuDefaultTheme.uss`、`DebugMenuDarkTheme.uss`、共通の `DebugMenu.uss` を import する構造。

---

## 4. 使い方のイメージ

### 本文

## Builder API

```csharp
public override void Configure(IDebugUIBuilder builder)
{
    builder.Label("Repository test");

    builder.Foldout("Input", foldout =>
    {
        foldout.IntegerField("Id");

        foldout.HorizontalScope(row =>
        {
            row.PrimaryButton("Create", Create);
            row.SecondaryButton("Read", Read);
            row.DangerButton("Delete", Delete);
        });
    });
}
```

### 備考

UXML を毎回書かず、C# でデバッグ項目を積んでいける設計。

ゲーム内デバッグ用途では「一時的に項目を足す」「実行中の値に応じて表示を変える」「ページの末尾に追加する」ことが多いので、Builder API にした。

---

## 5. 全体構成

### 本文

## 技術構成

- `DebugMenu`
  - 初期化、表示/非表示、ページ登録の入口
- `DebugMenuWindow`
  - Runtime 側の表示コンテナ
- `DebugPage`
  - 1画面分の UI
- `DebugPageCache`
  - ページ生成と再利用
- `RuntimePageNavigator` / `EditorPageNavigator`
  - ページ遷移を担当
- `HostRegistry`
  - Runtime / EditorWindow の所有権管理

### 備考

ここは詳しくなりすぎない。

重要なのは「ページ」「ナビゲーション」「Runtime/Editorの2ホスト」という3点。

EditorWindow でも Runtime と同じ `DebugPage` を表示できるようにしたため、所有権管理が必要になった。

---

## 6. 特徴1: Runtime と EditorWindow の両対応

### 本文

## Runtime / EditorWindow 両対応

Runtime:

- ゲーム画面上にデバッグメニューを表示
- 実機に近い操作感で確認できる

EditorWindow:

- PlayMode 中にデバッグページを EditorWindow 側へ借用
- ゲーム画面を隠さずにデバッグ項目を操作できる
- Runtime と同じ `DebugPage` を再利用できる

### 備考

この機能は便利だが、後半の「EditorWindow のツラさ」につながる伏線。

同じ VisualElement ツリーを別パネルへ移すと、テーマ変数、テキスト描画、フォーカススタイルなどで差分が出る。

---

## 7. 特徴2: 一時ページと動的追加

### 本文

## 一時ページ / 動的追加

- `NavigationButton`
  - 登録済みページへ遷移
- `TempNavigationButton`
  - その場で一時ページを作る
- `AddDebugUI`
  - 実行中に UI を追加
  - `IDisposable` で削除

### 備考

デバッグUIでは、常に固定のメニューだけでは足りない。

例:

- 選択中のオブジェクトに応じた詳細ページ
- 通信結果に応じた一時操作
- テスト中だけ追加したい操作群

こういう用途に、UXML 固定より C# 構築の方が合う。

---

## 8. 特徴3: テーマカスタム

### 本文

## テーマカスタム

見た目は Theme Style Sheet で切り替え可能。

- `DebugMenuDefaultTheme.tss`
- `DebugMenuDarkTheme.tss`
- 共通スタイル `DebugMenu.uss`
- テーマ側で色やサイズの変数を定義
- `PanelSettings.themeUss` を差し替えて適用

### 備考

序盤でこれを言っておくと、後半の「USS だけでは background-color が制御しきれず C# 補助が必要だった」「C# から読むカスタムプロパティは対象要素に直接定義する必要がある」という話につながる。

`DebugMenuDarkTheme.tss` は、Default の定義を読み込んだ上で Dark 用 USS を重ね、最後に共通スタイルを読む。テーマごとの差分をテーマ USS に閉じ込める狙い。

---

## 9. UIToolkit の良かったところ

### 本文

## UIToolkit の良かったところ

- C# から UI を動的構築しやすい
- `VisualElement` ツリーとして扱える
- USS で見た目を分離できる
- `ScrollView` / `Button` / `BaseField` など標準部品が多い
- Runtime と Editor の両方で同じ思想の UI が作れる
- IMGUI より構造化しやすい

### 備考

ここは一般論も含むが、短めにする。

このあと「でも実際に使うとこうだった」に入るため、良いところは長く話しすぎない。

このパッケージには確かに向いていた、という前提を作る。

---

## 10. ただし、ハマりどころは浅くない

### 本文

## 実際に使って分かったツラさ

- USS だけで制御できない見た目がある
- 標準コントロールの内部実装にイベントを止められる
- `:root` のカスタムプロパティを C# から読めない
- Runtime パネルと EditorWindow パネルで挙動が変わる
- 非表示の仕方で初回表示コストが大きく変わる
- 内部要素名や階層に依存せざるを得ない場面がある

### 備考

ここからが発表のメイン。

「UIToolkit が悪い」というより、「Web 的な見た目制御を期待すると違う」「Unity の標準コントロールは内部 Manipulator が強い」「Runtime と Editor は似ているが同じではない」という話。

---

## 11. 前提: BubbleUp / TrickleDown とは

### 本文

## UIToolkit のイベント伝播

UIToolkit のイベントは、VisualElement ツリー上を段階的に伝播する。

```text
Root
 └─ Parent
     └─ Button
```

Button を押した場合:

1. `TrickleDown`: Root → Parent → Button
2. Target: Button
3. `BubbleUp`: Button → Parent → Root

### 備考

Web のキャプチャフェーズ / バブリングフェーズに近い説明でよい。

`BubbleUp` は、イベント発生元から親方向へ戻っていく段階。UIToolkit の `RegisterCallback<TEvent>(callback)` は、特に指定しない場合この BubbleUp 側で登録される。

`TrickleDown` は、イベント発生元へ向かって親から子へ降りていく段階。`RegisterCallback<TEvent>(callback, TrickleDown.TrickleDown)` と書くとこちらで拾える。

何もしなければ、対象要素で発生したイベントは BubbleUp で親方向へ流れる。

今回の Button 問題では、内部の `Clickable` が `StopImmediatePropagation()` を呼ぶため、通常なら流れるはずの `PointerDown` が途中で止まる、という話になる。

---

## 12. ツラさ1: Button の PointerDown が取れない

### 本文

## Button の PointerDown 問題

標準 `Button` に BubbleUp で `PointerDownEvent` を登録しても、押下を拾えない。

```csharp
button.RegisterCallback<PointerDownEvent>(...);
```

`Button` も `VisualElement` なので、このコードは動きそうに見える。

再現結果:

- `Button PointerDown (BubbleUp)`: 0 のまま
- `Button PointerDown (TrickleDown)`: 増える
- `Button.clicked`: 増える

原因:

- 通常なら BubbleUp で上に流れる
- しかし `Button` 内部の `Clickable` が `PointerDownEvent` で `StopImmediatePropagation()` する
- 結果として、後から普通に登録した BubbleUp の `PointerDown` には届かない

### 備考

ここで再現シーンを見せると強い。

重要なのは「クリックできない」ではない。`Button.clicked` は取れる。

問題は、押した瞬間の状態、押している間の見た目、離した瞬間の状態を自分で管理したいときに困ること。

「イベントを消費したら自然に上へ流れない」というより、「本来は流れるイベントを、内部実装が `StopImmediatePropagation()` で意図的に止めている」と説明すると正確。

ユーザー目線では、`Button` が `VisualElement` であり `RegisterCallback<PointerDownEvent>` できる以上、普通に届くと思いやすい。ここが罠。

---

## 13. Button 問題が実際に何に効いたか

### 本文

## なぜ対策が必要だったか

このパッケージでは、ボタンの押下中スタイルを自前制御している。

- hover 時: `hoverColor`
- press 中: `activeColor`
- release 後: hover 中なら `hoverColor`、外なら通常色

対象:

- `DebugButton`
- `DebugSecondaryButton`
- `DebugDangerButton`
- `DebugNavigationButton`
- 戻る / Root へ戻るボタン

### 備考

「Button.clicked は取れるのに、なぜそこまで必要？」への答え。

クリック後の処理だけなら不要。しかし UI の操作感として「押している間だけ色が変わる」を作るには、press / release が必要。

---

## 14. Button 問題の対策

### 本文

## 対策: Clickable を差し替える

`Clickable` を継承し、`ProcessDownEvent` / `ProcessUpEvent` をフック。

```csharp
internal class InteractiveClickable : Clickable
{
    public event Action OnPressed;
    public event Action OnReleased;

    protected override void ProcessDownEvent(
        EventBase evt, Vector2 localPosition, int pointerId)
    {
        OnPressed?.Invoke();
        base.ProcessDownEvent(evt, localPosition, pointerId);
    }

    protected override void ProcessUpEvent(
        EventBase evt, Vector2 localPosition, int pointerId)
    {
        base.ProcessUpEvent(evt, localPosition, pointerId);
        OnReleased?.Invoke();
    }
}
```

### 備考

実装場所は `DebugControlHelpers.cs` の `InteractiveClickable` / `ButtonInteractionHelper`。

注意点として、`button.clickable` を置き換えると既存の clickable 側の購読が失われる。なので、この helper を通したあとに `button.clicked += ...` する設計にしている。

---

## 15. ツラさ2: Slider の PointerUp が取れない

### 本文

## Slider / Scroller dragger の PointerUp 問題

`ScrollView` のスクロールバーつまみで確認。

再現結果:

- `Dragger PointerDown`: 増える
- `Slider PointerDown`: 増える
- `Dragger PointerUp`: 0 のまま
- `Slider PointerUp`: 0 のまま
- `PointerCaptureOut による解放検知`: 増える

### 備考

これも再現シーンを見せる。

押下は取れる。しかし解放が取れない。

`PointerUp` が取れないと、ドラッグ中の見た目を解除できない。

---

## 16. Slider 問題が実際に何に効いたか

### 本文

## なぜ対策が必要だったか

`DebugPage` は中身が長くなるため `ScrollView` を持つ。

その縦スクロールバーの dragger で、以下を自前制御している。

- hover 中の背景色
- drag 中の背景色
- drag 終了後の背景色リセット

`PointerUp` が取れないと:

- `isPressed = true` のまま残る
- dragger が押されっぱなしの見た目になる
- EditorWindow へページ移動したときにも色が残る可能性がある

### 備考

ここも「値変更」ではなく「見た目と状態管理」の話。

スライダーの value を変えるだけなら `ChangeEvent` で十分。今回はスクロールバーの内部部品をテーマに合わせるための話。

---

## 17. Slider 問題の対策

### 本文

## 対策: PointerCaptureOutEvent を release として扱う

押下:

- 親 `Slider` に `TrickleDown` で `PointerDownEvent` を登録
- `e.target == dragger` のときだけ反応

解放:

- `PointerUpEvent` ではなく `PointerCaptureOutEvent` を使う
- `slider` と `dragger` の両方に登録
- `isPressed` で二重実行を防ぐ

### 備考

実装場所は `VisualElementInteractionHelper.RegisterSliderDragger`。

`PointerCaptureOutEvent` は「マウスボタンが上がった」そのものではなく、「ポインターキャプチャが外れた」イベント。だが、このケースでは drag 終了シグナルとして使える。

この話は、実際に触らないとまず出てこない知見。

---

## 18. ツラさ3: USS だけでは背景色を制御しきれない

### 本文

## Button / Scroller の色制御

試したこと:

- USS `:hover` で `background-color`
- USS `!important`
- `PointerDownEvent` で `style.backgroundColor`

起きたこと:

- Unity のデフォルトテーマに上書きされる
- Button の `PointerDown` は内部で止められる
- 結局 C# から inline style を制御する必要があった

### 備考

UIToolkit は USS があるので CSS 的に扱えるが、「CSS と完全に同じ」と思うと危ない。

今回の背景色制御は、USS のカスタムプロパティで色を定義し、C# の `CustomStyleResolvedEvent` で読み、イベントに応じて `style.backgroundColor` を直接入れる形にした。

---

## 19. ツラさ4: カスタムプロパティの読み取り

### 本文

## `ICustomStyle.TryGetValue` の罠

USS:

```css
:root {
    --hover-color: #eeeeee;
}
```

C#:

```csharp
e.customStyle.TryGetValue(s_HoverColor, out hoverColor);
```

この組み合わせでは読めない。

対策:

- C# から読むカスタムプロパティは、対象要素に直接マッチするセレクタへ書く

### 備考

USS の `var()` と、C# の `ICustomStyle` は同じ感覚で使えない。

`:root` に置いた変数は USS 内では参照できるが、`TryGetValue` では対象要素に直接適用された値として取れない。

テーマ設計に影響する話なので、かなり実務的。

---

## 20. ツラさ5: Runtime と EditorWindow は同じではない

### 本文

## EditorWindow 対応で踏んだこと

Runtime の `DebugPage` を EditorWindow 側へ移すと、次の差分が出た。

- Runtime 用テーマ変数が EditorWindow パネルに存在しない
- BaseField の背景・枠線が Editor 組み込み USS に負ける
- hover / focus で入力欄のラベルやテキストがずれる
- CompositeField / BoundsField は内部構造が複雑
- TextElement の縦位置がずれるケースがある

### 備考

「Runtime と Editor で同じ VisualElement を使える」は良い点。

ただし同じ見た目になるとは限らない。

EditorWindow は Editor 側の組み込み USS やテキスト描画設定の影響を受けるため、Runtime で完成したスタイルがそのまま通るとは限らない。

---

## 21. EditorWindow 対応で入れた対策

### 本文

## EditorWindow 側の対策

- `DebugMenuEditorTheme.uss` を別途用意
- Runtime 用テーマ変数を EditorWindow 側にも定義
- `BaseField` / `CompositeField` の margin / padding を固定
- 入力欄の箱は `#unity-text-input` に描く
- `Bounds` / `BoundsInt` の行ラッパーは透明維持
- EditorWindow 表示時に TextElement を refresh
- ページ移動時に inline style を残さない

### 備考

ここは細かいので全部説明しきらない。

重要なのは「EditorWindow 対応は単なる表示先変更ではなく、別パネルへの移植作業に近い」ということ。

---

## 22. ツラさ6: 非表示管理と初回表示スパイク

### 本文

## `display: none` の罠

`display: none` でメニューを隠すと:

- レイアウト計算がスキップされる
- スタイル解決がスキップされる
- フォントアトラス生成も遅れる

結果:

- 表示した瞬間に処理がまとまって走る
- 初回表示や初回ページ遷移でスパイクが出る

### 備考

これはパフォーマンス面の実戦知見。

隠している間に処理されないことは一見よさそうだが、デバッグメニューでは「開いた瞬間に重い」が一番困る。

---

## 23. 非表示管理の採用案

### 本文

## 採用: 画面外 translate

非表示時:

```csharp
style.translate = new StyleTranslate(new Translate(-5000, -5000));
style.opacity = 0f;
```

採用理由:

- レイアウト・スタイル計算は済ませられる
- 画面外なので pointer event が自然に当たらない
- `UsageHints.DynamicTransform` で transform 更新コストを抑えられる

### 備考

`visibility: hidden` や `opacity: 0` も検討したが、pointer event の扱いが難しい。

`pickingMode = Ignore` は子要素に伝播しないので、親だけ Ignore にしても子の Button が hit-test される可能性がある。

そのため、物理的に画面外へ出すのが一番扱いやすかった。

---

## 24. 設計として良かった判断

### 本文

## 設計として良かったこと

- UI 構築を Builder API に寄せた
- ページ単位で責務を分けた
- Runtime / Editor のナビゲーションを分離した
- `HostRegistry` で所有権を明示した
- 見た目のテーマ値は USS、状態遷移は C# に分けた
- UIToolkit の挙動差分を helper に閉じ込めた

### 備考

このスライドで「苦労話」から「パッケージとしてどう吸収したか」に戻す。

実装上の罠を、利用者に直接踏ませないための helper や抽象化が今回のパッケージ価値。

---

## 25. UIToolkit を使うときの判断基準

### 本文

## UIToolkit はどこに向いているか

向いている:

- ツール UI
- デバッグメニュー
- 設定画面
- 入力フォーム
- Editor 拡張と Runtime で思想を揃えたい UI

注意が必要:

- 独自の押下/ドラッグ操作感を細かく作り込む UI
- 標準部品の内部要素を強くカスタムする UI
- Runtime と EditorWindow で完全同一表示を期待する UI
- 初回表示スパイクを嫌う UI

### 備考

「使うべき / 使うべきでない」の単純な結論にしない。

UIToolkit は強い。ただし、標準部品の外観やイベントを深く触るほど、内部実装への理解が必要になる。

---

## 26. まとめ

### 本文

## まとめ

- UIToolkit はデバッグメニュー用途にかなり向いている
- C# から構造化 UI を作れるのは強い
- ただし、標準コントロールのイベント伝播は素直ではない
- Runtime と EditorWindow は同じ VisualElement でも同じ挙動にはならない
- 非表示管理はパフォーマンスに直結する
- 今回のパッケージでは、それらのツラさを helper / theme / navigator に閉じ込めた

### 備考

最後はポジティブに締める。

結論:

「UIToolkit は便利。ただし、いい感じのデバッグメニューを作るには、標準部品の外側だけでなく内側の挙動まで見る必要があった。その知見をパッケージに封じ込めた。」

---

## 27. デモ案

### 本文

## デモ

1. Runtime でデバッグメニューを開く
2. ページ遷移、戻る、Root へ戻る
3. Builder API で作った各種コントロールを見る
4. EditorWindow 側で同じページを表示
5. Button PointerDown 再現シーン
6. Slider PointerCaptureOut 再現シーン

### 備考

時間が厳しければ 5, 6 だけでも良い。

今回の発表で一番価値が高いのは「本当にそうなるの？」と言われやすい挙動を再現シーンで見せられること。

再現シーンで見せるポイント:

- Button: BubbleUp の PointerDown だけ 0 のまま
- Slider: PointerUp は 0 のまま、PointerCaptureOut だけ増える

---

## 28. 時間配分案

### 本文

## 時間配分

- 0:00-2:00 何を作ったか
- 2:00-5:00 パッケージ構成と使い方
- 5:00-7:00 UIToolkit の良かったところ
- 7:00-13:00 実際に踏んだツラさ
- 13:00-17:00 Button / Slider 再現デモ
- 17:00-19:00 設計判断とまとめ
- 19:00-20:00 質疑

### 備考

15分に寄せるなら、EditorWindow の細かい話を圧縮する。

20分取れるなら、Button / Slider の再現シーンを丁寧に見せる。

「悪いところ」は多いが、全部を詳細説明すると散る。主役は Button / Slider / EditorWindow / 非表示パフォーマンスの4つに絞る。
