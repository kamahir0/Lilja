---
name: unity-uxml-to-ugui-prefab
description: >-
  Use when asked to convert, translate, or reproduce a UI Toolkit .uxml file
  into a UGUI Prefab using UniCli in projects where Uxml.ToUgui command is available.
metadata:
  version: "2.0.0"
---

# Skill: UXML → UGUI Prefab 自動変換ガイド

UI Toolkit の `.uxml` / `.uss` ファイルから、`unicli` カスタムコマンド `Uxml.ToUgui` を用いて、エディタ上で UGUI ベースの Prefab を自動生成するスキル。

---

## 1. 動作前提条件
* 対象 Unity プロジェクトで `unicli` が有効であること。
* `unicli exec Uxml.ToUgui --help` が正常にヘルプを返すこと。

---

## 2. 実行ワークフロー

### Step 1: 変換パラメータの決定
UXMLを変換するにあたり、ターゲットのフォント倍率を決定する。
* **`fontScale` (フォントスケール) の決定**:
  * ユーザーのプロンプト内にターゲット解像度や倍率の明示的な指定が無い場合は、タスクを一時停止し、`ask_question` ツール（またはチャット）を用いてユーザーに解像度・倍率を選択させる。
    * 選択肢の例：
      * 「1920x1080 (高解像度 UI 用、フォント倍率 2.8)」
      * 「1280x720 (標準解像度 UI 用、フォント倍率 1.8)」
      * 「等倍 (倍率 1.0)」
      * 「その他 (自由に入力)」
  * ユーザーが決定したフォント倍率を `--fontScale` パラメータに使用する。

### Step 2: 変換コマンドの実行
`unicli exec Uxml.ToUgui` を使用して変換を実行する。

```bash
unicli exec Uxml.ToUgui --uxmlPath "<UXML相対パス>" --outputPath "<出力Prefab相対パス>" --fontScale <算出スケール> --json
```
* ※ `--outputPath` が未指定の場合、自動的に UXML と同じ階層に同名 `.prefab` で出力される。

### Step 3: レスポンスの解析
実行結果の JSON レスポンスから以下の統計情報を確認する。
* `success`: `true` であること。
* `nodeCount`: 変換されたオブジェクトの総数。
* `warnCount`: 警告数。
* `todoCount`: 手動調整が必要な箇所の数。

### Step 4: 警告・TODO箇所の解決
`todoCount > 0` の場合、Unity の `Console.GetLog` を実行して、どのオブジェクトで警告が出たか詳細を確認する。
```bash
unicli exec Console.GetLog "{\"logType\":\"Warning\"}" --json
```
* **一般的な警告と対策**:
  * `justify-content: SpaceBetween / SpaceAround` 警告:
    * UxmlToUguiConverter はこれらを `FlexStart` で近似し、該当コンテナ直下に `[TODO] justify-content...` というダミーオブジェクトを生成する。
    * 必要に応じて、変換後の Prefab を開き、親の `Horizontal/Vertical Layout Group` のパラメータやアンカーを手動調整、またはスクリプトで修正してダミーの `[TODO]` オブジェクトを削除する。

---

## 3. 変換後の UGUI 階層構造（L/G分離設計）ルール

本コンバーターは、**レイアウト制御層(Layer-L)** と **グラフィック実体層(Layer-G)** を分離して構築する。

```text
[Layout] Parent (LayoutElement, VerticalLayoutGroup 等)  <-- サイズ・整列を支配
  └── Graphic Child (Image, Button, TMP 等)             <-- 色・見た目、当たり判定を支配 (Stretch-Stretch 0,0,0,0)
```

### 修正時の重要ルール:
* 変換後の階層をエディタスクリプト等で微調整する際、**`[Layout]` と名のつく親オブジェクトに直接 `Image` や `TextMeshProUGUI` などの描画コンポーネントを追加してはならない**（レイアウトグループによるストレッチ強制とサイズ競合が発生するため）。
* 見た目の変更や装飾は、必ず子階層の `Graphic Child` 側に対して行うこと。
* 親が `LayoutGroup` を持たない特殊エリア（`UIRoot` 直下や `[Overlay]` コンテナ直下）の子要素は、サイズ駆動漏れを防ぐため `Stretch-Stretch (0,0,0,0)` でストレッチされていることを確認する。
