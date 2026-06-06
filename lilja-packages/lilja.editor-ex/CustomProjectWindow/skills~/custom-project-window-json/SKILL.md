---
name: custom-project-window-json
description: >-
  Edits Unity Custom Project Window tree data in UserSettings/CustomProjectWindowSettings.json
  (groups, assets, folder-refs). Use when editing custom project window layout.
  Requires SaveMode=UserSettingsFile. Nodes are stored as a flat list with ParentId.
---

# Custom Project Window Settings JSON 編集

Unity 拡張 `Lilja.EditorEx` の「Project (Custom)」ウィンドウのツリーデータを [`.vscode/../UserSettings/CustomProjectWindowSettings.json`](UserSettings/CustomProjectWindowSettings.json) で編集する手順。
詳細スキーマは [reference.md](reference.md)、操作例は [examples.md](examples.md)。

## 1. 前提チェック（必須）

編集前に必ず確認してください。

1. **保存モード (SaveMode)**: 
   - Unity エディタの上部メニュー `Lilja/EditorEx/Save Mode/UserSettings File` にチェックが入っているか（または右上コンテキストメニューで `UserSettings ファイル` になっているか）。
   - `EditorPrefs` モードの場合、**JSON ファイルを編集しても Unity 側に反映されません。**
2. **設定ファイルの場所**:
   - Unityプロジェクトルート直下の `UserSettings/CustomProjectWindowSettings.json`。
   - 存在しない場合は、初回は `{ "Version": 2, "Nodes": [] }` で新規作成して構いません。

## 2. 標準ワークフロー

1. `UserSettings/CustomProjectWindowSettings.json` を **Read** する。
2. 編集方針を決める。**Unity版はVSCode版と異なり、JSON構造が「フラットなリスト（Nodes）」であり、親子関係は `ParentId` で紐付けられている**点に注意する。
3. 依頼に合わせて **最小差分** で編集する。
4. **Write** で保存する。
5. 保存後、Unity エディタにフォーカスが戻った際（`focusChanged` 時）、ファイルのタイムスタンプが更新されていれば自動的にツリーが再ロードされます。
6. 変更内容を 1〜2 文でユーザーに報告する。

## 3. Unity特有のルール

- **パスの形式**:
  - パスは常に `Assets/` または `Packages/` から始まるプロジェクト相対パス（例: `Assets/Scripts/PlayerController.cs`）とし、区切り文字は `/` を使用します。
- **GUIDの省略と自動補完**:
  - アセット（`Kind: 2`）やフォルダ（`Kind: 1`）を追加する際、**`AssetGuid` は空文字列 `""`（または省略）にして構いません**。
  - `AssetPath` さえ正確に指定されていれば、Unity側で設定ファイルをロードする際に自動的に `AssetDatabase.AssetPathToGUID` を使用して `AssetGuid` が自動解決・補完され、保存時に自動で書き込まれます。
  - AIエージェント自身が GUID を事前に検索する必要はありません。
- **ディスクの同期**:
  - フォルダ参照（`Kind: 1, Source: 1`）を登録する場合、その配下にある子ファイルや子フォルダは Unity 側で動的にスキャン・同期（`FolderRefSynced`）されます。
  - そのため、同期される子フォルダ・ファイルは **JSON に記述してはいけません**（保存時に除外されます）。

## 4. スキーマ要約

| 項目 | 値 |
| :--- | :--- |
| ルート | `{ "Version": 2, "Nodes": [ ... ] }` |
| `Id` | 一意な文字列。新規グループは `manual-group:{Guid:N}` 形式など。新規アセットは `manual-asset:{Guid:N}` 形式。手動でユニークな文字列を指定しても構いません。 |
| `ParentId` | 親ノードの `Id`。ルート直下の場合は空文字列 `""`。 |
| `Kind` | `0` = Group (仮想グループ), `1` = Folder (フォルダ参照など), `2` = Asset (アセットポインター) |
| `Source` | `0` = Manual (手動グループ/手動アセット), `1` = FolderRefRoot (同期フォルダ), `3` = FolderPointer (フォルダポインター) |

## 5. 禁止・注意事項

* **GUID を自力で推測したり検索したりしない**:
  * 追加するアセットの GUID を検索したり、適当な文字列を当てずっぽうで書き込んだりしてはいけません。新規ノードの追加時は一貫して **`AssetGuid` を `""`（空文字列）に設定** してください。
* **ディスク同期される子ノードを書き込まない**:
  * フォルダ参照（`Source: 1`）の配下に同期されるファイルやフォルダは、ロード時に自動で展開されるため、JSONに子ノードとして追加してはいけません。
* **絶対パスを使用しない**:
  * Unityプロジェクト外のファイルや、絶対パス（例: `C:/...`）を指定してはいけません。必ず `Assets/` または `Packages/` から始まる相対パスを指定してください。

---

## 6. 完了チェックリスト

- [ ] 保存モードが `UserSettingsFile` であることを確認した
- [ ] 有効な JSON、`Version` は `2`、`Nodes` はフラット配列
- [ ] 新規アセット・フォルダの追加時に **`AssetGuid` を `""` (空文字列)** に設定した（自力で解決しようとしていない）
- [ ] 親子関係がネストではなく `ParentId` で表現されている
- [ ] アセットやフォルダパスが `Assets/` または `Packages/` から始まっている
- [ ] フォルダ参照（`Source: 1`）の子要素を JSON に含めていない
