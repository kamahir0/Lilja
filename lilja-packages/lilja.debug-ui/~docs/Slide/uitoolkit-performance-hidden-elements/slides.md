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
自己紹介。
今日は「UI Toolkit入門」ではなく、デバッグメニューを実際に作ってみて、どこが良かったか、どこが厳しかったかを話す。
前半は作ったものの紹介、後半はUI Toolkitのかなり正直な感想。
-->

---

## 今日の話

<div class="grid-3">
  <div class="mini-card"><h3>1. 実演</h3><p>まず作ったものを触る。</p></div>
  <div class="mini-card"><h3>2. 使い方</h3><p>DebugPage / Configure / Builder。</p></div>
  <div class="mini-card"><h3>3. こだわり</h3><p>動的追加と EditorWindow。</p></div>
  <div class="mini-card"><h3>4. 自作理由</h3><p>既存ライブラリへの不満。</p></div>
  <div class="mini-card"><h3>5. UI Toolkit</h3><p>AI相性と実装上の罠。</p></div>
  <div class="mini-card"><h3>6. 結論</h3><p>良い。ただし保守は厳しい。</p></div>
</div>

<div class="callout top-gap">発表の主役は「パッケージ自体」よりも、作った結果見えた UI Toolkit の向き不向き。</div>

<!--
今日の地図。
前半は軽めに紹介、後半は実装して分かったUI Toolkitの評価へ寄せる。
-->

---

## 実演: まず触ってみる

<div class="flow">
  <div class="card"><h3>3回タップ</h3><p>ランタイムでメニューを開く。</p></div>
  <div class="card"><h3>階層メニュー</h3><p>ページ遷移、戻る、Root。</p></div>
  <div class="card"><h3>操作UI</h3><p>入力、ボタン、Slider。</p></div>
  <div class="card"><h3>動的追加</h3><p>実行中にUIを足す。</p></div>
  <div class="card"><h3>EditorWindow</h3><p>同じページをEditorで触る。</p></div>
</div>

<!--
0:40-3:30
ライブデモ。
3回タップで出る、階層メニューになっている、動的にUI追加できる、EditorWindow版がある、を順番に見せる。
細かい実装説明はしない。
失敗した場合は予備のRuntime/EditorWindow/動的追加スライドへ飛ぶ。
-->

---

## こんなものを作りました

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
デモで見せたものを一言で整理する。
「何を作ったのかと言うと、階層式のデバッグメニューです」。
-->

---

<p class="section-kicker">Public API</p>

## 軽く使い方

<p class="lead"><code>DebugPage</code> を継承して、<code>Configure</code> を override する。</p>

<div class="grid-3">
  <div class="mini-card"><h3>DebugPage</h3><p>1ページ分のUI。</p></div>
  <div class="mini-card"><h3>Configure</h3><p>中身をC#で組む。</p></div>
  <div class="mini-card"><h3>IDebugUIBuilder</h3><p>VisualElementを追加する入口。</p></div>
</div>

```csharp
public sealed class BattleDebugPage : DebugPage
{
    public override void Configure(IDebugUIBuilder builder)
    {
        builder.Label("Battle");
        builder.IntegerField("Enemy Id");
        builder.PrimaryButton("Spawn", Spawn);
    }
}
```

<!--
3:30-4:30
API説明は軽く。
DebugPageを継承したクラスを作り、Configureをoverrideする。
引数のIDebugUIBuilderでUIを追加していく、というだけ伝える。
-->

---

## Builder API

<p class="lead"><code>Label</code> や <code>IntegerField</code> は拡張メソッド。</p>

<div class="grid-3">
  <div class="mini-card"><h3>VisualElement</h3><p>概ね何でも追加できる。</p></div>
  <div class="mini-card"><h3>Extensions</h3><p>標準UIは薄く呼ぶ。</p></div>
  <div class="mini-card"><h3>Foldout</h3><p>中身も Builder で組む。</p></div>
</div>

```csharp
builder.Foldout("Actions", b =>
{
    b.Label("Admin actions");
    b.PrimaryButton("Create", Create);
    b.DangerButton("Delete", Delete);
});
```

<!--
4:30-5:30
Foldoutに渡しているラムダの引数もIDebugUIBuilderになっているのが少し面白いところ。
標準コントロールを増やしつつ、必要ならVisualElementをそのまま足せるので、ユーザー拡張性は高い。
-->

---

<p class="section-kicker">Feature</p>

## こだわり1: 実行中にUIを追加できる

<p class="lead">追加時に返る <code>IDisposable</code> を dispose すると、そのUIは消える。</p>

<div class="grid-3">
  <div class="mini-card"><h3>AddDebugUI</h3><p>ページ末尾に追加。</p></div>
  <div class="mini-card"><h3>IDisposable</h3><p>追加したUIを削除。</p></div>
  <div class="mini-card"><h3>AddTo(enemy)</h3><p>敵の寿命に紐づけ。</p></div>
