---
theme: default
aspectRatio: 16/9
canvasWidth: 1280
class: title-slide
transition: none
lineNumbers: false
lang: ja
title: デバッグメニュー自作してみた
info: Lilja.DebugUI と UI Toolkit 実戦知見
---

# デバッグメニュー<br>自作してみた

<p class="subtitle">F2F クライアント 平塚</p>

<!--
0:00-0:40
自己紹介
今日は「UI Toolkit入門」ではなく、デバッグメニューを実際に作ってみて、どこが良かったか、どこが厳しかったかを話す
前半は作ったものの紹介、後半はUI Toolkitのかなり正直な感想
-->

---

## 何を作ったのか

<div class="grid-2">
  <div>
    <p class="big-message">階層式の<br>デバッグメニュー</p>
    <div class="stack">
      <div class="stack-row accent">3回タップで起動</div>
      <div class="stack-row">ページ単位で整理</div>
      <div class="stack-row">Runtime / EditorWindow 両対応</div>
    </div>
  </div>
  <figure class="image-frame">
    <img src="./assets/images/runtime-menu.png" alt="Runtime debug menu screenshot">
  </figure>
</div>

<!--
3:30-
デモで見せたものを一言で整理する
「何を作ったのかと言うと、階層式のデバッグメニューです」
-->

---

<p class="section-kicker">Public API</p>

## 使い方

<p class="lead"><code>DebugPage</code> を継承して、<code>Configure</code> を override する</p>

<div class="grid-2">
  <div class="mini-card"><h3>DebugPage</h3><p>1ページ分のUI</p></div>
  <div class="mini-card"><h3>Configure</h3><p>中身をC#で組む</p></div>
</div>
<br>

```csharp
public sealed class BattleDebugPage : DebugPage
{
    public override void Configure(IDebugUIBuilder builder)
    {
        builder.Label("Battle");
        builder.IntegerField("Enemy Id", 0, x => enemy.Hp = x);
        builder.Button("Spawn", () => Enemy.Spawn());
    }
}
```

<!--
3:30-4:30
API説明は軽く
DebugPageを継承したクラスを作り、Configureをoverrideする
引数のIDebugUIBuilderでUIを追加していく、というだけ伝える
-->

---

## Builder API

<p class="lead"><code>Label</code> や <code>IntegerField</code> は拡張メソッド</p>

<div class="grid-3">
  <div class="mini-card"><h3>VisualElement</h3><p>概ね何でも追加できる</p></div>
  <div class="mini-card"><h3>Extensions</h3><p>標準UIは薄く呼ぶ</p></div>
  <div class="mini-card"><h3>Foldout</h3><p>中身も Builder で組む</p></div>
</div>
<br>

```csharp
public override void Configure(IDebugUIBuilder builder)
{
    builder.Label("Battle");
    builder.IntegerField("Enemy Id", 0, x => enemy.Hp = x);

    builder.Foldout("Actions", b =>
    {
        b.Label("Admin actions");
        b.Button("Create", () => Actions.Create(););
    });
}
```

<!--
4:30-5:30
Foldoutに渡しているラムダの引数もIDebugUIBuilderになっているのが少し面白いところ
標準コントロールを増やしつつ、必要ならVisualElementをそのまま足せるので、ユーザー拡張性は高い
-->

---

<p class="section-kicker">Feature</p>

## UIの動的追加

<p class="lead">追加時に返る <code>IDisposable</code> を dispose すると、そのUIは消える</p>

<div class="grid-3">
  <div class="mini-card"><h3>AddDebugUI</h3><p>ページ末尾に追加</p></div>
  <div class="mini-card"><h3>IDisposable</h3><p>追加したUIを削除</p></div>
  <div class="mini-card"><h3>AddTo(enemy)</h3><p>寿命に紐づける</p></div>
</div>
<br>

```csharp
var page = DebugMenu.GetPage<BattleDebugPage>();

page.AddDebugUI(builder =>
{
    builder.Slider("Enemy HP", enemy.Hp.Value, 0, enemy.MaxHp, x => enemy.Hp.Value = x);
}).AddTo(enemy);
```

<!--
5:30-6:40
例えば特定の敵のHPを操作するデバッグコマンドを追加して、AddToで敵の寿命に紐づける
デバッグUIは固定メニューだけでは足りないので、ここはかなり大事
-->

---

## EditorWindow

<div class="flow-3">
  <div class="node accent"><strong>Runtime</strong><span>ゲーム画面上で表示</span></div>
  <div class="arrow">&gt;</div>
  <div class="node"><strong>DebugPage</strong><span>同一インスタンスを移動</span></div>
  <div class="arrow">&gt;</div>
  <div class="node green"><strong>EditorWindow</strong><span>Editor側で操作</span></div>
</div>

