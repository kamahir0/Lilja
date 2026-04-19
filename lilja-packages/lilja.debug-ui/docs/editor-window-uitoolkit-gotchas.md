# EditorWindow × UIToolkit: ハマりどころと対処法

DebugMenuEditorWindow でランタイムの DebugPage を借用して表示する実装を通じて判明した
UIToolkit の挙動と対処法をまとめる。

---

## 1. エディタパネルでは Runtime テーマ変数が使えない

### 現象

`DebugMenuEditorWindow` は Runtime の `PanelSettings` を持たない独立したパネルで動く。
`DebugMenuDefaultTheme.uss` / `DebugMenuDarkTheme.uss` に定義された `:root` 変数は
エディタパネルには存在しない。

### 影響

`DebugMenu.uss` の `var(--hover-color)` / `var(--active-color)` がエディタ文脈では未定義になり、
`ButtonInteractionHelper` の `TryGetValue` が `Color.default`（透明）を返す。
ホバー・クリック時に要素が透明になる。

### 対処

`DebugMenuEditorTheme.uss` で **要素レベルのセレクタ** に `--hover-color` / `--active-color` を直接定義する。

```css
/* 各ボタン種別に定義しないと ButtonInteractionHelper が拾えない */
.editor-debug-window .c-button--secondary {
    --hover-color: var(--color-secondary-hover);
    --active-color: var(--color-secondary-active);
}
.editor-debug-window .c-nav-button {
    --hover-color: var(--color-nav-hover);
    --active-color: var(--color-nav-active);
}
```

### 補足: `TryGetValue` は未定義でも値をリセットしない

`CustomStyleResolvedEvent` で `TryGetValue` が `false` を返した場合、
`out` 変数は **前回の値を保持したまま** になる。
DebugPage をランタイムパネルから借用してエディタパネルへ移動すると、
`CustomStyleResolvedEvent` が再発火するが、エディタで該当プロパティが未定義なら
**ランタイム時の色が変数クロージャに残存する**。
エディタ側でプロパティを定義することで正しく上書きされる。

---

## 2. `--unity-colors-button-background-pressed` = 青 (Unity 6)

### 現象

Unity 6 のエディタテーマでは `--unity-colors-button-background-pressed` が
**青（ハイライト/選択色）** に解決される。通常のボタンでは「押下時に青くなる」のは
Unity 標準動作として期待通りだが、スクロールバードラッガーに適用すると不自然に見える。

### 対処

スクロールバードラッガーの `--active-color` には `--unity-colors-button-background-pressed` を
使わず、通常色と同じ値を指定して変化させない。

```css
.editor-debug-window .c-scroll-view .unity-scroller--vertical #unity-dragger {
    --active-color: var(--unity-colors-button-background); /* 青くならないよう通常色を使用 */
}
```

---

## 3. `BaseSlider` のフォーカスビジュアル（青ハイライト）

### 現象

`ScrollView` の `verticalScroller` 内部の `Slider`（`BaseSlider<float>`）はデフォルトでフォーカス可能。
ドラッガーをクリックすると `Slider` がフォーカスを受け取り、
Unity 組み込み USS の `:focus` スタイルが青ハイライトをドラッガーに適用する。

### CSS では解決できない

`DebugMenuEditorTheme.uss` で `:focus` をオーバーライドしても Unity 組み込みスタイルに勝てない。

```css
/* これは効かない */
.editor-debug-window .c-scroll-view .unity-scroller--vertical #unity-dragger:focus {
    background-color: var(--unity-colors-button-background);
}
```

### 対処: C# で `focusable = false` を設定

借用ページを `_rightPane` に追加した直後に `ScrollView` とそのスクローラー要素の
フォーカスを無効化する。

```csharp
private static void SuppressScrollbarFocus(VisualElement root)
{
    root.Query<ScrollView>().ForEach(sv =>
    {
        sv.focusable = false;
        sv.verticalScroller.focusable = false;
        sv.verticalScroller.slider.focusable = false;
    });
}
```

> クラス名クエリ（`Query(className: "unity-base-slider--vertical")`）よりも
> 型クエリ + 直接プロパティアクセスの方が確実。

---

## 4. UIToolkit の `border-radius` は H/V を独立にクランプする

### 現象

CSS 仕様では `border-radius` が要素サイズの半分を超えた場合、
H/V を**比例して**クランプするためカプセル（pill）形状になる。
UIToolkit では H と V を**独立に**クランプするため、大きな値を指定すると
各コーナーが楕円弧になり「縦に引き伸ばした歪な形状」になる。

例: 幅 13px・高さ 40px の要素に `border-radius: 100px` を指定した場合
- H 半径: min(100, 13/2) = **6.5px**
- V 半径: min(100, 40/2) = **20px**
- コーナー = 楕円(6.5, 20) → 上下が尖った歪な形状

### カプセル形状にするには

`border-radius ≤ 要素幅 ÷ 2` に収める。
Unity エディタのスクローラー幅は約 13px なので、`border-radius: 6px` が適切。