</div>

```csharp
var page = DebugMenu.GetPage<BattleDebugPage>();

page.AddDebugUI(ui =>
{
    ui.Slider("Enemy HP", enemy.Hp.Value, 0, enemy.MaxHp,
        value => enemy.Hp.Value = value);
}).AddTo(enemy);
```

<!--
5:30-6:40
例えば特定の敵のHPを操作するデバッグコマンドを追加して、AddToで敵の寿命に紐づける。
デバッグUIは固定メニューだけでは足りないので、ここはかなり大事。
-->

---

## こだわり2: EditorWindow 版がある

<div class="flow-3">
  <div class="node accent"><strong>Runtime</strong><span>ゲーム画面上で表示</span></div>
  <div class="arrow">&gt;</div>
  <div class="node"><strong>DebugPage</strong><span>同一インスタンスを移動</span></div>
  <div class="arrow">&gt;</div>
  <div class="node green"><strong>EditorWindow</strong><span>Editor側で操作</span></div>
</div>

<div class="callout top-gap">内部的には、Runtime 側から引っこ抜いて EditorWindow に差し替えるような作り。</div>

<!--
6:40-8:00
ランタイム側と同じデバッグメニューを、エディタウィンドウとして表示できる。
内部実装的には本当に同一インスタンスで、Runtime側から引っこ抜いてEditorWindowにぶっ挿しているような感じ。
この後の「EditorWindowは別世界」への伏線にもなる。
-->

---

<p class="section-kicker">Motivation</p>

## そもそも、なぜ自作したのか

<div class="grid-2">
  <div class="card"><h3>既存の選択肢はある</h3><p>SRDebugger / UnityDebugSheet など、似たことができるものは存在する。</p></div>
  <div class="card"><h3>ただ、不満もある</h3><p>階層、見た目、拡張手順、Editor併用をまとめて揃えたかった。</p></div>
</div>

<!--
8:00-8:40
「既存の選択肢はあります。ただ、いくつか不満がありました」。
ここから既存比較へ。
-->

---

## 既存ライブラリへの不満

<div class="grid-2">
  <div class="card">
    <h3>SRDebugger</h3>
    <p>階層メニューが作れないと、デバッグコマンドまみれになりがち。</p>
  </div>
  <div class="card">
    <h3>UnityDebugSheet</h3>
    <p>かなり良い。正直これでいい。ただ、細かい不満はある。</p>
  </div>
</div>

<div class="grid-3 top-gap">
  <div class="mini-card"><h3>見た目</h3><p>ボタンがボタンっぽく見えにくい。</p></div>
  <div class="mini-card"><h3>拡張</h3><p>できるが手続きが少し重い。</p></div>
  <div class="mini-card"><h3>テーマ</h3><p>見た目だけ変えるにも手を入れたい。</p></div>
</div>

<!--
8:40-9:50
UnityDebugSheetはかなりいい。そこは認める。
ただ、ボタンのデザインや拡張手順の好みが合わなかった。
-->

---

## 一方で本パッケージは

<div class="grid-4">
  <div class="mini-card"><h3>UI Toolkit</h3><p>VisualElement をそのまま扱える。</p></div>
  <div class="mini-card"><h3>拡張</h3><p>Prefab 登録のような手続きはいらない。</p></div>
  <div class="mini-card"><h3>見た目</h3><p>テーマ USS を差し替える。</p></div>
  <div class="mini-card"><h3>C# 不要</h3><p>見た目だけならコードを触らない。</p></div>
</div>

<div class="quote-box top-gap">
  <p>拡張は C# から、見た目は USS から触れるようにした。</p>
</div>

<!--
9:50-10:30
その辺、本パッケージは UI Toolkit を使用している。
拡張するにあたって Prefab を登録する必要はないし、見た目を変えたいだけならそもそも C# をいじらなくていい。
ここからUI Toolkitの話に入る。
-->

---

<p class="section-kicker">UI Toolkit</p>

## 正直、一長一短が激しい

<div class="grid-2">
  <div class="card"><h3 class="ok">良い</h3><p>AIエージェントとの相性はかなり良い。</p></div>
  <div class="card"><h3 class="ng">厳しい</h3><p>作り込むほど内部実装との格闘が増える。</p></div>
</div>

<div class="callout warn top-gap">今回は「良かった点」と「これは厳しい点」を分けて話す。</div>

<!--
10:30-10:50
ここで後半のトーンを切り替える。
UIToolkitは正直、一長一短が激しい。
-->

---

## 良いところ: AIエージェントとの相性

