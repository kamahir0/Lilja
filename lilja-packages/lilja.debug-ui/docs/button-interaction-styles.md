# ボタン・スクローラーのホバー・クリック外観カスタマイズ

## 背景と問題

Unity UI Toolkit のデフォルトテーマ (`unity-theme://default`) は、ボタンのホバー・クリック時に `background-color` を強制上書きする。
このため、以下のアプローチはいずれも機能しない。

| 試みたアプローチ                                          | 結果                             | 理由                                                                               |
| --------------------------------------------------------- | -------------------------------- | ---------------------------------------------------------------------------------- |
| USS `:hover` に `background-color`                        | 効かない                         | デフォルトテーマが後段で上書き                                                     |
| USS `!important`                                          | 効かない                         | 同上（Unity の実装上 `!important` が無効）                                         |
| `PointerDownEvent` コールバックで `style.backgroundColor` | ホバーは効く、クリックは効かない | `Clickable` が `StopImmediatePropagation()` を呼ぶため PointerDownEvent が届かない |

ただし `color` や `border-color` は USS `:hover` で変更できる（background-color だけが特殊）。

---

## 解決策

### ホバー色（PointerEnter / PointerLeave）

`PointerEnterEvent` / `PointerLeaveEvent` に登録したコールバックで `style.backgroundColor` を直接セットする。
`Clickable` は `PointerDownEvent` のみを止め、Enter/Leave は止めないため、これらのイベントは届く。

```csharp
button.RegisterCallback<PointerEnterEvent>(_ => button.style.backgroundColor = hoverColor);
button.RegisterCallback<PointerLeaveEvent>(_ => button.style.backgroundColor = StyleKeyword.Null);
```

### クリック色（Button / DebugButton 系）

`Button` の `Clickable` マニピュレータは `PointerDownEvent` 内で `StopImmediatePropagation()` を呼ぶ。
そのため後から登録した `PointerDownEvent` コールバックは一切届かない。

解決策は `Clickable` のサブクラスを作り、`ProcessDownEvent` / `ProcessUpEvent` をオーバーライドしてフックを挿入すること。

```csharp
internal class InteractiveClickable : Clickable
{
    public event Action OnPressed;
    public event Action OnReleased;

    public InteractiveClickable() : base((Action)null) { }

    protected override void ProcessDownEvent(EventBase evt, Vector2 localPosition, int pointerId)
    {
        OnPressed?.Invoke();               // base より先に呼ぶ
        base.ProcessDownEvent(evt, localPosition, pointerId); // ここで StopImmediatePropagation が呼ばれる
    }

    protected override void ProcessUpEvent(EventBase evt, Vector2 localPosition, int pointerId)
    {
        base.ProcessUpEvent(evt, localPosition, pointerId);
        OnReleased?.Invoke();
    }
}
```

このクラスで `button.clickable` を置き換えてから、`OnPressed` / `OnReleased` に色変更ロジックを登録する。

```csharp
var clickable = new InteractiveClickable();
button.clickable = clickable;
clickable.OnPressed  += () => button.style.backgroundColor = activeColor;
clickable.OnReleased += () => button.style.backgroundColor = isOver ? (StyleColor)hoverColor : StyleKeyword.Null;
```

> **重要**: `button.clickable` を置き換えると元の `clickable.clicked` イベントの購読が失われる。
> `button.clicked += handler` は **Register より後に** 呼ぶこと。

---

## ScrollView スクローラーの対応

ScrollView の `verticalScroller` 配下には以下の要素がある。

| 要素           | クラス / ID                    | アクセス方法                         |
| -------------- | ------------------------------ | ------------------------------------ |
| 上矢印ボタン   | `.unity-scroller__high-button` | `scroller.highButton`                |
| 下矢印ボタン   | `.unity-scroller__low-button`  | `scroller.lowButton`                 |
| スライダーノブ | `#unity-dragger`               | `scroller.slider.Q("unity-dragger")` |
| スライダー背景 | `#unity-tracker`               | `scroller.slider.Q("unity-tracker")` |

### RepeatButton（上下矢印）

`RepeatButton` の内部マニピュレータ（`Pressable`）は `StopPropagation()` のみ使用しており、`StopImmediatePropagation()` を呼ばない。
そのため `PointerDownEvent` / `PointerUpEvent` を直接 BubbleUp で登録するだけで動作する。
`ButtonInteractionHelper` と同様の `VisualElementInteractionHelper.Register()` で対応。

### スライダーノブ（#unity-dragger）

`Slider` の内部 `ClampedDragger` マニピュレータは `TrickleDown` フェーズで `PointerDownEvent` を処理し、
`StopImmediatePropagation()` を呼ぶため、ノブ自身への登録では一切届かない。

| 試みたアプローチ                       | 結果     | 理由                                                      |
| -------------------------------------- | -------- | --------------------------------------------------------- |
| `PointerDownEvent` BubbleUp（ノブ）    | 効かない | ClampedDragger が TrickleDown で StopImmediatePropagation |
| `PointerCaptureEvent`（ノブ）          | 効かない | キャプチャは Slider 側が行うためノブに届かない            |
| `PointerUpEvent` TrickleDown（Slider） | 効かない | Slider の内部処理が先に StopImmediatePropagation          |

**解決策（確定）**:

- **押下検知**: 親 `Slider` に **TrickleDown** で `PointerDownEvent` を登録し、`e.target == dragger` のときに反応。  
  TrickleDown はイベントが親→子に伝播する段階で発火するため、ClampedDragger が処理する前に確実に拾える。

