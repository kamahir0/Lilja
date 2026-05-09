# UI Toolkit を触ってきた感想: ランタイム UI 基盤としては慎重に見たい

## スライド構成案

| ページ数 | スライド文章 | 必要なら貼るべき画像の説明 | 補足（私用） |
| --- | --- | --- | --- |
| 1 | # UI Toolkit を触ってきた感想<br><br>ランタイム UI 基盤としては慎重に見たい | DebugMenu のランタイム画面、または EditorWindow 画面のスクリーンショット | 最初に「UI Toolkit を否定する話ではない」と言う。実装してみて、どこにコストが出たかを共有する会。 |
| 2 | ## 今日の話<br><br>- Debug UI パッケージを作った<br>- 便利だったところもある<br>- ただしハマりどころが多かった<br>- エンタープライズ的なランタイム UI 基盤として見ると慎重になりたい | Runtime / Editor / USS / docs の関係を簡単な箱図で示す | 「エンタープライズ的」は、大量画面、長期保守、テーマ変更、品質保証、複数人開発を想定している、と定義する。 |
| 3 | ## 作ったもの<br><br>- Runtime で開くデバッグメニュー<br>- `DebugPage` を Builder API で構築<br>- 同じページを EditorWindow からも閲覧<br>- USS で Runtime / Editor の見た目を切り替え | DebugMenu の構成図。`DebugMenu.Initialize` → `DebugMenuWindow` → `DebugPage` → Controls | ここは成果物紹介。まず「ちゃんと作れた」ことを伝えてから問題点に入る。 |
| 4 | ## 期待していたこと<br><br>- Unity 標準の UI システムに乗れる<br>- USS で見た目を整理できる<br>- Editor 拡張と近い書き味で作れる<br>- uGUI より構造化しやすそう | Web の CSS / UI Toolkit USS / Unity C# の対応関係を軽く図示 | UI Toolkit の良さも認める。VisualElement ツリー、USS、Builder API は悪くない。 |
| 5 | ## 実際に採用した USS 設計<br><br>- `t-`: theme<br>- `l-`: layout<br>- `c-`: component<br>- `u-`: utility<br>- Design Token を `:root` に集約 | `DebugMenu.uss` の冒頭と `DebugMenuDefaultTheme.uss` の変数部分を抜粋 | この設計自体は Web 的にも普通。問題は、この設計だけでは収まらないところが多かったこと。 |
| 6 | ## Runtime / Editor でテーマを分ける<br><br>- Runtime は大きめのタッチ UI<br>- EditorWindow は小さめのツール UI<br>- 同じ `c-button`, `c-input` を使い、変数で差し替える | Runtime 画面と EditorWindow 画面を並べる | ここは「設計としては綺麗にしたかった」ポイント。コンポーネントとテーマを分離した。 |
| 7 | ## ここまではよい<br><br>- コンポーネントクラスを C# 側で付与<br>- USS 側で見た目を定義<br>- テーマ差し替えも可能<br>- Builder API でページも作りやすい | `DebugButton` が `c-control-size`, `c-button`, `c-button--primary` を付けるコード抜粋 | 一度ポジティブにまとめる。ここから「しかし」に入る。 |
| 8 | ## ハマりどころ 1: Button の hover / active<br><br>- USS の `:hover` だけでは背景色が安定しない<br>- Unity 標準テーマが `background-color` を上書きする<br>- `!important` でも期待通りにならない場面がある | `button-interaction-styles.md` の表、または該当コードの抜粋 | Web でもフレームワーク上書きはあるが、ここでは Unity UI Toolkit の挙動に強く依存している。 |
| 9 | ## C# で背景色を直接制御した<br><br>- `--hover-color` / `--active-color` を USS に定義<br>- C# で `ICustomStyle` から読む<br>- `style.backgroundColor` を inline style として当てる | `ButtonInteractionHelper.Register` のコード抜粋 | 「見た目の話なのに C# 実装が必要になった」ことを強調する。責務の分離が崩れやすい。 |
| 10 | ## ハマりどころ 2: Clickable がイベントを止める<br><br>- `Button` 内部の `Clickable` が `StopImmediatePropagation()` する<br>- 通常の `PointerDownEvent` では押下を拾えない<br>- `Clickable` を差し替えて press / release をフックした | `InteractiveClickable` のコード抜粋 | ここはインパクトがある。普通に RegisterCallback すればよい、という直感が外れる。 |
| 11 | ## ハマりどころ 3: ScrollView のスクロールバー<br><br>- ScrollView 内部の `Scroller` / `Slider` / `#unity-dragger` に触る必要があった<br>- ドラッガー押下は親 Slider の TrickleDown で拾う<br>- 解放は `PointerCaptureOutEvent` を使う | ScrollView の内部構造図。`Scroller` → `Slider` → `unity-dragger` / `unity-tracker` | Web のネイティブ scrollbar は見た目だけ触ることが多い。UI Toolkit は内部要素に触れる分、自由だが壊れやすい。 |
| 12 | ## ハマりどころ 4: 非表示とパフォーマンス<br><br>- `display: none` はレイアウト・スタイル・フォント生成をスキップする<br>- 初回表示や初回遷移で処理が集中する<br>- 画面外へ `translate(-5000, -5000)` する方式にした | `display: none` と translate 退避の比較図 | `uitoolkit-performance-hidden-elements.md` の内容。普通の UI 表示制御にもパフォーマンス罠がある。 |
| 13 | ## ハマりどころ 5: pickingMode の直感差<br><br>- `pickingMode = Ignore` は子に伝播しない<br>- 親を Ignore にしても子が hit-test される場合がある<br>- 非表示 UI が入力をブロックしないように工夫が必要 | Pick 処理のツリー図。親 Ignore、子 Position がヒットする例 | 「透明にして Ignore にすればよい」が成立しない。ランタイム UI では入力事故に直結する。 |
| 14 | ## ハマりどころ 6: EditorWindow との差分<br><br>- Runtime の PanelSettings と EditorWindow は別世界<br>- `:root` の変数がそのまま使えない<br>- Editor 用 USS が大きくなった<br>- InputField / EnumField / BoundsField への上書きが増えた | `DebugMenuEditorTheme.uss` の長いセレクタ群を一部抜粋 | 「Editor でも同じページを表示したい」という要求が特殊なのは認める。ただし社内ツールでは Runtime / Editor 両対応はありがち。 |
| 15 | ## 結果: USS はこうなった<br><br>- Design Token 層<br>- Component CSS 層<br>- Editor 用 override 層<br>- C# inline style 層<br>- Unity 内部構造への依存層 | レイヤー図。下から Unity default theme、package theme、component USS、Editor override、C# inline style | ここで「ややこしいが、やむを得ず積み上がった」と言う。設計ミスだけではない。 |
| 16 | ## これは Web 開発でも起きる?<br><br>- 既存 UI フレームワークの上書きは Web でもある<br>- Bootstrap / MUI / Ant Design などでも似た問題はある<br>- ただし Web には theme API や headless UI という逃げ道も多い | Web UI framework と UI Toolkit の比較表 | UI Toolkit だけが悪い、という話にはしない。既存テーマに乗る以上、上書き問題は一般に起こる。 |
| 17 | ## UI Toolkit で特に重いと感じた点<br><br>- 標準テーマの優先度が読みにくい<br>- 内部要素への依存が増えやすい<br>- USS と C# の責務境界が崩れやすい<br>- Runtime / Editor / PanelSettings の差分が大きい<br>- バージョン差分への不安が残る | チェックリスト風のスライド | ここが主張の中心。感情論ではなく、保守コストの話にする。 |
| 18 | ## エンタープライズ的ランタイム UI で怖いこと<br><br>- 画面数が増える<br>- 複数人が触る<br>- テーマ変更が入る<br>- 長期保守が必要<br>- 入力・フォーカス・アクセシビリティの品質が求められる<br>- Unity バージョン更新の影響を受ける | 「小規模デバッグ UI」から「大規模業務 UI」へ広がる図 | ここで「デバッグ UI だから許せる複雑さ」と「業務 UI で許容しづらい複雑さ」を分ける。 |
| 19 | ## だから、結論<br><br>UI Toolkit は便利。<br>ただしランタイム UI の基盤として大規模・長期運用するなら、現時点では慎重に評価したい。 | 強調用のシンプルな文字だけのスライド | 「使うな」ではなく「検証なしに標準採用するのは危険」という言い方にする。 |
| 20 | ## 使うなら必要そうなルール<br><br>- Design Token を先に決める<br>- Unity 内部クラスへの依存を局所化する<br>- コンポーネントごとに wrapper を作る<br>- Runtime / Editor のテーマ差分を明示する<br>- パフォーマンス検証を最初から入れる<br>- Unity バージョン更新時の確認項目を持つ | 採用チェックリスト | 建設的な着地。社内で「じゃあどうするか」の話につなげる。 |
| 21 | ## 向いていそうな用途<br><br>- Editor 拡張<br>- 小〜中規模の社内ツール<br>- デバッグ UI<br>- 構造化された設定画面<br>- 見た目の自由度がそこまで高くない UI | 適性マトリクス。縦軸: 規模、横軸: 見た目自由度 | 完全否定を避ける。Debug UI には十分使える。 |
| 22 | ## 慎重に見たい用途<br><br>- 大量の画面を持つ Runtime UI<br>- 高いブランド再現性が必要な UI<br>- 複雑な入力・フォーカス制御が必要な UI<br>- 長期運用する基盤 UI<br>- 複数プロダクトで共有するデザインシステム | リスクマトリクス。赤・黄・緑で用途を分類 | 「エンタープライズ開発でのランタイム UI に厳しい」の主張はここで出す。 |
| 23 | ## 今回の学び<br><br>- USS 設計自体は Web 的に整理できる<br>- しかし UI Toolkit 固有の workaround が多い<br>- 見た目の調整が C# 実装に漏れやすい<br>- 小さく始める分にはよいが、基盤化には覚悟がいる | `docs/` の 3 ファイルを並べた画像 | docs が増えたこと自体を「知見が溜まった」と見る。ハマりどころがドキュメント化される程度には多かった。 |
| 24 | ## まとめ<br><br>UI Toolkit は使える。<br>でも、ランタイム UI の標準基盤として採用するなら、<br>「作れるか」ではなく「長く保守できるか」で評価したい。 | なし、または DebugMenu の完成画面 | 最後は柔らかく。実装できたからこそ見えた不安、という語りにする。 |

## 追加で用意するとよい素材

- Runtime DebugMenu のスクリーンショット
- Debug Menu Inspector のスクリーンショット
- `DebugMenu.uss` の `t- / l- / c- / u-` セクション抜粋
- `DebugMenuDefaultTheme.uss` の design token 抜粋
- `DebugMenuEditorTheme.uss` の長い override セレクタ抜粋
- `ButtonInteractionHelper` / `InteractiveClickable` のコード抜粋
- ScrollView 内部構造の簡易図
- `display: none` と translate 退避の比較図
- Runtime / Editor 所有権移譲の簡易図

## 口頭での注意点

- UI Toolkit 全否定に聞こえないようにする。
- 「今回の Debug UI は特殊な要求も含んでいる」と先に認める。
- その上で、特殊要求に対応しようとしたときの escape hatch が少なく、内部挙動への依存が増えたことを論点にする。
- エンタープライズ開発という言葉は「長期保守、複数人開発、品質保証、テーマ変更、Unity 更新耐性」と具体化する。
- 結論は「使えない」ではなく「標準基盤にするなら慎重に検証すべき」にする。
