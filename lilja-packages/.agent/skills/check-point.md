# Checkpoint Skill
## WHEN
- タスク完了後（機能実装完了、テスト通過時）
- 30分以上連続作業後
- 変更ファイルが10個以上蓄積

## HOW
1. git status分析し、論理的な単位でステージング
2. Conventional Commits形式でコミットメッセージ生成
3. LiljaパッケージごとのCHANGELOG.mdに該当セクション追加（日時・変更概要）
4. 通知：「Checkpoint {commit-hash} created」