- **解放検知**: `PointerUpEvent` は内部処理でブロックされるため、`PointerCaptureOutEvent`（キャプチャ解放通知）を使う。  
  キャプチャ元が Slider か Dragger かわからないため、両方に登録し `isPressed` フラグで二重実行を防ぐ。

```csharp
// 押下: 親 Slider に TrickleDown で登録
slider.RegisterCallback<PointerDownEvent>(e =>
{
    if (e.target == dragger)
    {
        isPressed = true;
        dragger.style.backgroundColor = activeColor;
    }
}, TrickleDown.TrickleDown);

// 解放: PointerCaptureOutEvent で検知（どちらがキャプチャしているか不明なので両方）
void OnRelease()
{
    if (!isPressed) return;
    isPressed = false;
    dragger.style.backgroundColor = isOver ? (StyleColor)hoverColor : StyleKeyword.Null;
}
slider.RegisterCallback<PointerCaptureOutEvent>(_ => OnRelease());
dragger.RegisterCallback<PointerCaptureOutEvent>(_ => OnRelease());
```

### スライダー背景（#unity-tracker）

背景はホバー時のみ色を変える（クリック時は変化不要）。
`PointerEnterEvent` / `PointerLeaveEvent` のみ登録する `VisualElementInteractionHelper.RegisterHoverOnly()` を使用。

---

## CSS カスタムプロパティによるテーマ対応

ホバー色・アクティブ色は USS のカスタムプロパティで定義し、C# 側で `ICustomStyle.TryGetValue` を使って読み取る。

### 注意: `:root` では読み取れない

`ICustomStyle` は **その要素に直接適用されたプロパティ** しか読み取れない。
`:root` に定義した変数は USS の `var()` では参照できるが、C# の `TryGetValue` では取得できない。

```css
/* NG: :root に書いても ICustomStyle から読めない */
:root { --hover-color: #0062CC; }

/* OK: 要素のクラスに直接書く */
.c-button--primary { --hover-color: #0062CC; --active-color: #004C9E; }
```

複合セレクタ（`.c-scroll-view .unity-scroller--vertical #unity-dragger`）でも、最終的にその要素に直接マッチしていれば `TryGetValue` で読み取れる。

### .tss のインポート順と上書き

`.tss` のインポート順は以下の通り。

```
@import url("unity-theme://default");
@import url("DebugMenuXxxTheme.uss");   ← テーマ（後から上書きされる側）
@import url("DebugMenu.uss");            ← ベーススタイル（最後なので同セレクタでは勝つ）
```

`DebugMenu.uss` の同セレクタに `--hover-color` を書くとテーマの定義を上書きしてしまう。
**ホバー色・アクティブ色は `DebugMenu.uss` には書かず、テーマ側のみで定義すること。**

### 読み取り方（C#）

```csharp
static readonly CustomStyleProperty<Color> s_HoverColor  = new("--hover-color");
static readonly CustomStyleProperty<Color> s_ActiveColor = new("--active-color");

button.RegisterCallback<CustomStyleResolvedEvent>(e =>
{
    e.customStyle.TryGetValue(s_HoverColor,  out hoverColor);
    e.customStyle.TryGetValue(s_ActiveColor, out activeColor);
});
```

---

## 実装場所

| ファイル                                                       | 役割                                                                                         |
| -------------------------------------------------------------- | -------------------------------------------------------------------------------------------- |
| `src/Runtime/DebugMenu/Scripts/DebugControls/DebugControls.cs` | `InteractiveClickable` / `ButtonInteractionHelper` / `VisualElementInteractionHelper` の定義 |
| `src/Runtime/DebugMenu/Scripts/Core/View/DebugPage.cs`         | ScrollView スクローラー要素への登録                                                          |
| `src/Runtime/DebugMenu/StyleSheets/DebugMenuDefaultTheme.uss`  | `--hover-color` / `--active-color` の Default テーマ値                                       |
| `src/Runtime/DebugMenu/StyleSheets/DebugMenuDarkTheme.uss`     | `--hover-color` / `--active-color` の Dark テーマ値                                          |

### テーマファイルの記述例

```css
/* ボタン系 */
.c-button--primary   { --hover-color: #0062CC; --active-color: #004C9E; }
.c-button--secondary { --hover-color: #E8E8E8; --active-color: #D0D0D0; }
.c-button--danger    { --hover-color: #C62828; --active-color: #9A0007; }
.c-nav-button        { --hover-color: #EEEEEE; --active-color: #D5D5D5; }
.c-menu-window__back-button,
.c-menu-window__back-to-root-button { --hover-color: #F0F0F0; --active-color: #E0E0E0; }

/* スクローラー系 */
.c-scroll-view .unity-scroller--vertical .unity-scroller__low-button,
.c-scroll-view .unity-scroller--vertical .unity-scroller__high-button { --hover-color: #E8E8E8; --active-color: #D0D0D0; }
.c-scroll-view .unity-scroller--vertical #unity-dragger { --hover-color: #EEEEEE; --active-color: #D5D5D5; }
.c-scroll-view .unity-scroller--vertical #unity-tracker { --hover-color: #C8C8C8; }
```

新しいテーマを作る場合は、上記クラスセレクタに `--hover-color` と `--active-color`（tracker は `--hover-color` のみ）を定義するだけでよい。