<div class="grid-3">
  <div class="card"><h3>UXML</h3><p>結局ただのテキストファイル。</p></div>
  <div class="card"><h3>USS</h3><p>見た目の差分をテキストで扱える。</p></div>
  <div class="card"><h3>Diff</h3><p>変更点をレビューしやすい。</p></div>
</div>

<div class="quote-box top-gap">
  <p>UGUI より、AI が高い精度でレイアウトを組みやすい。</p>
</div>

<!--
10:50-12:00
今の時代としては何と言ってもAIエージェントとの相性の良さ。
UGUIと比べると、UXML/USSはテキストなので、AIが触りやすい。
-->

---

## UGUI と比べたときの効率

<div class="grid-3">
  <div class="mini-card"><h3>構造</h3><p>階層をテキストで見られる。</p></div>
  <div class="mini-card"><h3>見た目</h3><p>USSだけで調整しやすい。</p></div>
  <div class="mini-card"><h3>トークン</h3><p>Prefab操作より軽く済みやすい。</p></div>
</div>

<div class="callout top-gap">UniCLI などで UGUI をゴリ押し生成するより、少ないやり取りで形にしやすい。</div>

<!--
12:00-13:00
UGUIをAIに触らせる場合、PrefabやSceneの変更が重くなりがち。
UIToolkitはUXML/USS/C#のテキストで完結しやすいので、エージェントとの往復が軽い。
-->

---

<p class="section-kicker">Issues</p>

## ただ、罠もだいぶ多かった

<div class="grid-4">
  <div class="mini-card"><h3>Button</h3><p>PointerDown が届かない。</p></div>
  <div class="mini-card"><h3>Clickable</h3><p>差し替えが必要。</p></div>
  <div class="mini-card"><h3>Slider</h3><p>PointerUp が取れない。</p></div>
  <div class="mini-card"><h3>USS</h3><p>結局C#補助が必要。</p></div>
</div>

<div class="callout danger top-gap">結果から言うと、エンタープライズ開発のランタイムUIに使うのは保守が厳しいと思った。</div>

<!--
13:00-13:30
ここから辛口パート。
このパッケージを通して、罠もかなり多かった。
-->

---

## Button: PointerDown が届かない

<div class="grid-2">
  <div>
    <p class="big-message">標準 <code>Button</code> の BubbleUp では押下を拾えない。</p>
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
13:30-14:30
クリックできない話ではない。clickedは取れる。
押している間の色を変えるなど、押下中の状態管理をしたいときに困る。
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

<div class="callout top-gap">普通にイベントを購読するのではなく、標準部品の内部実装に入り込む必要があった。</div>

<!--
14:30-15:10
Clickableを継承してProcessDownEvent / ProcessUpEventをフック。
このあたりから「驚き最小の原則」と逆方向のつらさが出てくる。
-->

---

## Slider: PointerUp が release にならない

<div class="grid-2">
  <div>
    <p class="lead"><code>ScrollView</code> の dragger で、押下は取れるが解放が取れない。</p>
    <div class="grid-2 compact-grid">
      <div class="mini-card"><h3>Down</h3><p class="ok">取れる</p></div>
      <div class="mini-card"><h3>Up</h3><p class="ng">取れない</p></div>
    </div>
  </div>
  <div class="diagram">
    <h3>採用したシグナル</h3>
    <div class="node green">
      <strong>PointerCaptureOut</strong>
      <span>キャプチャ解放を release として扱う</span>
    </div>
  </div>
</div>

<div class="callout top-gap">値変更ではなく、drag 中の見た目を戻すために必要だった。</div>

<!--
15:10-16:00
SliderというよりScrollViewのdraggerで踏んだ話。
PointerUpではなくPointerCaptureOutを見る、という迂回になる。
-->

---

## USS: 結局、見た目だけでは完結しない

<div class="flow-3">
  <div class="node warn"><strong>:hover</strong><span>背景色が<br>上書きされる</span></div>
  <div class="arrow">&gt;</div>
  <div class="node warn"><strong>!important</strong><span>期待通り<br>勝てない</span></div>
  <div class="arrow">&gt;</div>
  <div class="node green"><strong>C#</strong><span>inline style で<br>状態反映</span></div>
</div>

<div class="split-banner top-gap">
  <div class="banner">
    <h3 class="ng">NG</h3>
<pre><code>:root {
  --hover-color: #eee;
}</code></pre>
    <p>C# の <code>TryGetValue</code> では読めない。</p>
  </div>
  <div class="banner">
    <h3 class="ok">OK</h3>
<pre><code>.c-button--primary {
  --hover-color: #eee;
}</code></pre>
    <p>対象要素に直接マッチさせる。</p>
  </div>
</div>

<!--
16:00-17:00
見た目はUSS、状態遷移はC#、という分担になった。
CSSっぽく見えるが、CSSの期待値で触ると外れるところがある。
-->