```css
#unity-dragger {
    border-radius: 6px; /* 幅(~13px) の半分以下 = 正確な半円端 */
}
```

---

## 5. `VisualElementInteractionHelper` への `DetachFromPanelEvent` 追加

### 背景

`ButtonInteractionHelper` は `DetachFromPanelEvent` でインライン `backgroundColor` をリセットするが、
`VisualElementInteractionHelper` の `Register` / `RegisterSliderDragger` / `RegisterHoverOnly` には
同じ処理が欠けていた。

### 問題

ランタイムでホバー中に DebugPage が借用されると、
`PointerLeaveEvent` が発火しないままページがエディタパネルへ移動する。
インラインスタイルにランタイムのホバー色が残存し、エディタ上で色が浮いて見える。

### 対処

各メソッドに `DetachFromPanelEvent` ハンドラを追加（`ButtonInteractionHelper` と同じパターン）。

```csharp
element.RegisterCallback<DetachFromPanelEvent>(_ =>
{
    isOver = false;
    isPressed = false; // Register / RegisterSliderDragger のみ
    element.style.backgroundColor = StyleKeyword.Null;
});
```

実装場所: `DebugControlHelpers.cs` の `VisualElementInteractionHelper` クラス内3メソッド

---

## 関連ファイル

| ファイル | 内容 |
|--------|------|
| `src/Editor/StyleSheets/DebugMenuEditorTheme.uss` | エディタ向け変数定義・直接スタイルオーバーライド |
| `src/Editor/DebugMenuEditorWindow.cs` | `SuppressScrollbarFocus` メソッド |
| `src/Runtime/Controls/DebugControlHelpers.cs` | `VisualElementInteractionHelper` の `DetachFromPanelEvent` 追加 |

---

## 6. InputField (BaseField) の外観崩れとレイアウトズレ

### 現象1: 背景色と枠線の同化

エディタウィンドウでは、Runtime 用のスタイルが適用されていても、Unity エディタ組み込みの `BaseField` スタイルが優先される場合がある。特に背景色（`background-color`）や枠線（`border-color`）がエディタウィンドウの背景と同化してしまい、入力欄がどこにあるか判別できなくなる。

### 現象2: ホバー/フォーカス時の左ズレ

Unity エディタ組み込みの USS には、`BaseField:hover` や `:focus` 時に `.unity-base-field__label` や `.unity-base-field__input` の `margin-left` や `padding-left` を変化させるルールが存在する。これにより、マウスを重ねたりクリックしたりすると、ラベルや入力中のテキストが左にガクッと動く現象が発生する。

### 対処

`!important` を活用してエディタ組み込みスタイルを強制的に上書きし、レイアウトを固定する。

#### 色の解決
`--unity-colors-input-field-background` が期待通りに動作しない場合は、エディタのボタン背景色など、確実に視認可能な変数で代用するのが安全。

#### レイアウトの固定
`margin` と `padding` を通常時・ホバー時・フォーカス時すべてで `!important` 固定する。

```css
/* DebugMenuEditorTheme.uss */

.editor-debug-window .c-input .unity-base-field__input {
    background-color: var(--unity-colors-button-background) !important;
    border-color: var(--unity-colors-input-field-border) !important;
    border-width: 1px !important;
}

.editor-debug-window .c-input:hover .unity-base-field__label,
.editor-debug-window .c-input:focus .unity-base-field__label {
    margin-left: 0 !important;
    padding-left: 0 !important;
}

.editor-debug-window .c-input:hover .unity-base-field__input,
.editor-debug-window .c-input:focus .unity-base-field__input {
    margin: 0 !important;
    padding: 0 8px !important; /* ランタイム側の padding と合わせる */
}
```

---

## 7. CompositeField (Vector / Rect / Bounds) のレイアウト崩れ

### 現象1: Vector 系の成分ラベル / 入力欄が hover・focus で動く

`Vector2Field` / `Vector3Field` / `Vector4Field` などの composite field は、
外側の `BaseField` の中に **成分ごとの子 `BaseField`** を持つ。

既存のエディタ向け対策がトップレベルの `.c-input` にしか効いていない場合、
内側の `X` / `Y` / `Z` / `W` 用フィールドには Unity エディタ組み込み USS の
hover / focus 時 `margin` / `padding` 変更が残り、各成分の位置がガタつく。

### 対処

`DebugMenuEditorTheme.uss` で、トップレベルだけでなく
`.unity-composite-field__field` 配下の `.unity-base-field__label` /
`.unity-base-field__input` に対しても `margin` / `padding` を `!important` で固定する。

また、成分ラベルの最小幅と入力欄までの余白を Editor テーマ側でも定義する。
ランタイムでは `:root` 変数で供給されているが、EditorWindow パネルには存在しないため、
未定義のままだと composite field 内の成分位置が安定しない。

