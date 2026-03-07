# Lilja.Repository

Source Generatorを活用した、高性能・トランザクション対応のリポジトリパターンを提供するUnityパッケージです。  
`[Entity]` 属性を付与したクラスから、DTO・Converter・Formatter・Repositoryを自動生成します。

## 主な機能

- **Source Generator による自動コード生成**
  - DTO（フラットな `[Serializable]` クラス）
  - Converter（Entity ↔ DTO 変換）
  - Repository インターフェース + 実装（InMemory / JSON / MessagePack）
  - MessagePack Formatter（依存フリーな低レベル実装）
  - Key Accessor（`[Key]` 属性付きEntity用）
- **トランザクション管理** — `TxManager` による読み取り / 読み書きトランザクション（SemaphoreSlim による書き込み直列化）
- **ValueObject サポート** — `[ToPrimitive]` / `[FromPrimitive]` によるプリミティブ変換・フラット化
- **Atomic ファイル書き込み** — `AtomicFileWriter` による一時ファイル→リプレースパターンでデータ破損を防止
- **エディタ拡張** — Repository Viewer で実行時のリポジトリ状態をインスペクト可能

## 要件

- Unity 6000.3 以降
- [UniTask](https://github.com/Cysharp/UniTask) 2.5.10 以降

## インストール

`Plugins/Lilja/Package Management/Fix Lilja Package Paths to Relative` より追加してください（ローカルパッケージとしてインポートする場合）。

## 使い方

### 1. Entity の定義

`[Entity]` 属性をクラスに付与し、永続化するフィールドに `[Persist(index)]` を指定します。  
主キーには `[Key]` 属性を付与します。

```csharp
using Lilja.Repository;

[Entity]
public partial class Item
{
    [Key]
    [Persist(0)]
    private int _id;

    [Persist(1)]
    private string _name;

    [Persist(2)]
    private Coordinate _location;
}
```

### 2. ValueObject の定義

`[ToPrimitive]` / `[FromPrimitive]` 属性を使用して、プリミティブ型への変換・復元を定義します。  
Source Generator はこれを検出し、DTO内でフィールドを自動的にフラット化します。

```csharp
using Lilja.Repository;

public struct Coordinate
{
    public int X { get; }
    public int Y { get; }

    [FromPrimitive]
    public Coordinate(int x, int y)
    {
        X = x;
        Y = y;
    }

    [ToPrimitive]
    public (int x, int y) ToPrimitive() => (X, Y);
}
```

### 3. 自動生成されるコード

上記の `Item` Entity から以下が自動生成されます。

| 生成物          | クラス名                      | 説明                                     |
| --------------- | ----------------------------- | ---------------------------------------- |
| DTO             | `ItemDto`                     | フラット化された `[Serializable]` クラス |
| Converter       | `Item.ToDto` / `Item.FromDto` | Entity ↔ DTO 変換メソッド                |
| Repository I/F  | `IItemRepository`             | CRUD操作インターフェース                 |
| InMemory実装    | `InMemoryItemRepository`      | メモリ上のリポジトリ                     |
| JSON実装        | `JsonItemRepository`          | JSONファイル永続化リポジトリ             |
| MessagePack実装 | `MessagePackItemRepository`   | MessagePack永続化リポジトリ（※）         |
| Formatter       | `ItemDtoFormatter`            | 依存フリーなMessagePackフォーマッタ（※） |
| Key Accessor    | Key Accessor                  | `[Key]` フィールドへのアクセサ           |

> ※ MessagePack関連のコードは、プロジェクトにMessagePackが参照されている場合のみ生成されます。

### 4. トランザクション

`TxManager` を通じてリポジトリ操作をトランザクション内で実行できます。

```csharp
var txManager = new TxManager();

// 読み取りトランザクション
txManager.BeginROTransaction(tx =>
{
    // 読み取り操作
});

// 読み書きトランザクション（コミット/ロールバック対応）
await txManager.BeginRWTransactionAsync(tx =>
{
    // 読み書き操作
    // 正常完了でコミット、例外でロールバック
}, cancellationToken);
```

## コア属性一覧

| 属性               | 対象                      | 説明                                            |
| ------------------ | ------------------------- | ----------------------------------------------- |
| `[Entity]`         | クラス                    | Source Generator の対象としてマーク             |
| `[Key]`            | フィールド / プロパティ   | 主キーフィールドをマーク                        |
| `[Persist(index)]` | フィールド / プロパティ   | 永続化対象フィールドをマーク（indexで順序指定） |
| `[ToPrimitive]`    | メソッド                  | ValueObjectのプリミティブ変換メソッドをマーク   |
| `[FromPrimitive]`  | コンストラクタ / メソッド | ValueObjectのプリミティブ復元メソッドをマーク   |

## ディレクトリ構成

```
lilja.repository/
├── src/
│   ├── Scripts/
│   │   ├── Runtime/
│   │   │   └── Core/
│   │   │       ├── Attributes/       # Entity, Key, Persist, ToPrimitive, FromPrimitive
│   │   │       ├── Transactions/     # TxManager, IReadOnlyTx, IReadWriteTx
│   │   │       ├── Diagnostics/      # RepositoryTracker（エディタ専用）
│   │   │       └── IO/               # AtomicFileWriter
│   │   └── Editor/                   # RepositoryViewer（エディタ拡張）
│   └── Plugins/                      # Source Generator DLL
├── Analyzer/                         # Source Generator ソースコード
│   ├── Lilja.Repository.Analyzer/
│   │   ├── RepositoryGenerator.cs    # IIncrementalGenerator 実装
│   │   ├── Analysis/                 # EntityAnalyzer
│   │   ├── Emitters/                 # DTO, Converter, Formatter, Repository, KeyAccessor
│   │   └── Models/                   # EntityInfo, FieldInfo
│   └── Lilja.Repository.Analyzer.Test/
└── package.json
```

## ライセンス

[LICENSE](https://github.com/kamahir0/Lilja/blob/main/LICENSE) を参照してください。
