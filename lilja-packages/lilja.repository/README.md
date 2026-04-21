# Lilja.Repository

`[Entity]` を付けたクラスから、Unity 用の Repository 実装を source generator で生成するパッケージです。  
generated repository を使う前提で、Entity 定義、transaction、永続化ファイルをできるだけ単純に保ちつつ、DDD / オニオンアーキテクチャ向けの Repository を扱いやすくすることを目指しています。

## 何が生成されるか

- `I{Entity}Repository`
- `InMemory{Entity}Repository`
- `[Persist]` がある場合:
  - `{Entity}Dto`
  - `{Entity}.ToDto` / `{Entity}.FromDto`
  - `Json{Entity}Repository`
- `[Key]` がある場合:
  - `{Entity}.GetKey`
  - `[Persist]` もあると `{Entity}.GetKeyFromDto`
- MessagePack 参照がある場合のみ:
  - `{Entity}DtoFormatter`
  - `MessagePack{Entity}Repository`

## 要件

- Unity `6000.3` 以降
- `com.cysharp.unitask` `2.5.10` 以降
- MessagePack は optional

## Entity の書き方

- 対応対象は `instance field` と `instance auto-property`
- `static` メンバー、計算プロパティ、custom accessor property は非対応
- 永続化リポジトリを生成するなら、`[Key]` メンバーにも `[Persist(index)]` が必要
- Entity は実質的に immutable を前提とします。mutable entity は unsupported contract です

```csharp
using Lilja.Repository;

namespace Demo;

[Entity]
public partial class Item
{
    [Key]
    [Persist(0)]
    public int Id { get; }

    [Persist(1)]
    public string Name { get; }

    [Persist(2)]
    public Coordinate Position { get; }

    public Item(int id, string name, Coordinate position)
    {
        Id = id;
        Name = name;
        Position = position;
    }
}
```

## ValueObject

`[ToPrimitive]` と `[FromPrimitive]` が対になっている型は DTO へフラット化されます。  
復元は `[FromPrimitive]` static メソッド、または `[FromPrimitive]` 付きコンストラクタから行われます。

```csharp
using Lilja.Repository;

public readonly struct Coordinate
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

## Repository の使い方

`InMemory` はそのまま使えます。  
`Json` / `MessagePack` は利用前に `InitializeAsync` が必須です。  
`Read` は存在しない可能性を表すため nullable を返します。  
永続化は `1 repository = 1 file` の単純な形です。

このパッケージは generated repository を使う前提です。  
`RepositoryTx` のような low-level helper を使った手書き custom repository はサポート対象外です。
- `InMemoryKeyed / InMemorySingleton / PersistedKeyed / PersistedSingleton` の4系統 base は意図的に維持しています
- keyed は `Items`、singleton は `HasValue` / `Item` の永続化 shape を使い分けます

- `Create` は current staged view に対象がすでに存在すると `InvalidOperationException` を投げます
- `Update` は current staged view に対象が存在しないと `InvalidOperationException` を投げます
- `Delete` は current staged view に対象が存在しないと `InvalidOperationException` を投げます
- 存在判定は committed state ではなく、同一 transaction 内の staged な変更を含めた current view で行われます

```csharp
using Demo.Repositories;
using Lilja.Repository;

var txManager = new TxManager();
var repository = new JsonItemRepository();

await repository.InitializeAsync();

await txManager.BeginRWTransactionAsync(tx =>
{
    repository.Create(tx, new Item(1, "Potion", new Coordinate(10, 20)));
});

txManager.BeginROTransaction(tx =>
{
    var item = repository.Read(tx, 1);
    if (item is not null)
    {
        UnityEngine.Debug.Log(item.Name);
    }
});
```

## Transaction 契約

- RW transaction 中の変更は transaction-local に staged されます
- RO transaction は committed state だけを見ます
- commit は `persist all dirty staged states` の成功後に committed state を差し替えます
- persist 失敗時は committed state は更新されず、例外が呼び出し元へ返ります
- strict CRUD の存在判定は current staged view 基準です
- rollback は staged state を捨てるだけで、コミット済み状態は触りません
- low-level transaction helper は public contract ではなく、generated repository の内部実装です

### 制約

- `AtomicFileWriter` が保証するのは単一ファイルの置換までです
- 複数 repository ファイルをまたぐ crash-safe ACID commit は保証しません

## Editor

- Repository Viewer は MessagePack を compile-time 必須にしません
- MessagePack 未導入時は JSON / InMemory だけを表示します
- DTO の key 表示は生成された `GetKeyFromDto` を優先して使います

## 診断

generator は次の形を error として止めます。

- `partial` でない Entity
- generic Entity
- `static` な `[Key]` / `[Persist]`
- computed property / custom accessor property
- 重複した `Persist(index)`
- `[Persist]` されていない key を持つ永続化 Entity
- 不正な `[ToPrimitive]` / `[FromPrimitive]`

## 開発メモ

analyzer のビルド成果物は `src/Plugins/Lilja.Repository.Analyzer.dll` へコピーされます。  
release 前は次を実行して、source と同梱 DLL を同期してください。

```powershell
dotnet build .\Analyzer\Lilja.Repository.Analyzer.slnx
```

## ライセンス

[LICENSE](https://github.com/kamahir0/Lilja/blob/main/LICENSE)