```css
.editor-debug-window {
    --size-composite-label-min-width: 18px;
    --size-composite-label-gap: 4px;
}

.editor-debug-window .c-input .unity-composite-field__field > .unity-base-field__label {
    min-width: var(--size-composite-label-min-width);
    margin-right: var(--size-composite-label-gap) !important;
}
```

### 現象2: Rect / Bounds 系が 1 行ぶんの高さで潰れる

`RectField` / `BoundsField` / `BoundsIntField` などは複数行で描画されるが、
エディタテーマ側で `.c-control-size` に固定 `height` を与えていると、
内側のレイアウトが必要とする高さまで広がれずクリップされる。

ランタイム側は `height: auto` + `min-height` なので問題が起きないが、
EditorWindow 側だけ固定高さにしているとここで差異が出る。

### 対処

エディタテーマでもランタイムと同様に、
`.c-control-size` は固定 `height` ではなく `height: auto` と `min-height` を使う。

```css
.editor-debug-window .c-control-size {
    height: auto;
    min-height: var(--size-control-height);
}
```

これにより、
- 単一行の InputField / VectorField はコンパクトなまま
- Rect / Bounds 系だけ必要なぶんだけ縦に伸びる

という期待どおりの挙動になる。

---

## 8. Input の「箱」を描くべき要素と Bounds の hover 罠

### 現象

EditorWindow 側で input の背景色・枠線を直そうとして
`.unity-base-field__input` や `>.unity-base-field__input` に
`background-color` / `border` を付けると、

- `TextField` / `IntegerField` では効いたように見える
- `Vector` 系では成分入力欄に対して効いたり効かなかったりする
- `Bounds` / `BoundsInt` では **入力欄だけでなく `Center` / `Extents` や
  `Position` / `Size` の行全体に箱が出る**

という不安定な状態になりやすい。

さらに `Bounds` 系では hover 状態が少なくとも以下の 3 段階ある。

1. `Bounds` 全体への hover
2. `Center` 行への hover
3. `Extents` 行への hover

このため、一部の hover セレクタだけ透明化しても
「hover している側だけ正しい / していない側だけ壊れる」という症状が起こる。

### 原因

UIToolkit の `BaseField` / `CompositeField` では、
見た目上の「入力ボックス」と DOM 上の `.unity-base-field__input` が
一致しないことがある。

特に EditorWindow 上で問題になったのは以下。

- `.unity-base-field__input` は **入力欄そのものではなくラッパー** の場合がある
- 実際に文字が入る箱は `#unity-text-input` にぶら下がっている
- `Bounds` / `BoundsInt` は multi-line 構造のため、`Center` / `Extents` の
  行ラッパーにも hover が波及する

つまり、

- `margin` / `padding` のリセットはラッパーに当ててもよい
- しかし **背景色と枠線は「実際の入力欄」にだけ付けるべき**

という切り分けが必要。

### 対処

#### 1. 箱の見た目は `#unity-text-input` に描く

```css
.editor-debug-window .c-input #unity-text-input {
    margin: 0 !important;
    padding: 0 8px !important;
    background-color: var(--color-input-editor-background) !important;
    border-color: var(--color-input-editor-border) !important;
    border-width: 1px !important;
}
```

hover / focus も同じく `#unity-text-input` 側で完結させる。

#### 2. `Bounds` の行ラッパーは通常時だけでなく hover / focus 時も透明維持する

`Bounds` 全体への hover が入った時点で `Center` / `Extents` 両方の行ラッパーに
影響するため、個別行だけでなく **親 `Bounds` hover から各行ラッパーへ届く透明化**
も必要。

```css
.editor-debug-window .c-input.unity-bounds-field:hover .unity-bounds-field__center-field > .unity-base-field__input,
.editor-debug-window .c-input.unity-bounds-field:hover .unity-bounds-field__extents-field > .unity-base-field__input,
.editor-debug-window .c-input.unity-bounds-int-field:hover .unity-bounds-int-field__position-field > .unity-base-field__input,
.editor-debug-window .c-input.unity-bounds-int-field:hover .unity-bounds-int-field__size-field > .unity-base-field__input {
    background-color: rgba(0, 0, 0, 0) !important;
    border-width: 0 !important;
}
```

ポイントは、

- 行そのものに箱を描かない
- 実入力欄だけに箱を描く
- hover 中も行ラッパーを透明に保つ

の 3 つ。

### 再発防止チェックリスト

- `background-color` / `border` を付ける前に、そのセレクタが **ラッパー** なのか
  **実入力欄** なのか確認する
- `margin` / `padding` と `background` / `border` は、同じ要素に当てるとは限らない
- `Bounds` / `BoundsInt` は hover 状態を「全体」「各行」で分けて考える
- `hover` で一部だけ壊れる場合は、親 hover が sibling 行に波及していないか疑う

### 実装場所

- `src/Editor/StyleSheets/DebugMenuEditorTheme.uss`
  - `#unity-text-input` への背景色・枠線付与
  - `Bounds` / `BoundsInt` ラッパー hover 時の透明維持