<div class="callout top-gap">内部的には、Runtime 側から引っこ抜いて EditorWindow に差し替えるような作り</div>

<!--
6:40-8:00
ランタイム側と同じデバッグメニューを、エディタウィンドウとして表示できる
内部実装的には本当に同一インスタンスで、Runtime側から引っこ抜いてEditorWindowにぶっ挿しているような感じ
この後の「EditorWindowは別世界」への伏線にもなる
-->

---

<p class="section-kicker">Motivation</p>

## そもそも、なぜ自作したのか

<div class="grid-2">
  <div class="card"><h3>既存の選択肢はある</h3><p>SRDebugger / UnityDebugSheet など、似たことができるものは存在する</p></div>
  <div class="card"><h3>ただ、不満もある</h3><p>階層、見た目、拡張手順、Editor併用をまとめて揃えたかった</p></div>
</div>

<!--
8:00-8:40
「既存の選択肢はありますただ、いくつか不満がありました」
ここから既存比較へ
-->

---

## 既存ライブラリへの不満

<div class="grid-2">
  <div class="card">
    <h3>SRDebugger</h3>
    <p>階層メニューが作れないと、デバッグコマンドまみれになりがち</p>
  </div>
  <div class="card">
    <h3>UnityDebugSheet</h3>
    <p>かなり良い正直これでいいただ、細かい不満はある</p>
  </div>
</div>

<div class="grid-3 top-gap">
  <div class="mini-card"><h3>見た目</h3><p>ボタンがボタンっぽく見えにくい</p></div>
  <div class="mini-card"><h3>拡張</h3><p>できるが手続きが少し重い</p></div>
  <div class="mini-card"><h3>テーマ</h3><p>見た目だけ変えるにも手を入れたい</p></div>
</div>

<!--
8:40-9:50
UnityDebugSheetはかなりいいそこは認める
ただ、ボタンのデザインや拡張手順の好みが合わなかった
-->

---

## 一方で本パッケージは

<div class="grid-4">
  <div class="mini-card"><h3>UI Toolkit</h3><p>VisualElement をそのまま扱える</p></div>
  <div class="mini-card"><h3>拡張</h3><p>Prefab 登録のような手続きはいらない</p></div>
  <div class="mini-card"><h3>見た目</h3><p>テーマ USS を差し替える</p></div>
  <div class="mini-card"><h3>C# 不要</h3><p>見た目だけならコードを触らない</p></div>
</div>

<div class="quote-box top-gap">
  <p>拡張は C# から、見た目は USS から触れるようにした</p>
</div>

<!--
9:50-10:30
その辺、本パッケージは UI Toolkit を使用している
拡張するにあたって Prefab を登録する必要はないし、見た目を変えたいだけならそもそも C# をいじらなくていい
ここからUI Toolkitの話に入る
-->

---

<p class="section-kicker">UI Toolkit</p>

## 長所と短所

<div class="grid-2">
  <div class="card"><h3 class="ok">良い</h3><p>AIエージェントとの相性はかなり良い</p></div>
  <div class="card"><h3 class="ng">厳しい</h3><p>作り込むほど内部実装との格闘が増える</p></div>
</div>

<!--
10:30-10:50
ここで後半のトーンを切り替える
UIToolkitは正直、一長一短が激しい
-->

---

## 良いところ: AIエージェントとの相性

<div class="grid-3">
  <div class="card"><h3>UXML</h3><p>結局ただのテキストファイル</p></div>
  <div class="card"><h3>USS</h3><p>見た目の差分をテキストで扱える</p></div>
  <div class="card"><h3>Diff</h3><p>変更点をレビューしやすい</p></div>
</div>

<div class="quote-box top-gap">
  <p>UGUI より、AI が高い精度でレイアウトを組みやすい</p>
</div>

<!--
10:50-12:00
今の時代としては何と言ってもAIエージェントとの相性の良さ
UGUIと比べると、UXML/USSはテキストなので、AIが触りやすい
-->

---

## UGUI と比べたときの効率

<div class="grid-3">
  <div class="mini-card"><h3>構造</h3><p>階層をテキストで見られる</p></div>
  <div class="mini-card"><h3>見た目</h3><p>USSだけで調整しやすい</p></div>
  <div class="mini-card"><h3>トークン</h3><p>Prefab操作より軽く済みやすい</p></div>
</div>

<div class="callout top-gap">UniCLI などで UGUI をゴリ押し生成するより、少ないやり取りで形にしやすい</div>

<!--
12:00-13:00
UGUIをAIに触らせる場合、PrefabやSceneの変更が重くなりがち
UIToolkitはUXML/USS/C#のテキストで完結しやすいので、エージェントとの往復が軽い
-->

---

<p class="section-kicker">Issues</p>

## ただ、罠もだいぶ多かった

