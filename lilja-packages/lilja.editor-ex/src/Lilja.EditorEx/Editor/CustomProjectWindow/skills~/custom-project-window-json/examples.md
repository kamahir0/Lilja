# Custom Project Window Settings JSON 操作例

前提: `Lilja/EditorEx` の `Save Mode` が `UserSettings File` であること。

---

## 1. ルートに新しい仮想グループを追加する

**依頼**: 「UI」という名前のグループを追加して。

**Before**:
```json
{
  "Version": 2,
  "Nodes": []
}
```

**After**:
```json
{
  "Version": 2,
  "Nodes": [
    {
      "Id": "manual-group:ui_assets_01",
      "ParentId": "",
      "Label": "UI",
      "Kind": 0,
      "Source": 0,
      "AssetGuid": "",
      "AssetPath": "",
      "IsExpanded": true
    }
  ]
}
```
* `Id` は手動編集の際、一意であれば `manual-group:任意のユニーク名` のような形式で構いません。
* `ParentId` はルート直下なので `""`（空文字列）にします。
* `Kind: 0` (Group) / `Source: 0` (Manual) を指定します。

---

## 2. 既存のグループ内にアセット（ファイル）を追加する

**依頼**: `Assets/Prefabs/Player.prefab` を既存の「UI」グループ（ID: `manual-group:ui_assets_01`）に追加して。
**前提調査**: AIエージェント側で GUID を事前に調べる必要はありません（`AssetGuid` は空文字列 `""` で指定すれば、Unity側でロード時に自動解決されます）。

**Before**:
```json
{
  "Version": 2,
  "Nodes": [
    {
      "Id": "manual-group:ui_assets_01",
      "ParentId": "",
      "Label": "UI",
      "Kind": 0,
      "Source": 0,
      "AssetGuid": "",
      "AssetPath": "",
      "IsExpanded": true
    }
  ]
}
```

**After**:
```json
{
  "Version": 2,
  "Nodes": [
    {
      "Id": "manual-group:ui_assets_01",
      "ParentId": "",
      "Label": "UI",
      "Kind": 0,
      "Source": 0,
      "AssetGuid": "",
      "AssetPath": "",
      "IsExpanded": true
    },
    {
      "Id": "manual-asset:player_prefab_01",
      "ParentId": "manual-group:ui_assets_01",
      "Label": "Player.prefab",
      "Kind": 2,
      "Source": 0,
      "AssetGuid": "",
      "AssetPath": "Assets/Prefabs/Player.prefab",
      "IsExpanded": false
    }
  ]
}
```
* `ParentId` に、追加先グループの `Id` (`manual-group:ui_assets_01`) を指定します。
* `Kind: 2` (Asset) / `Source: 0` (Manual) を指定します。
* `AssetGuid` は空文字列 `""` を指定し、`AssetPath` を正確に記述します（ロード時に自動補完されます）。

---

## 3. フォルダ参照（同期フォルダ）を追加する

**依頼**: `Assets/Scripts` フォルダを同期表示して。
**前提調査**: AIエージェント側で GUID を事前に調べる必要はありません（`AssetGuid` は空文字列 `""` で指定すれば、Unity側でロード時に自動解決されます）。

**After**:
```json
{
  "Version": 2,
  "Nodes": [
    {
      "Id": "folderref-root:Assets/Scripts",
      "ParentId": "",
      "Label": "Scripts",
      "Kind": 1,
      "Source": 1,
      "AssetGuid": "",
      "AssetPath": "Assets/Scripts",
      "IsExpanded": false
    }
  ]
}
```
* `Kind: 1` (Folder) / `Source: 1` (FolderRefRoot) を指定します。
* **注意**: `Assets/Scripts` の中に含まれるスクリプトや子フォルダは、Unityエディタがディスクから直接スキャンしてツリーを自動構築するため、**JSON の `Nodes` リストに子ノードを追加してはいけません。**

---

## 4. ノードを別のグループへ移動する

**依頼**: 「Player.prefab」を「UI」グループから新しく作った「Characters」グループに移動して。

**Before**:
```json
{
  "Version": 2,
  "Nodes": [
    { "Id": "manual-group:ui_assets_01", "ParentId": "", "Label": "UI", "Kind": 0, "Source": 0, "AssetGuid": "", "AssetPath": "", "IsExpanded": true },
    { "Id": "manual-asset:player_prefab_01", "ParentId": "manual-group:ui_assets_01", "Label": "Player.prefab", "Kind": 2, "Source": 0, "AssetGuid": "12345678...", "AssetPath": "Assets/Prefabs/Player.prefab", "IsExpanded": false }
  ]
}
```

**After**:
```json
{
  "Version": 2,
  "Nodes": [
    { "Id": "manual-group:ui_assets_01", "ParentId": "", "Label": "UI", "Kind": 0, "Source": 0, "AssetGuid": "", "AssetPath": "", "IsExpanded": true },
    { "Id": "manual-group:char_assets_01", "ParentId": "", "Label": "Characters", "Kind": 0, "Source": 0, "AssetGuid": "", "AssetPath": "", "IsExpanded": true },
    { "Id": "manual-asset:player_prefab_01", "ParentId": "manual-group:char_assets_01", "Label": "Player.prefab", "Kind": 2, "Source": 0, "AssetGuid": "12345678...", "AssetPath": "Assets/Prefabs/Player.prefab", "IsExpanded": false }
  ]
}
```
* `Player.prefab` ノードの `ParentId` を、新しい「Characters」グループの `Id` (`manual-group:char_assets_01`) に書き換えるだけで移動が完了します。

---

## 5. ノードの削除

**依頼**: `Player.prefab` をカスタムウィンドウから取り除いて。

**Before**:
```json
{
  "Version": 2,
  "Nodes": [
    { "Id": "manual-group:char_assets_01", "ParentId": "", "Label": "Characters", "Kind": 0, "Source": 0, "AssetGuid": "", "AssetPath": "", "IsExpanded": true },
    { "Id": "manual-asset:player_prefab_01", "ParentId": "manual-group:char_assets_01", "Label": "Player.prefab", "Kind": 2, "Source": 0, "AssetGuid": "12345678...", "AssetPath": "Assets/Prefabs/Player.prefab", "IsExpanded": false }
  ]
}
```

**After**:
```json
{
  "Version": 2,
  "Nodes": [
    { "Id": "manual-group:char_assets_01", "ParentId": "", "Label": "Characters", "Kind": 0, "Source": 0, "AssetGuid": "", "AssetPath": "", "IsExpanded": true }
  ]
}
```
* 削除対象のノードを `Nodes` 配列から取り除くだけで完了します。
