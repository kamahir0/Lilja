# Custom Project Window Settings JSON リファレンス

実装の正: [CustomProjectTreeModel.cs](file:///c:/Users/hrtkm/develop/unity/lilja/lilja-packages/lilja.editor-ex/CustomProjectWindow/CustomProjectTreeModel.cs)。

## 設定ファイルの基本情報

| 項目 | 値 |
| :--- | :--- |
| パス | `UserSettings/CustomProjectWindowSettings.json`（プロジェクトルート） |
| モード設定 | `Lilja/EditorEx/Save Mode/UserSettings File` (有効時のみ本JSONが機能します) |

## ルートオブジェクト

```json
{
  "Version": 2,
  "Nodes": []
}
```

| フィールド | 型 | 必須 | 説明 |
| :--- | :--- | :--- | :--- |
| `Version` | number | はい | 現状は常に `2` |
| `Nodes` | array | はい | すべてのノードが含まれるフラットな配列 |

---

## ノード共通フィールド

すべてのノードは `Nodes` 配列の中にフラットに配置され、以下のフィールドを持ちます。

| フィールド | 型 | 必須 | 説明 |
| :--- | :--- | :--- | :--- |
| `Id` | string | はい | 一意な識別子。手動作成時は UUID や一意な文字列を設定します。 |
| `ParentId` | string | はい | 親ノードの `Id`。ルート直下の場合は空文字列 `""` を指定します。 |
| `Label` | string | はい | ツリー上に表示される名前。 |
| `Kind` | number | はい | ノードのタイプ。`0` = Group, `1` = Folder, `2` = Asset |
| `Source` | number | はい | ノードの出自。`0` = Manual, `1` = FolderRefRoot, `3` = FolderPointer |
| `AssetGuid` | string | いいえ | アセットまたはフォルダのUnity GUID。新規追加時は **常に空文字列 `""` を設定してください**（Unityでのロード時に自動補完されます）。 |
| `AssetPath` | string | いいえ | アセットまたはフォルダのパス。`Assets/` から始まるプロジェクト相対。 |
| `IsExpanded` | boolean | はい | ツリー上での展開状態。グループなどの場合は `true` 推奨。 |

---

## 各ノードタイプの設定値

### 1. Group（手動仮想グループ）
子ノード（アセットや別のサブグループ）をまとめるための仮想フォルダです。
* `Kind`: `0`
* `Source`: `0`
* `AssetGuid`: 不要（空または省略）
* `AssetPath`: 不要（空または省略）

### 2. FolderRefRoot（同期フォルダ）
ディスク上の特定のフォルダーをツリーにリンクし、その中身を自動的に同期表示します。
* `Kind`: `1`
* `Source`: `1`
* `AssetGuid`: 必須（フォルダのGUID）
* `AssetPath`: 必須（`Assets/` からの相対フォルダパス）
* **注意**: このフォルダ配下に含まれる子ファイル・フォルダは Unity 側で動的にスキャン・同期されるため、**JSON には一切記述しません。**

### 3. FolderPointer（フォルダポインター）
特定のフォルダへの簡易的なブックマークです。中身は同期しません。
* `Kind`: `1`
* `Source`: `3`
* `AssetGuid`: 必須（フォルダのGUID）
* `AssetPath`: 必須（`Assets/` からの相対フォルダパス）

### 4. Asset（手動アセットポインター）
特定のファイルアセットへの参照です。
* `Kind`: `2`
* `Source`: `0`
* `AssetGuid`: 必須（ファイルのGUID）
* `AssetPath`: 必須（`Assets/` からの相対ファイルパス）