<div class="grid-4">
  <div class="mini-card"><h3>USS</h3><p>背景色が標準テーマに負ける</p></div>
  <div class="mini-card"><h3>Button</h3><p>Clickable がイベントを止める</p></div>
  <div class="mini-card"><h3>Slider</h3><p>ClampedDragger がイベントを握る</p></div>
  <div class="mini-card"><h3>Hidden</h3><p>初回表示の負荷が出る</p></div>
</div>

<div class="callout danger top-gap">結果から言うと、エンタープライズ開発のランタイムUIに使うのは保守が厳しいと思った</div>

<!--
13:00-13:30
ここから辛口パート
このパッケージを通して、罠もかなり多かった
-->

---

## USS で background-color を制御しきれない

<div class="flow-3">
  <div class="node warn"><strong>:hover</strong><span>標準テーマに<br>上書きされる</span></div>
  <div class="arrow">&gt;</div>
  <div class="node warn"><strong>!important</strong><span>期待通り<br>勝てない</span></div>
  <div class="arrow">&gt;</div>
  <div class="node green"><strong>C#</strong><span>inline style で<br>直接反映</span></div>
</div>

<div class="grid-3 top-gap">
  <div class="mini-card"><h3>やりたいこと</h3><p>hover / press の背景色変更</p></div>
  <div class="mini-card"><h3>色定義</h3><p>USS custom property</p></div>
  <div class="mini-card"><h3>状態反映</h3><p><code>style.backgroundColor</code></p></div>
</div>

<div class="callout top-gap">ここから Button / Slider のイベント問題につながった</div>

<!--
13:30-14:10
最初にやりたかったのは、テーマを少し変えて、hoverやpress中の背景色を変えるだけだった
ただ、USSの:hoverでbackground-colorを書いてもUnityのデフォルトテーマに上書きされる
この用途では!importantも期待通りには勝てなかった
なのでC#からinline styleでbackgroundColorを入れる必要が出てきた
ここから「では押した瞬間や離した瞬間をどう取るのか」という話になり、ButtonとSliderの罠につながる
-->

---

## Button: PointerDown イベントが取れない

<div class="grid-2">
  <div>
    <p class="big-message">標準 <code>Button</code> の <code>Clickable</code> 内部で押下が止まる</p>
  </div>
  <div class="mono-diagram">
Button<br>
└─ Clickable<br>
&nbsp;&nbsp;&nbsp;└─ PointerDownEvent<br>
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;└─ <span class="ng">StopImmediatePropagation()</span>
  </div>
</div>

<div class="grid-3 compact-grid">
  <div class="mini-card"><h3>BubbleUp</h3><p>0 のまま</p></div>
  <div class="mini-card"><h3>TrickleDown</h3><p>増える</p></div>
  <div class="mini-card"><h3>clicked</h3><p>増える</p></div>
</div>

<!--
14:10-14:50
クリックできない話ではないclickedは取れる
原因はButtonのクリック処理を担当しているClickableの内部実装で、PointerDownEventがStopImmediatePropagationされること
C#で押下中の背景色を変えようとしたときに、押した瞬間の状態管理で困った
-->

---

## Button 対策: Clickable を差し替える

<div class="flow-3">
  <div class="node accent"><strong>Press</strong><span>OnPressed</span></div>
  <div class="arrow">&gt;</div>
  <div class="node"><strong>Clickable</strong><span>base 処理へ渡す</span></div>
  <div class="arrow">&gt;</div>
  <div class="node green"><strong>Release</strong><span>OnReleased</span></div>
</div>

```csharp
button.clickable = new InteractiveClickable();

clickable.OnPressed  += ApplyActiveColor;
clickable.OnReleased += RestoreHoverColor;
```

<div class="callout top-gap">普通にイベントを購読するのではなく、標準部品の内部実装に入り込む必要があった</div>

<!--
14:50-15:20
Clickableを継承してProcessDownEvent / ProcessUpEventをフック
このあたりから「驚き最小の原則」と逆方向のつらさが出てくる
-->

---

## Slider: PointerUp イベントが取れない

<div class="grid-2">
  <div>
    <p class="big-message"><code>ClampedDragger</code> により解放を拾えない</p>
    <div class="grid-2 compact-grid">
      <div class="mini-card"><h3>Down</h3><p class="ok">取れる</p></div>
      <div class="mini-card"><h3>Up</h3><p class="ng">取れない</p></div>
    </div>
  </div>
  <div class="mono-diagram">
ScrollView<br>
└─ Slider<br>
&nbsp;&nbsp;&nbsp;└─ ClampedDragger<br>
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;└─ PointerUpEvent<br>
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;└─ <span class="ng">StopImmediatePropagation()</span>
  </div>
</div>

<div class="callout top-gap">値変更ではなく、drag 中の見た目を戻すために必要だった</div>