---

## 非表示最適化: 開いた瞬間を軽くする

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

<div class="callout warn top-gap">公式の hide 方法のトレードオフを踏まえ、この用途では表示時の体感を優先した。</div>

<!--
17:00-18:30
公式がhide方法のトレードオフやDynamicTransformを説明している。
display:noneはレンダリングやレイアウトを止められるので一般には効率が良い。
ただ、このデバッグメニューでは「開いた瞬間に重い」ほうがユーザー体験として困る。
なので画面外translateと事前アタッチを選んだ、という話にする。
参考: Unity Manual Optimizing performance / UsageHints。
-->

---

## まとめ

<div class="grid-4">
  <div class="mini-card"><h3>作ったもの</h3><p>階層式のデバッグメニュー。</p></div>
  <div class="mini-card"><h3>こだわり</h3><p>動的追加と EditorWindow 版。</p></div>
  <div class="mini-card"><h3>良かった点</h3><p>UI Toolkit は AI と相性が良い。</p></div>
  <div class="mini-card"><h3>つらかった点</h3><p>標準部品の内部実装との格闘。</p></div>
</div>

<div class="quote-box top-gap">
  <p>作る体験は良い。ただし、運用するには罠を吸収する設計が必要。</p>
</div>

<!--
18:30-19:20
今日話したことを一度回収する。
作ったのは階層式デバッグメニュー。こだわりは動的追加とEditorWindow版。
UI ToolkitはAIエージェントと相性が良い一方、標準部品の内部実装との格闘が多かった。
この整理を置いてから、最後の結論で採用判断の話に入る。
-->

---

## 結論

<div class="grid-3">
  <div class="card"><h3>個人開発</h3><p>AIエージェントにトラブル対応まで丸投げとかならアリかも</p></div>
  <div class="card"><h3>ツールUI</h3><p>用途を絞れば強い。テキスト資産として扱える。</p></div>
  <div class="card"><h3>大規模Runtime</h3><p>保守前提だとかなり慎重に見たい。</p></div>
</div>

<div class="callout danger top-gap">便利ではある。ただし、チーム開発のランタイムUI基盤として採用するには覚悟がいる。</div>

<!--
19:20-20:00
最後の結論。
個人開発で、UI ToolkitのトラブルはAIエージェント+上位モデルに任せる、ならアリかもしれない。
エンタープライズのランタイムUIとしては、保守が厳しいという締め。
-->

---

## 予備: Runtime 表示

<div class="grid-2">
  <div>
    <p class="lead">ゲーム画面上で、その場の状態を見ながら操作する。</p>
    <div class="stack">
      <div class="stack-row accent">実機に近い確認</div>
      <div class="stack-row">タッチ操作</div>
      <div class="stack-row">マウスホイール操作</div>
    </div>
  </div>
  <figure class="image-frame">
    <img src="./assets/images/runtime-menu.png" alt="Runtime debug menu screenshot">
  </figure>
</div>

<!--
ライブデモ失敗時の予備。
Runtimeでメニューが出ること、ページ遷移やコントロールを触れることをこの画像で説明する。
-->

---

## 予備: EditorWindow 表示

<div class="grid-2">
  <div>
    <p class="lead">ゲーム画面を隠さず、Unity Editor 側で同じメニューを触る。</p>
    <div class="stack">
      <div class="stack-row accent">画面を占有しない</div>
      <div class="stack-row">同じ DebugPage</div>
      <div class="stack-row">Editor 作業と併用</div>
    </div>
  </div>
  <figure class="image-frame">
    <img src="./assets/images/editor-window.png" alt="EditorWindow debug menu screenshot">
  </figure>
</div>

<!--
ライブデモ失敗時の予備。
同じDebugPageをEditor側へ借用できることを説明する。
-->

---
class: dynamic-ui-slide
---

## 予備: 動的にUIを追加する

<div class="grid-3">
  <div class="mini-card"><h3>AddDebugUI</h3><p>ページ末尾にUIを後から追加。</p></div>
  <div class="mini-card"><h3>PlaceBehind</h3><p>既存UIの前後に差し込む。</p></div>
  <div class="mini-card"><h3>Dispose</h3><p>追加したUIを削除する。</p></div>
</div>

```csharp
var page = DebugMenu.GetPage<BattleDebugPage>();

page.AddDebugUI(ui =>
{
    var slider = ui.Slider(
        "Enemy HP", enemy.Hp.Value, 0, enemy.MaxHp,
        value => enemy.Hp.Value = value);

    enemy.Hp.Subscribe(hp => slider.value = hp)
        .AddTo(enemy);
}).AddTo(enemy);
```

<!--
ライブデモ失敗時、または質疑で動的追加の詳細を聞かれたときの予備。
本編ではここまで読まない。
-->
