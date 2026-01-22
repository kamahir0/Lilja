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
  - クラス、構造体、メソッド、定数、public/internalメンバ
  - インターフェース (接頭辞 `I`)
  - private プロパティ
- **camelCase**:
  - メソッド引数、ローカル変数
- **Field Rules**:
  - private フィールドは `_` + camelCase (例: `_itemCount`)
- **Collection Naming**:
  - Array / IEnumerable: 英語の複数形
  - List / Queue / Stack: 末尾に型名を付与 (例: `itemsList`, `taskQueue`)
  - Dictionary: 末尾に `Dict` (例: `userDict`)
  - ReactiveProperty<T> (R3): 末尾に `RP` (例: `hpRP`)
- **Others**:
  - 属性型: 必ず `Attribute` で終える
  - 列挙型: 原則として単数形。ただし `[Flags]` 属性付与時のみ複数形

## 3. Unity 固有ルール
- **Serialization**: `[SerializeField]` は「フィールド」にのみ付与し、「プロパティ」には付与しない。
- **Unity Events**: `Start`, `Update` 等のイベント関数は原則 `private`。継承前提の場合のみ `protected`。

## 4. 非同期処理 (UniTask)
- **Naming**: すべての非同期メソッドは末尾に `Async` を付与。
- **Cancellation**: `CancellationToken` を引数に取れる場合は必ず受け取り、メソッド内部の非同期処理に伝搬させる。

## 5. リアクティブプログラミング (R3)
- **Memory Management**: `Subscribe` 時は必ず `AddTo` または `IDisposable` の保持を行い、適切に破棄する。
- **Disposable Optimization**: 連結が必要な場合は、以下の性能優先順位に従って最適な型を選択する。
  `Combine` > `CreateBuilder` > `DisposableBag` > `CompositeDisposable`