<!--
15:20-15:55
SliderというよりScrollViewのdraggerで踏んだ話
内部的にはSliderのドラッグ操作を担当するClampedDraggerが絡んでいて、PointerUpを外側で期待通り拾えなかった
-->

---

## Slider 対策: PointerCaptureOut を使う

<div class="flow-3">
  <div class="node accent"><strong>Down</strong><span>親 Slider の<br>TrickleDown</span></div>
  <div class="arrow">&gt;</div>
  <div class="node"><strong>Drag</strong><span>active 色を<br>維持</span></div>
  <div class="arrow">&gt;</div>
  <div class="node green"><strong>Release</strong><span>PointerCaptureOut</span></div>
</div>

```csharp
slider.RegisterCallback<PointerDownEvent>(
    OnDraggerDown, TrickleDown.TrickleDown);

slider.RegisterCallback<PointerCaptureOutEvent>(_ => OnRelease());
dragger.RegisterCallback<PointerCaptureOutEvent>(_ => OnRelease());
```

<div class="callout top-gap">PointerUp そのものではなく、キャプチャ解除を drag 終了として扱った</div>

<!--
15:55-16:25
PointerUpではなくPointerCaptureOutを見る、という迂回になる
キャプチャ元がSlider側かdragger側かに寄るため、両方に登録してisPressedで二重実行を防いだ
-->

---

## 色を変えたいだけなのに...

<div class="flow-3">
  <div class="node"><strong>USS</strong><span>色の定義</span></div>
  <div class="arrow">&gt;</div>
  <div class="node warn"><strong>C#</strong><span>状態管理と<br>inline style</span></div>
  <div class="arrow">&gt;</div>
  <div class="node warn"><strong>内部実装</strong><span>Clickable /<br>ClampedDragger</span></div>
</div>
<br>
<p class="big-message">やりたいことは背景色変更だけなのに、イベント伝播と標準部品の内部実装まで見ることになった</p>
<br>
<div class="callout danger top-gap">テーマ調整が USS で完結できず、結局 C# が必要</div>

<!--
16:25-17:00
ここは総括
USSで色を定義するところまでは自然だった
ただ、状態管理と反映はC#側になり、ButtonではClickable、SliderではClampedDraggerの内部事情まで見ることになった
細かいところでは、C#から読む--hover-colorや--active-colorは:rootではなく対象要素に直接マッチするセレクタへ置く必要もあった
テーマをちょっと変えたいだけなのに、必要な知識がかなり低レイヤーまで降りてしまった、という話
-->

---

## 最適化: 初回表示のスパイクを抑える

<div class="flow-3">
  <div class="node"><strong>期待</strong><span>非表示中の処理を<br>止めたい</span></div>
  <div class="arrow">&gt;</div>
  <div class="node warn"><strong>実際</strong><span>表示時に処理が<br>寄ることがある</span></div>
  <div class="arrow">&gt;</div>
  <div class="node accent"><strong>採用</strong><span>画面外 translate +<br>事前アタッチ</span></div>
</div>

```csharp
style.translate =
    new StyleTranslate(
        new Translate(-5000, -5000));

style.opacity = 0f;
```

<div class="callout warn top-gap">公式の hide 方法のトレードオフを踏まえ、この用途では表示時の体感を優先した</div>

<!--
17:00-18:30
公式がhide方法のトレードオフやDynamicTransformを説明している
display:noneはレンダリングやレイアウトを止められるので一般には効率が良い
ただ、このデバッグメニューでは「開いた瞬間に重い」ほうがユーザー体験として困る
なので画面外translateと事前アタッチを選んだ、という話にする
参考: Unity Manual Optimizing performance / UsageHints
-->

---

## 結言

<div class="grid-2">
  <div class="card"><h3 class="ok">UI Toolkit × AI</h3><p>UXML / USS はテキストなので、エージェントとの往復が軽い。個人開発やツール UI なら積極的に使える</p></div>
  <div class="card"><h3 class="ng">内部実装との格闘</h3><p>Clickable・ClampedDragger・標準テーマの上書き。驚き最小の原則の逆を行く罠が多い</p></div>
</div>

<div class="grid-3 top-gap">
  <div class="mini-card"><h3>個人開発</h3><p>トラブルを AI に丸投げできるならアリ</p></div>
  <div class="mini-card"><h3>ツール UI</h3><p>用途を絞れば強いテキスト資産</p></div>
  <div class="mini-card"><h3>大規模 Runtime</h3><p>チーム保守前提では慎重に</p></div>
</div>

<!--
18:30-20:00
作ったのは階層式デバッグメニューで、こだわりは動的追加と EditorWindow 版
UI ToolkitはAIエージェントと相性が良い一方、標準部品の内部実装との格闘が多かった
個人開発でAIにトラブル対応を丸投げするならアリ、エンタープライズのランタイムUIとしては保守が厳しい、という締め
-->
