---
trigger: always_on
---

# C# & Unity Coding Standards

あなたはプロフェッショナルなC#エンジニアとして、以下のルールを厳格に順守してコードを生成・修正してください。

## 1. 基本スタイル (Formatting)
- **Indent**: 半角スペース4（タブ禁止）
- **Line Endings**: LF
- **Braces `{}`**: 改行して配置 (Allman style)
- **Access Modifiers**: すべてのメンバで明示的に指定（privateも省略不可）
- **Empty Strings**: `""` ではなく `string.Empty` を使用

## 2. 命名規則 (Naming Conventions)
- **PascalCase**:
  - クラス
  - 構造体
  - インターフェース (接頭辞 `I`)
  - メソッド
  - public変数
  - internal変数
  - publicプロパティ
  - internalプロパティ
  - private プロパティ
  - 定数
- **camelCase**:
  - メソッド引数
  - ローカル変数
- **`_` + camelCase**:
  - privateフィールド
- **Others**:
  - 属性型: 必ず `Attribute` で終える
  - 列挙型: 原則として単数形。ただし `[Flags]` 属性付与時のみ複数形

## 3. Unity 固有ルール
- **Serialization**: `[SerializeField]` は「フィールド」にのみ付与し、「プロパティ」には付与しない。
- **Unity Events**: `Start`, `Update` 等のイベント関数は原則 `private`。継承前提の場合のみ `protected`。

## 4. 非同期処理 (UniTask)
- **Naming**: すべての非同期メソッドは末尾に `Async` を付与。
- **Cancellation**: `CancellationToken` を引数に取れる場合は必ず受け取り、メソッド内部の非同期処理に伝搬させる。