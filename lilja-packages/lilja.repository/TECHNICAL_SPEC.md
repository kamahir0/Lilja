# Lilja.Repository Technical Specification

## 1. 文書の目的と適用範囲

### 仕様

本書は `lilja-packages/lilja.repository` パッケージの技術仕様書である。  
目的は、この文書だけを参照して、公開 API、source generator の入力規約、生成物、利用方法、transaction 挙動、永続化形式、Editor 補助機能、Analyzer diagnostics が本実装と実質同一になるような互換実装を再構築できるようにすることである。

本書の対象は以下の 3 レイヤーである。

1. Runtime 基盤
2. Editor tooling
3. Source generator / bundled analyzer

### 実装由来の注記

- 本書の基準は 2026-04-22 時点の現行ワークツリーである。
- `src/package.json` の version は `0.2.0` だが、`CHANGELOG.md` には `Unreleased` 項目も存在する。本書は package manifest の version 表記よりも、現行ソースコードとテストの挙動を優先する。
- 事実上の source of truth は以下である。
  - `src/Scripts/Runtime`
  - `src/Scripts/Editor`
  - `Analyzer/Lilja.Repository.Analyzer`
  - `Analyzer/Lilja.Repository.Analyzer.Test`
- 本書作成時点で Analyzer test suite は 42 件すべて成功している。

### 非保証

- 本書は README の言い換えではない。README にのみ書かれていて実装やテストで裏づけられない内容は、原則として public guarantee としては採用しない。
- 同梱 DLL `src/Plugins/Lilja.Repository.Analyzer.dll` のバイナリ内容は参照していない。ソースコードとテストが優先される。

## 2. パッケージメタデータ

### 仕様

`src/package.json` に基づくパッケージメタデータは以下のとおり。

| 項目 | 値 |
| --- | --- |
| package name | `com.kamahir0.lilja.repository` |
| display name | `Lilja.Repository` |
| package version | `0.2.0` |
| required Unity version | `6000.3` |
| summary | Unity repository source generator with staged transactions, JSON persistence, and optional MessagePack support |
| required dependency | `com.cysharp.unitask` `2.5.10` |
| optional dependency | MessagePack runtime / formatter APIs |

### 実装由来の注記

- MessagePack は package dependency としては宣言されていない。generator はコンパイル参照内に `MessagePack.Formatters.IMessagePackFormatter<T>` が存在する場合のみ MessagePack 用生成物を出力する。

### 非保証

- `package.json` の version が `0.2.0` であることは、現行ソースが 0.2.0 リリース内容だけに限定されることを意味しない。

## 3. アーキテクチャ概要

### 仕様

Lilja.Repository は、「`[Entity]` を付与した partial class から repository 群を source generator で生成し、生成済み repository を runtime で利用する」構成を取る。

ランタイム設計の中核は以下である。

1. `TxManager` による single-writer / snapshot-reader transaction 制御
2. 生成済み repository 基底クラスによる strict CRUD と staged write
3. 永続化 repository における `1 repository = 1 file` モデル
4. Analyzer による Entity 形状の制約と code generation
5. Editor による repository tracking / persisted file inspection

### 実装由来の注記

- 手書き custom repository を low-level helper で組み立てる用途は、README・changelog・runtime visibility test のすべてで public contract から外されている。
- 実装上の low-level helper (`RepositoryTx`, write state, overlay state, internal type cache など) は内部実装であり public API ではない。

### 非保証

- DDD / オニオンアーキテクチャへの適用しやすさは設計意図ではあるが、フレームワーク的な拡張ポイントや policy injection を網羅的に提供することは保証しない。

## 4. 配布物とソース構成

### 仕様

主な構成は以下のとおり。

| レイヤー | パス | 役割 |
| --- | --- | --- |
| Runtime | `src/Scripts/Runtime` | transaction、repository base、attributes、diagnostics、atomic file I/O |
| Editor | `src/Scripts/Editor` | Repository Viewer、persisted file loader、MessagePack reflection bridge |
| Analyzer source | `Analyzer/Lilja.Repository.Analyzer` | Roslyn incremental generator と emitters |
| Analyzer tests | `Analyzer/Lilja.Repository.Analyzer.Test` | generator/runtime/editor 契約の回帰テスト |
| Bundled analyzer | `src/Plugins/Lilja.Repository.Analyzer.dll` | Unity 側へ同梱する analyzer DLL |

### 実装由来の注記

- `Analyzer/Lilja.Repository.Analyzer.slnx` の `dotnet build` により analyzer DLL が `src/Plugins` へコピーされる。
- test 実行時にも同 DLL が更新されることがある。

### 非保証

- DLL のビルド・コピー手順自体は開発手順であり、runtime contract ではない。

## 5. 公開 Runtime API

### 仕様

現行実装で public runtime surface に含まれる型は以下のみである。

| 型 | 用途 |
| --- | --- |
| `Lilja.Repository.EntityAttribute` | Entity マーカー |
| `Lilja.Repository.KeyAttribute` | 主キー指定 |
| `Lilja.Repository.PersistAttribute` | 永続化対象と DTO index 指定 |
| `Lilja.Repository.ToPrimitiveAttribute` | ValueObject の primitive 化 |
| `Lilja.Repository.FromPrimitiveAttribute` | ValueObject の復元 |
| `Lilja.Repository.IReadOnlyTx` | 読み取り transaction インターフェース |
| `Lilja.Repository.IReadWriteTx` | 読み書き transaction インターフェース |
| `Lilja.Repository.TxManager` | transaction coordinator |
| `Lilja.Repository.AtomicFileWriter` | atomic file replacement utility |
| `Lilja.Repository.InMemoryKeyedRepositoryBase<TEntity, TKey>` | keyed in-memory repository 基底 |
| `Lilja.Repository.InMemorySingletonRepositoryBase<TEntity>` | singleton in-memory repository 基底 |
| `Lilja.Repository.PersistedKeyedRepositoryBase<TEntity, TKey, TDto>` | keyed persisted repository 基底 |
| `Lilja.Repository.PersistedSingletonRepositoryBase<TEntity, TDto>` | singleton persisted repository 基底 |
| `Lilja.Repository.Diagnostics.RepositoryTracker` | Editor 向け repository tracker |
| `Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType` | tracker 用 enum |

内部型 `RepositoryTx`、`RepositoryWriteState<T>`、`RepositoryOverlayState<TKey, TValue>`、`RuntimeInstanceMonitor` は public surface に含まれない。

### 5.1 属性 API

#### 仕様

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class EntityAttribute : Attribute {}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class KeyAttribute : Attribute {}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class PersistAttribute : Attribute
{
    public int Index { get; }
    public PersistAttribute(int index);
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ToPrimitiveAttribute : Attribute {}

[AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class FromPrimitiveAttribute : Attribute {}
```

#### 実装由来の注記

- `PersistAttribute.Index` は DTO への field 展開順および `FromDto` の constructor argument 順を決める。
- `FromPrimitiveAttribute` は constructor と method の両方に付けられるが、generator が受理するのは「1 個の static factory」または「1 個の constructor」のどちらかだけである。

#### 非保証

- 属性の継承、複数付与、interface や struct への `[Entity]` 付与はサポートしない。

### 5.2 Transaction API

#### 仕様

```csharp
public interface IReadOnlyTx : IDisposable {}
public interface IReadWriteTx : IReadOnlyTx {}

public class TxManager
{
    public TxManager();
    public void BeginROTransaction(Action<IReadOnlyTx> action);
    public UniTask BeginROTransactionAsync(Func<IReadOnlyTx, UniTask> action);
    public UniTask BeginRWTransactionAsync(Action<IReadWriteTx> action, CancellationToken ct = default);
    public UniTask BeginRWTransactionAsync(Func<IReadWriteTx, UniTask> action, CancellationToken ct = default);
}
```

`BeginROTransaction*` は read-only transaction を開始する。  
`BeginRWTransactionAsync*` は read-write transaction を開始し、delegate 正常終了後に commit を試みる。

引数 `action` が `null` の場合は `ArgumentNullException`。

#### 実装由来の注記

- `TxManager` constructor は development/editor 向け duplicate-instance 監視に登録される。
- sync RO API は内部的に reader admission が開くまで block する。
- RW API は内部で `SemaphoreSlim` による single writer lock を取る。

#### 非保証

- nested transaction、複数 writer の並列許可、ambient transaction、distributed transaction は提供しない。

### 5.3 AtomicFileWriter API

#### 仕様

```csharp
public static class AtomicFileWriter
{
    public static void WriteAllText(string filePath, string content);
    public static void WriteAllBytes(string filePath, byte[] bytes);
    public static void DeleteIfExists(string filePath);
}
```

`WriteAllText` / `WriteAllBytes` は一時ファイル `*.tmp` を書き出し、その後 `File.Replace` または `File.Move` により対象ファイルへ置換する。  
対象ファイルが既存の場合は `*.bak` を一時 backup として使い、置換後に削除する。  
対象ファイルが未存在の場合は必要に応じてディレクトリを作成し、temp file を rename する。

#### 実装由来の注記

- backup file が残っていれば置換前に削除される。
- `DeleteIfExists` は単純な `File.Exists` + `File.Delete` であり、atomic delete ではない。

#### 非保証

- 複数ファイルをまとめた atomicity は保証しない。
- 電源断や OS crash をまたぐ multi-file ACID commit は保証しない。

### 5.4 Repository Base API

#### 仕様

`Read` は C# nullability 上 nullable reference を返す。  
CLR reflection 上は `TEntity` に見えても、ソース契約は `TEntity?` である。

#### Keyed in-memory

```csharp
public abstract class InMemoryKeyedRepositoryBase<TEntity, TKey>
    where TEntity : class
    where TKey : notnull
{
    public UniTask InitializeAsync(CancellationToken ct = default);
    public TEntity? Read(IReadOnlyTx tx, TKey key);
    public void Create(IReadWriteTx tx, TEntity entity);
    public void Update(IReadWriteTx tx, TEntity entity);
    public void Delete(IReadWriteTx tx, TKey key);
    public IReadOnlyList<TEntity> All(IReadOnlyTx tx);

    protected abstract TKey GetKey(TEntity entity);
    protected virtual UniTask PersistStateAsync(Dictionary<TKey, TEntity> state, CancellationToken ct);
}
```

#### Singleton in-memory

```csharp
public abstract class InMemorySingletonRepositoryBase<TEntity>
    where TEntity : class
{
    public UniTask InitializeAsync(CancellationToken ct = default);
    public TEntity? Read(IReadOnlyTx tx);
    public void Create(IReadWriteTx tx, TEntity entity);
    public void Update(IReadWriteTx tx, TEntity entity);
    public void Delete(IReadWriteTx tx);

    protected virtual UniTask PersistStateAsync(TEntity? state, CancellationToken ct);
}
```

#### Keyed persisted

```csharp
public abstract class PersistedKeyedRepositoryBase<TEntity, TKey, TDto>
    where TEntity : class
    where TKey : notnull
    where TDto : class
{
    protected PersistedKeyedRepositoryBase(string filePath);
    protected string FilePath { get; }

    public UniTask InitializeAsync(CancellationToken ct = default);
    public TEntity? Read(IReadOnlyTx tx, TKey key);
    public void Create(IReadWriteTx tx, TEntity entity);
    public void Update(IReadWriteTx tx, TEntity entity);
    public void Delete(IReadWriteTx tx, TKey key);
    public IReadOnlyList<TEntity> All(IReadOnlyTx tx);

    protected abstract TDto ToDto(TEntity entity);
    protected abstract TEntity FromDto(TDto dto);
    protected abstract TKey GetKeyFromDto(TDto dto);
    protected abstract UniTask<IReadOnlyList<TDto>?> LoadItemsAsync(CancellationToken ct);
    protected abstract UniTask SaveItemsAsync(IReadOnlyList<TDto> items, CancellationToken ct);
}
```

#### Singleton persisted

```csharp
public abstract class PersistedSingletonRepositoryBase<TEntity, TDto>
    where TEntity : class
    where TDto : class
{
    protected PersistedSingletonRepositoryBase(string filePath);
    protected string FilePath { get; }

    public UniTask InitializeAsync(CancellationToken ct = default);
    public TEntity? Read(IReadOnlyTx tx);
    public void Create(IReadWriteTx tx, TEntity entity);
    public void Update(IReadWriteTx tx, TEntity entity);
    public void Delete(IReadWriteTx tx);

    protected abstract TDto ToDto(TEntity entity);
    protected abstract TEntity FromDto(TDto dto);
    protected abstract UniTask<TDto?> LoadValueAsync(CancellationToken ct);
    protected abstract UniTask SaveValueAsync(TDto? value, CancellationToken ct);
}
```

#### 実装由来の注記

- in-memory repository の `InitializeAsync` は no-op で `UniTask.CompletedTask` を返す。
- persisted repository は constructor で `filePath` の null/empty/whitespace を拒否し、`ArgumentException` を投げる。
- persisted repository は `InitializeAsync` 完了前に `Read` / `Create` / `Update` / `Delete` / `All` を呼ぶと `InvalidOperationException` を投げる。
- persisted repository の `FilePath` は constructor 時点の `Application.persistentDataPath` から決まる。後から `Application.persistentDataPath` が変わっても `FilePath` は更新されない。

#### 非保証

- 基底クラス継承による独自 repository 実装は技術的には可能だが、本パッケージの主契約は source generator が生成した repository を使うことにある。

### 5.5 RepositoryTracker API

#### 仕様

`RepositoryTracker` は `#if UNITY_EDITOR` 条件でのみコンパイルされる。

```csharp
public static class RepositoryTracker
{
    public enum RepositoryType
    {
        InMemory,
        Json,
        MessagePack,
    }

    public static void Track(object repository, RepositoryType type);
    public static IEnumerable<object> GetAll(RepositoryType type);
}
```

`Track` は repository instance への弱参照を登録する。  
`GetAll` は指定 type に属する live instance を列挙する。

#### 実装由来の注記

- cleanup は `Track` 呼び出し時に死んだ参照を除去する簡易 GC 方式である。
- `GetAll` 自体は live 参照のみ返すが、登録配列の完全掃除は保証しない。

#### 非保証

- runtime build で `RepositoryTracker` が利用可能であることは保証しない。

## 6. Entity authoring contract

### 仕様

generator が対象とする Entity は次を満たす必要がある。

1. `class` であること
2. `[Entity]` が付与されていること
3. `partial` であること
4. generic type でないこと

generator が解析対象とする member は以下のみ。

1. instance field
2. instance auto-property

以下はサポートしない。

1. static member 上の `[Key]` または `[Persist]`
2. computed property
3. custom accessor body を持つ property
4. expression-bodied property
5. indexer

### 実装由来の注記

- field は compiler-generated implicit field を除外し、明示宣言 field のみ解析する。
- property は `DeclaringSyntaxReferences` から `PropertyDeclarationSyntax` を確認し、expression body や accessor body がある場合は reject する。
- generator は member 全体ではなく、`[Key]` または `[Persist]` が付いた field/property だけを集計する。

### 非保証

- mutable entity は README 上も unsupported contract とされている。generator 自体は setter の存在を禁止していないが、repository の設計意図は immutable entity 前提である。

## 7. Key contract

### 仕様

- `[Key]` が 1 個以上ある Entity は keyed repository 系列を生成する。
- `[Key]` が 0 個の Entity は singleton repository 系列を生成する。
- `[Key]` が複数ある場合は composite key として扱う。

single key の場合、repository API の key 型はその member の型になる。  
composite key の場合、repository API の key 型は value tuple になる。

例:

```csharp
[Key] public int GroupId { get; }
[Key] public string UserId { get; }
```

この場合の generated key 型は `(int, string)` である。

### 実装由来の注記

- key member 自体には sort が入っていない。composite key の tuple 要素順は、Analyzer が `classSymbol.GetMembers()` から key member を収集した順に従う。
- 通常のソースコードでは実質的に宣言順と一致することが多いが、実装は key 用に明示 sort を行わない。

### 非保証

- key の uniqueness は repository の current view に対してしか検証しない。domain-level uniqueness invariant の追加 policy は提供しない。

## 8. Persist contract

### 仕様

`[Persist(index)]` が 1 個以上ある Entity は persisted 生成物の対象になる。

persisted entity では次を満たす必要がある。

1. `Persist(index)` は Entity 内で一意
2. persisted entity に `[Key]` がある場合、その key member には必ず `[Persist]` も必要

persist member は `index` 昇順に sort され、以下の順に利用される。

1. DTO field declaration 順
2. `ToDto` の代入順
3. `FromDto` の constructor argument 順
4. constructor auto-generation のシグネチャ判定順

### 実装由来の注記

- `Persist(index)` の index 値には負数を禁止する validation はない。`int` として受理され、一意性だけが検査される。
- persisted member が 0 個なら DTO / converter / persisted repository / formatter は生成されない。

### 非保証

- DTO field 順序の人間可読性や index の連番性は保証しない。

## 9. ValueObject contract

### 仕様

ある member 型が ValueObject として flatten される条件は以下。

1. 型に `[ToPrimitive]` 付き method がちょうど 1 個ある
2. その method は instance method であり、引数を持たない
3. `[FromPrimitive]` は次のどちらか一方だけ存在する
   - ちょうど 1 個の static factory method
   - ちょうど 1 個の constructor
4. `[FromPrimitive]` 側の parameter 列は `[ToPrimitive]` の戻り shape と型一致する

`[ToPrimitive]` の戻り値が tuple なら DTO 上では tuple 要素ごとに分解される。  
tuple でない場合は 1 field として DTO に保持される。

### DTO field naming rule

非 ValueObject member:

- member 名を先頭 `_` 除去
- PascalCase 化
- C# keyword なら `@` escape

例:

| Entity member | DTO field |
| --- | --- |
| `_id` | `Id` |
| `name` | `Name` |
| `class` | `@class` |

tuple ValueObject member:

- `<MemberPascalCase>_<TupleElementName>`

tuple element 名が空なら `Item1`, `Item2`, ... を使う。

例:

| Entity member | `ToPrimitive` return | DTO fields |
| --- | --- | --- |
| `Position` | `(int x, int y)` | `Position_x`, `Position_y` |
| `Color` | `(byte, byte, byte)` | `Color_Item1`, `Color_Item2`, `Color_Item3` |

### 実装由来の注記

- static factory の場合、`FromDto` は `TypeName.Factory(arg1, arg2, ...)` を使う。
- constructor の場合、`FromDto` は `new TypeName(arg1, arg2, ...)` を使う。
- shape 判定では parameter 名は見ない。parameter 型列だけを見る。
- `[ToPrimitive]` が存在しない型は ValueObject ではなく、そのまま DTO field 型になる。

### 非保証

- custom serializer hook や nested DTO flattening policy は提供しない。

## 10. Generator diagnostics

### 仕様

Analyzer は以下の diagnostics を `Error` として報告し、Entity の生成を止める。

| ID | Title | 発火条件 | 修正指針 |
| --- | --- | --- | --- |
| `LILJAREPO001` | Entity must be partial | `[Entity]` class が `partial` でない | `partial class` にする |
| `LILJAREPO002` | Generic entity is not supported | `[Entity]` class が generic | 型引数を外すか対象外にする |
| `LILJAREPO003` | Static member is not supported | `[Key]` または `[Persist]` が static member に付いている | instance member へ移す |
| `LILJAREPO004` | Only auto-properties are supported | `[Key]` / `[Persist]` 付き property が auto-property でない、indexer、custom accessor body、expression body を持つ | field にするか純粋な auto-property にする |
| `LILJAREPO005` | Persist index must be unique | 同一 Entity 内で `Persist(index)` が重複 | index を一意に振り直す |
| `LILJAREPO006` | Persisted keys must also be persisted | persisted entity の `[Key]` member に `[Persist]` がない | key に `[Persist]` を追加する |
| `LILJAREPO007` | Invalid [ToPrimitive] definition | `[ToPrimitive]` method が 0 個または複数、static、引数付き | 1 個の instance no-arg method に整理する |
| `LILJAREPO008` | Invalid [FromPrimitive] definition | `[FromPrimitive]` constructor/static factory が shape 不一致、複数定義、return type 不一致 | `[ToPrimitive]` の戻り shape と一致する 1 定義にする |

### 実装由来の注記

- `LILJAREPO008` は ValueObject member を持つ Entity 上で報告される。ValueObject 型単体に analyzer を掛けているわけではない。
- 1 Entity に複数 error があれば複数報告されうる。

### 非保証

- Warning レベルの soft guidance は提供しない。診断は現状すべて Error である。

## 11. 生成物の全体マトリクス

### 仕様

Entity 条件ごとの生成物は以下。

| 条件 | 生成物 |
| --- | --- |
| `[Entity]` 全般 | `I{Entity}Repository`, `InMemory{Entity}Repository` |
| key を持つ | `{Entity}.GetKey` |
| persisted (`[Persist]` あり) | `{Entity}Dto`, `{Entity}.ToDto`, `{Entity}.FromDto`, `{Entity}StorageEnvelope`, `Json{Entity}Repository` |
| persisted かつ key あり | `{Entity}.GetKeyFromDto` |
| persisted かつ MessagePack 参照あり | `{Entity}DtoFormatter`, `{Entity}StorageEnvelopeFormatter`, `MessagePack{Entity}Repository` |

### 実装由来の注記

- MessagePack 判定は compilation 全体に `MessagePack.Formatters.IMessagePackFormatter<T>` が存在するかどうかだけで決まる。
- persisted でない Entity に formatter は生成されない。

### 非保証

- generator output の physical file count や add-source order は public contract ではない。ただし named output は現行実装と一致させる必要がある。

## 12. Namespace, type name, hint name, storage identifier 規約

### 仕様

Entity の namespace を `N`、class 名を `C` としたとき、生成規約は次のとおり。

### 12.1 StorageIdentifier

- `N` が空でなければ `N.C`
- global namespace なら `C`

### 12.2 Generated namespace

| 種類 | Namespace |
| --- | --- |
| repository | `N.Repositories` または `Repositories` |
| DTO | `Lilja.Repository.Generated.Dtos.N` または `Lilja.Repository.Generated.Dtos` |
| storage envelope | `Lilja.Repository.Generated.Storage.N` または `Lilja.Repository.Generated.Storage` |
| formatter | `Lilja.Repository.Generated.Formatters.N` または `Lilja.Repository.Generated.Formatters` |

### 12.3 Generated type name

| 種類 | 型名 |
| --- | --- |
| interface | `I{C}Repository` |
| in-memory repository | `InMemory{C}Repository` |
| json repository | `Json{C}Repository` |
| messagepack repository | `MessagePack{C}Repository` |
| DTO | `{C}Dto` |
| storage envelope | `{C}StorageEnvelope` |
| dto formatter | `{C}DtoFormatter` |
| storage envelope formatter | `{C}StorageEnvelopeFormatter` |

### 12.4 AddSource hint name

- namespace あり: `{StorageIdentifier}.{FileName}`
- global namespace: `{FileName}`

主要 file name:

| 生成物 | FileName |
| --- | --- |
| repository interface | `I{C}Repository.g.cs` |
| in-memory repository | `InMemory{C}Repository.g.cs` |
| json repository | `Json{C}Repository.g.cs` |
| messagepack repository | `MessagePack{C}Repository.g.cs` |
| DTO | `{C}Dto.g.cs` |
| storage envelope | `{C}StorageEnvelope.g.cs` |
| DTO formatter | `{C}DtoFormatter.g.cs` |
| storage envelope formatter | `{C}StorageEnvelopeFormatter.g.cs` |
| converter partial | `{C}.Converter.g.cs` |
| key accessor partial | `{C}.KeyAccessor.g.cs` |

### 実装由来の注記

- same-named entity が別 namespace にある場合、hint 名と storage file 名は `StorageIdentifier` により衝突回避される。

### 非保証

- global namespace entity の hint name に storage identifier prefix を付けることは現行実装では行わない。

## 13. 生成 interface 契約

### 仕様

各 Entity について public interface `I{Entity}Repository` が生成される。

keyed entity:

```csharp
public interface IItemRepository
{
    UniTask InitializeAsync(CancellationToken ct = default);
    Item? Read(IReadOnlyTx tx, TKey key);
    void Create(IReadWriteTx tx, Item entity);
    void Update(IReadWriteTx tx, Item entity);
    void Delete(IReadWriteTx tx, TKey key);
    IReadOnlyList<Item> All(IReadOnlyTx tx);
}
```

singleton entity:

```csharp
public interface ISettingsRepository
{
    UniTask InitializeAsync(CancellationToken ct = default);
    Settings? Read(IReadOnlyTx tx);
    void Create(IReadWriteTx tx, Settings entity);
    void Update(IReadWriteTx tx, Settings entity);
    void Delete(IReadWriteTx tx);
}
```

### 実装由来の注記

- in-memory と persisted の両方が同じ interface を実装する。
- `InitializeAsync` は in-memory でも必ず含まれる。

### 非保証

- query API、pagination、predicate search、async CRUD、bulk operation は生成されない。

## 14. 生成 repository 契約

### 14.1 InMemory repository

#### 仕様

keyed entity では `InMemoryKeyedRepositoryBase<TEntity, TKey>` を継承し、`GetKey(entity)` を override する。  
singleton entity では `InMemorySingletonRepositoryBase<TEntity>` を継承する。

constructor は parameterless である。  
`UNITY_EDITOR` 時は constructor 内で `TrackRepository(RepositoryTracker.RepositoryType.InMemory)` が呼ばれる。

#### 実装由来の注記

- singleton in-memory repository は追加 member を生成しない。
- keyed in-memory repository の `GetKey(entity)` は generated `Entity.GetKey(entity)` を呼ぶだけである。

#### 非保証

- repository constructor に dependency injection parameter を持たせる拡張は現行生成にはない。

### 14.2 Json repository

#### 仕様

keyed entity では `PersistedKeyedRepositoryBase<TEntity, TKey, TDto>`、singleton entity では `PersistedSingletonRepositoryBase<TEntity, TDto>` を継承する。

constructor は parameterless であり、base constructor へ以下の file path を渡す。

```csharp
Path.Combine(Application.persistentDataPath, "{StorageIdentifier}.json")
```

`UNITY_EDITOR` 時は constructor 内で `TrackRepository(RepositoryTracker.RepositoryType.Json)` を呼ぶ。

load/save contract:

- keyed: envelope の `Items` を load/save
- singleton: envelope の `HasValue` と `Item` を load/save
- 実際の JSON serializer は `UnityEngine.JsonUtility`

#### 実装由来の注記

- load/save 本体は `UniTask.RunOnThreadPool` 上で実行される。
- load 時は `File.Exists(FilePath)` が false なら `null` を返す。
- JSON file が存在しても空白だけなら `FromJson<T>` を呼ばず `null` 扱いになる。

#### 非保証

- pretty print は行わない。`JsonUtility.ToJson(envelope, false)` が使われる。

### 14.3 MessagePack repository

#### 仕様

生成条件は MessagePack formatter interface 参照が compilation に存在すること。

constructor は parameterless であり、base constructor へ以下の file path を渡す。

```csharp
Path.Combine(Application.persistentDataPath, "{StorageIdentifier}.msgpack")
```

constructor 内で resolver を構築し、private readonly `_options` に保持する。

```csharp
var resolver = CompositeResolver.Create(
    new IMessagePackFormatter[] { new {Entity}StorageEnvelopeFormatter(), new {Entity}DtoFormatter() },
    new IFormatterResolver[] { StandardResolver.Instance });
_options = MessagePackSerializerOptions.Standard.WithResolver(resolver);
```

load/save contract:

- load: `MessagePackSerializer.Deserialize<{Entity}StorageEnvelope>(bytes, _options)`
- save: `MessagePackSerializer.Serialize(envelope, _options)`

#### 実装由来の注記

- `TrackRepository(RepositoryTracker.RepositoryType.MessagePack)` が `UNITY_EDITOR` 時のみ constructor で呼ばれる。

#### 非保証

- user custom resolver の差し込みは現行生成では行わない。

## 15. DTO, converter, key accessor, formatter, envelope の生成契約

### 15.1 DTO

#### 仕様

persisted entity ごとに `[Serializable] public sealed class {Entity}Dto` が生成される。  
DTO member はすべて public field であり、初期値は `default!` で与えられる。

例:

```csharp
[Serializable]
public sealed class ItemDto
{
    public int Id = default!;
    public string Name = default!;
    public int Position_x = default!;
    public int Position_y = default!;
}
```

#### 実装由来の注記

- property ではなく field である。
- ValueObject flatten 時は 1 tuple 要素ごとに 1 field を生やす。

#### 非保証

- JSON field name customisation や MessagePack key attribute は付与しない。

### 15.2 Converter partial

#### 仕様

Entity partial class へ以下が internal static で追加される。

1. `ToDto(Entity entity)`
2. `FromDto(Dto dto)`

persisted member の型列と一致する instance constructor が Entity に存在しない場合、generator は private constructor も追加する。

constructor 生成条件:

- persisted member が 1 個以上ある
- 既存の instance constructor の中に「persisted member 数と同数、かつ各 parameter 型が persisted member の sort 済み型列と一致するもの」が存在しない

#### 実装由来の注記

- constructor 一致判定では parameter 名を使わない。型列だけを見る。
- 生成 private constructor の parameter 名は member 名から camelCase 化して作る。
- `FromDto` は常に `new Entity(...)` を使う。`FromPrimitive` static factory を使うのは ValueObject 復元時だけである。

#### 非保証

- object initializer、factory method、builder pattern に対応する拡張はしない。

### 15.3 Key accessor partial

#### 仕様

keyed entity には partial class へ internal static method が追加される。

1. `GetKey(Entity entity)`
2. persisted keyed entity の場合は `GetKeyFromDto(Dto dto)` も追加

single key なら単一値を返す。  
composite key なら tuple を返す。

#### 実装由来の注記

- `GetKeyFromDto` は DTO key 表示や persisted keyed repository の key extraction に用いられる。

#### 非保証

- public static helper として公開はしない。generated method は internal。

### 15.4 Storage envelope

#### 仕様

storage envelope は `[Serializable] internal sealed class {Entity}StorageEnvelope` である。

keyed:

```csharp
internal sealed class ItemStorageEnvelope
{
    public List<ItemDto> Items = new List<ItemDto>();
}
```

singleton:

```csharp
internal sealed class SettingsStorageEnvelope
{
    public bool HasValue;
    public SettingsDto? Item;
}
```

#### 実装由来の注記

- keyed envelope は常に `Items` list shape を使う。
- singleton envelope は `HasValue` により「値なし」と「null item field の偶発状態」を区別する。

#### 非保証

- keyed repository で singleton shape を使うこと、またはその逆はしない。

### 15.5 MessagePack formatter

#### 仕様

persisted entity かつ MessagePack 参照ありの場合、formatter namespace に以下が生成される。

1. `public sealed class {Entity}DtoFormatter : IMessagePackFormatter<{Entity}Dto>`
2. `internal sealed class {Entity}StorageEnvelopeFormatter : IMessagePackFormatter<{Entity}StorageEnvelope>`

DTO formatter:

- `null` は `writer.WriteNil()`
- non-null は array header を書き、flattened field 順に serialize
- deserialize 時は array 長が不足している field を default のまま残す
- array 長が過剰なら残りを `reader.Skip()` で無視

StorageEnvelope formatter:

- keyed: header 長 1、`Items` のみ serialize
- singleton: header 長 2、`HasValue`, `Item` の順で serialize

#### 実装由来の注記

- DTO formatter は `IMessagePackFormatter<TDto>` 実装で public。
- envelope formatter は internal。
- deserialize 時に nil を読むと `null!` を返す。

#### 非保証

- `[MessagePackObject]` ベースの attribute 生成や map encoding への切り替えは行わない。

## 16. 利用方法

### 16.1 Canonical usage: keyed in-memory

#### 仕様

```csharp
using Demo.Repositories;
using Lilja.Repository;

var txManager = new TxManager();
var repository = new InMemoryItemRepository();

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

### 16.2 Canonical usage: keyed JSON persisted

#### 仕様

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
    UnityEngine.Debug.Log(item?.Name);
});
```

### 16.3 Canonical usage: singleton persisted

#### 仕様

```csharp
var txManager = new TxManager();
var repository = new JsonSettingsRepository();

await repository.InitializeAsync();

await txManager.BeginRWTransactionAsync(tx =>
{
    repository.Create(tx, new Settings(10));
});

txManager.BeginROTransaction(tx =>
{
    var settings = repository.Read(tx);
    UnityEngine.Debug.Log(settings?.Volume);
});
```

### 16.4 Canonical usage: optional MessagePack

#### 仕様

```csharp
var txManager = new TxManager();
var repository = new MessagePackItemRepository();

await repository.InitializeAsync();
```

MessagePack 参照が compilation に無い場合、この型は生成されない。

### 実装由来の注記

- in-memory repository に対しても interface 統一のため `InitializeAsync` が存在する。
- persisted repository では `InitializeAsync` 呼び出しが必須。
- `Read` は「存在しない」を表現するため nullable を返す。

### 非保証

- 生成 repository を使わず、`RepositoryTx` 相当 helper を直接利用する custom repository はサポート対象外。

## 17. Strict CRUD contract

### 仕様

Create / Update / Delete の存在判定は committed state ではなく current staged view 基準で行う。

keyed repository:

- `Create`: current staged view に key が既に存在すると `InvalidOperationException`
- `Update`: current staged view に key が存在しないと `InvalidOperationException`
- `Delete`: current staged view に key が存在しないと `InvalidOperationException`

singleton repository:

- `Create`: current staged view に既に値があると `InvalidOperationException`
- `Update`: current staged view に値がないと `InvalidOperationException`
- `Delete`: current staged view に値がないと `InvalidOperationException`

exception message には operation 名、repository type 名、必要に応じて key が含まれる。

### 同一 transaction 内の振る舞い

#### 仕様

| 操作列 | 結果 |
| --- | --- |
| `Create -> Update` | 成功。更新後の staged value が commit される |
| `Delete -> Create` | 成功。再作成後の value が commit される |
| `Delete -> Update` | `Update` が失敗。先行 `Delete` は有効のまま |
| `Create -> Create` | 2 回目 `Create` が失敗。1 回目の staged create は有効のまま |
| `Delete -> Delete` | 2 回目 `Delete` が失敗。1 回目の staged delete は有効のまま |
| `Create -> Update -> Delete` | 最終状態は存在しない |

#### 実装由来の注記

- CRUD 失敗が delegate 外へ伝播しない限り transaction 全体は rollback されない。呼び出し側が例外を transaction delegate 内で catch し、そのまま delegate を正常終了させれば、先行 staged change は commit されうる。
- delegate 自体が例外で脱出した場合は `TxManager` が rollback する。

#### 非保証

- 失敗した CRUD 後に transaction を poison 状態にする仕組みは無い。

## 18. Transaction model

### 18.1 読み取りモデル

#### 仕様

- RO transaction は committed state の snapshot を読む。
- 同一 RO transaction 内では、外部で commit が起きても読み値は変わらない。
- RW transaction 内の read はその transaction の staged state を反映する。

### 18.2 書き込みモデル

#### 仕様

- writer は常に 1 つだけ許可される。
- RW transaction 中の変更は transaction-local に staged される。
- commit 時に初めて committed state が差し替わる。

### 18.3 Commit publish sequence

#### 仕様

RW transaction commit の順序は以下。

1. `PrepareCommitAsync`
2. reader admission を閉じる
3. 既存 reader が drain するまで待機
4. `ApplyCommit`
5. reader admission を reopen

`PrepareCommitAsync` では各 participant が persistence を完了させる。  
`ApplyCommit` は成功した prepared state を committed state へ反映する。

### 18.4 Rollback sequence

#### 仕様

- rollback は staged state を破棄するだけで、committed state は触らない。
- persist が失敗した場合も committed state は差し替えない。

### 18.5 Cancellation

#### 仕様

- RW transaction の cancellation token は writer lock 待ち、prepare、persist に使われる。
- publish 開始後、すなわち reader admission を閉じてからの commit には cancellation が割り込まない。
- したがって publish 開始後に token が cancel されても commit は継続し、成功すれば committed state は更新される。

### 実装由来の注記

- RO snapshot は repository instance ごとに内部 store に保持される。
- RW staged state も repository instance ごとに保持される。
- participant が存在しない RW transaction は prepare/publish を行わず即終了する。

### 非保証

- serializable isolation、repeatable read 以上の名前付き isolation level を公開 API として提供してはいない。実際の挙動は「single-writer + snapshot-reader」で表現するのが正確である。

## 19. 内部 state model

### 仕様

keyed repository は overlay state を使う。  
overlay state は以下の 3 部分から current view を形成する。

1. committed dictionary
2. upsert dictionary
3. deleted key set

singleton repository は reference state を使う。  
current staged reference が直接 1 値保持される。

### 実装由来の注記

keyed overlay の read priority は以下。

1. upsert dictionary
2. deleted key set
3. committed dictionary

overlay の `Count` は committed count を基準に deleted / new upsert を加減算して計算する。  
commit prepare 時には overlay を materialize して完全 dictionary を作り、その完全 dictionary を persist と committed apply の両方に使う。

### 非保証

- diff log、change event、undo stack は保持しない。

## 20. 永続化 contract

### 20.1 ファイル配置

#### 仕様

- 保存先は `Application.persistentDataPath` 直下
- ファイル名は `{StorageIdentifier}.json` または `{StorageIdentifier}.msgpack`
- `1 repository = 1 file`

例:

| Entity | StorageIdentifier | JSON path |
| --- | --- | --- |
| `Demo.Item` | `Demo.Item` | `Application.persistentDataPath/Demo.Item.json` |
| `Demo.Profile.Settings` | `Demo.Profile.Settings` | `Application.persistentDataPath/Demo.Profile.Settings.json` |

### 20.2 初期化

#### 仕様

- persisted repository は使用前に `InitializeAsync` が必須
- file 未存在時:
  - keyed: empty dictionary
  - singleton: no value
- `InitializeAsync` は concurrent-safe
- 初期化失敗後は、呼び出し側が原因を解消して再度 `InitializeAsync` を呼べる

#### 実装由来の注記

- `SemaphoreSlim _initializationGate` により初期化が 1 回に直列化される。
- 初期化中に例外が出た場合、`_initialized` は `false` のまま残る。

### 20.3 Keyed persisted load/save shape

#### 仕様

ロード時:

1. file を読む
2. envelope を deserialize
3. `Items` を順に列挙
4. `GetKeyFromDto(dto)` を key に dictionary 化

セーブ時:

1. committed/staged merged state を dictionary として得る
2. `state.Values` を list 化
3. `Items` に入れた envelope を serialize
4. `AtomicFileWriter` で置換

#### 実装由来の注記

- load 時に file 内で同一 key DTO が複数存在した場合、後勝ちで dictionary に残る。
- save 時の item 順は dictionary enumeration order に従う。コード上で明示 sort はしない。

### 20.4 Singleton persisted load/save shape

#### 仕様

- `HasValue == false` のとき repository の read 結果は `null`
- delete 済み singleton でも file 自体は削除せず、`HasValue = false, Item = null` の envelope を保存する

### 20.5 JSON specifics

#### 仕様

- serializer は `UnityEngine.JsonUtility`
- envelope 全体を serialize / deserialize する
- keyed file preview は JSON string として人間可読

### 20.6 MessagePack specifics

#### 仕様

- serializer は `MessagePackSerializer`
- generated formatter と `StandardResolver` の composite resolver を使う
- DTO は flattened field array
- keyed envelope は array length 1、singleton envelope は array length 2

### 実装由来の注記

- load/save は thread pool 上で実行される。
- save 前にも load 前にも `ct.ThrowIfCancellationRequested()` を呼ぶ。

### 非保証

- file lock coordination、cross-process consistency、journal file 管理はしない。

## 21. Runtime duplicate-instance diagnostics

### 仕様

正常 runtime path は以下を前提とする。

1. live な `TxManager` は通常 1 個
2. persisted repository は通常「1 file path あたり 1 live instance」

### 実装由来の注記

- `RuntimeInstanceMonitor` は internal。
- `UNITY_EDITOR` または `DEVELOPMENT_BUILD` でのみ warning を出す。
- duplicate は exception ではなく `Debug.LogWarning`。
- persisted repository duplicate 判定キーは `repositoryType.FullName + normalized FilePath`。

### 非保証

- duplicate runtime instance が自動修復されることはない。

## 22. Editor tooling contract

### 22.1 RepositoryViewer

#### 仕様

EditorWindow `RepositoryViewer` はメニュー `Lilja/Repository/Repository Viewer` から開く。

主機能:

1. `InMemory` / `Json` / `MessagePack` 切り替え
2. `AutoReload` toggle と `Reload`
3. play mode 中の live repository 表示
4. edit mode 中の persisted file 表示
5. `OpenDirectory`
6. `A1/A2/B` 3-pane layout

ヘッダには以下を左から順に表示する。

1. backend dropdown
2. `AutoReload` toggle
3. `Reload` button
4. `OpenDirectory` button

`OpenDirectory` button は常時表示されるが、`InMemory` 選択時は disabled。
`AutoReload` の初期値は ON。

content area は以下の 3 pane で構成する。

- `A`: top area
- `B`: bottom preview area
- `A` 内はさらに `A1` / `A2` に分かれる

`A/B` と `A1/A2` の divider はどちらも drag でサイズ変更可能で、divider state は `viewDataKey` により復元対象とする。

各 pane の責務:

- `A1`: repository selection list
- `A2`: selected repository の record list
- `B`: selected record preview

`A1` / `A2` は `ListView` ベースで、各要素は button として描画する。

### 22.2 データソース

#### 仕様

- `Application.isPlaying == true` のとき:
  - `RepositoryTracker.GetAll(type)` から live repository を読む
- `Application.isPlaying == false` のとき:
  - `InMemory` は表示しない
  - `Json` / `MessagePack` は `Application.persistentDataPath` の file 群から読む

Viewer UI と data loading は分離し、data source 層は UI 非依存の snapshot model を返す。

最低限の snapshot 単位:

- repository snapshot
- record snapshot

repository selection は stable id で保持し、reload 後は可能な限り選択中 repository / record を復元する。

### 22.3 MessagePack optionality

#### 仕様

- MessagePack runtime 型が reflection で見つからない場合、Editor 側は MessagePack を compile-time 必須にしない
- `MessagePackReflectionBridge.IsAvailable == false` の場合:
  - dropdown に `MessagePack` を表示しない
  - MessagePack persisted file 読み込みを行わない

### 22.4 Persisted file type resolution

#### 仕様

- DTO / envelope / formatter type は AppDomain loaded assemblies から type name で探索される
- same-named entity は storage identifier で区別される
- keyed DTO の item key 表示には generated `GetKeyFromDto` があればそれを優先する
- fallback key 候補は `Id`, `Key`, `Name`, `id`, `key`, `name`
- どれも取れなければ `Item {index}` を表示する

live repository の keyed item label は、repository 側の non-public `GetKey(entity)` を reflection で優先し、取得できない場合のみ fallback key 候補へフォールバックする。

### 22.5 Unknown / error handling

#### 仕様

- persisted file から対応 metadata を解決できない場合:
  - `Type = "Unknown"`
  - JSON は raw preview を表示
  - MessagePack は `Binary Data` と表示
- deserialize 失敗時:
  - warning を出し
  - `Type = "Error"`
  - detail には error message を表示

preview pane は selected record を read-only multiline text として表示する。

- `string` / raw text はそのまま表示
- serializable object は `JsonUtility.ToJson(value, true)` を優先
- `JsonUtility` で整形できない場合は collection / object を再帰的に JSON-like text へ整形
- selected record が存在しない場合は empty state を表示

`AutoReload` が ON のとき、Viewer は約 1.5 秒ごとに再読込を行う。以下は poll を待たず即時 refresh 対象:

- backend 切り替え
- `Reload`
- play mode 遷移
- window focus 復帰

### 実装由来の注記

- live repository 読み取り時、Viewer は snapshot-aware transaction を作らず、空の `IReadOnlyTx` 実装を使う。そのため表示対象は current committed state であり、RO snapshot semantics を再現するものではない。
- repository / record list button の preview text は短縮表示される。
- singleton repository に値が存在しない場合、record list は空表示になる。

### 非保証

- Viewer UI の見た目、column 配置、toolbar の細かな挙動は public contract ではない。

## 23. MessagePack reflection bridge contract

### 仕様

Editor は MessagePack に compile-time 依存しない。  
`MessagePackReflectionBridge` は runtime reflection で以下の型を解決できた場合のみ有効になる。

1. `MessagePackSerializer`
2. `MessagePackSerializerOptions`
3. `CompositeResolver`
4. `StandardResolver`
5. `IMessagePackFormatter`
6. `IFormatterResolver`

`CreateOptions(formatterTypes...)` は formatter instance 群と `StandardResolver.Instance` から resolver を組み立て、`MessagePackSerializerOptions.Standard.WithResolver(...)` を返す。  
`Deserialize(bytes, targetType, options)` は generic `Deserialize<T>` を reflection で呼ぶ。

### 実装由来の注記

- formatter type 解決に失敗したときは fallback で標準 options を返しうる。
- reflection 例外が出てもできるだけ null / 標準 options にフォールバックする設計である。

### 非保証

- MessagePack API の将来変更に対する forward-compatibility は保証しない。

## 24. 実装再現に必要なサンプル

### 24.1 Keyed persisted entity

```csharp
using Lilja.Repository;

namespace Demo;

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

この Entity から以下が生成される。

| 生成物 | 内容 |
| --- | --- |
| `Demo.Repositories.IItemRepository` | keyed repository interface |
| `Demo.Repositories.InMemoryItemRepository` | in-memory keyed repository |
| `Demo.Repositories.JsonItemRepository` | JSON persisted keyed repository |
| `Demo.Repositories.MessagePackItemRepository` | MessagePack persisted keyed repository。MessagePack 参照時のみ |
| `Lilja.Repository.Generated.Dtos.Demo.ItemDto` | `Id`, `Name`, `Position_x`, `Position_y` |
| `Lilja.Repository.Generated.Storage.Demo.ItemStorageEnvelope` | `Items: List<ItemDto>` |
| `Lilja.Repository.Generated.Formatters.Demo.ItemDtoFormatter` | DTO formatter |
| `Lilja.Repository.Generated.Formatters.Demo.ItemStorageEnvelopeFormatter` | envelope formatter |
| `Demo.Item.ToDto/FromDto/GetKey/GetKeyFromDto` | partial helper |

### 24.2 Singleton persisted entity

```csharp
[Entity]
public partial class Settings
{
    [Persist(0)]
    public int Volume { get; }

    public Settings(int volume)
    {
        Volume = volume;
    }
}
```

この Entity では `HasValue` / `Item` shape の envelope が生成される。

## 25. 永続化フォーマット例

### 25.1 JSON keyed envelope 例

```json
{
  "Items": [
    {
      "Id": 1,
      "Name": "Potion",
      "Position_x": 10,
      "Position_y": 20
    },
    {
      "Id": 2,
      "Name": "Sword",
      "Position_x": 30,
      "Position_y": 40
    }
  ]
}
```

### 25.2 JSON singleton envelope 例

```json
{
  "HasValue": true,
  "Item": {
    "Volume": 10
  }
}
```

delete 済み singleton の保存例:

```json
{
  "HasValue": false,
  "Item": null
}
```

### 25.3 MessagePack keyed envelope shape

MessagePack keyed envelope は array 長 1 の shape を取る。

1. `Items: List<Dto>`

DTO 自体は flattened field 数と同じ array 長を持つ。

### 25.4 MessagePack singleton envelope shape

MessagePack singleton envelope は array 長 2 の shape を取る。

1. `HasValue`
2. `Item`

## 26. Public runtime surface snapshot

### 仕様

現行 test suite が固定している public runtime surface は実質的に以下と等価である。

```text
type Lilja.Repository.AtomicFileWriter
method System.Void Lilja.Repository.AtomicFileWriter.DeleteIfExists(System.String filePath)
method System.Void Lilja.Repository.AtomicFileWriter.WriteAllBytes(System.String filePath, System.Byte[] bytes)
method System.Void Lilja.Repository.AtomicFileWriter.WriteAllText(System.String filePath, System.String content)
type Lilja.Repository.Diagnostics.RepositoryTracker
method System.Collections.Generic.IEnumerable<System.Object> Lilja.Repository.Diagnostics.RepositoryTracker.GetAll(Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType type)
method System.Void Lilja.Repository.Diagnostics.RepositoryTracker.Track(System.Object repository, Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType type)
type Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType
field Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType.InMemory
field Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType.Json
field Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType.MessagePack
type Lilja.Repository.EntityAttribute
ctor Lilja.Repository.EntityAttribute()
type Lilja.Repository.FromPrimitiveAttribute
ctor Lilja.Repository.FromPrimitiveAttribute()
type Lilja.Repository.IReadOnlyTx
type Lilja.Repository.IReadWriteTx
type Lilja.Repository.InMemoryKeyedRepositoryBase<TEntity, TKey>
method System.Collections.Generic.IReadOnlyList<TEntity> Lilja.Repository.InMemoryKeyedRepositoryBase<TEntity, TKey>.All(Lilja.Repository.IReadOnlyTx tx)
method System.Void Lilja.Repository.InMemoryKeyedRepositoryBase<TEntity, TKey>.Create(Lilja.Repository.IReadWriteTx tx, TEntity entity)
method System.Void Lilja.Repository.InMemoryKeyedRepositoryBase<TEntity, TKey>.Delete(Lilja.Repository.IReadWriteTx tx, TKey key)
method Cysharp.Threading.Tasks.UniTask Lilja.Repository.InMemoryKeyedRepositoryBase<TEntity, TKey>.InitializeAsync(System.Threading.CancellationToken ct)
method TEntity Lilja.Repository.InMemoryKeyedRepositoryBase<TEntity, TKey>.Read(Lilja.Repository.IReadOnlyTx tx, TKey key)
method System.Void Lilja.Repository.InMemoryKeyedRepositoryBase<TEntity, TKey>.Update(Lilja.Repository.IReadWriteTx tx, TEntity entity)
type Lilja.Repository.InMemorySingletonRepositoryBase<TEntity>
method System.Void Lilja.Repository.InMemorySingletonRepositoryBase<TEntity>.Create(Lilja.Repository.IReadWriteTx tx, TEntity entity)
method System.Void Lilja.Repository.InMemorySingletonRepositoryBase<TEntity>.Delete(Lilja.Repository.IReadWriteTx tx)
method Cysharp.Threading.Tasks.UniTask Lilja.Repository.InMemorySingletonRepositoryBase<TEntity>.InitializeAsync(System.Threading.CancellationToken ct)
method TEntity Lilja.Repository.InMemorySingletonRepositoryBase<TEntity>.Read(Lilja.Repository.IReadOnlyTx tx)
method System.Void Lilja.Repository.InMemorySingletonRepositoryBase<TEntity>.Update(Lilja.Repository.IReadWriteTx tx, TEntity entity)
type Lilja.Repository.KeyAttribute
ctor Lilja.Repository.KeyAttribute()
type Lilja.Repository.PersistAttribute
ctor Lilja.Repository.PersistAttribute(System.Int32 index)
property System.Int32 Lilja.Repository.PersistAttribute.Index { get; }
type Lilja.Repository.PersistedKeyedRepositoryBase<TEntity, TKey, TDto>
method System.Collections.Generic.IReadOnlyList<TEntity> Lilja.Repository.PersistedKeyedRepositoryBase<TEntity, TKey, TDto>.All(Lilja.Repository.IReadOnlyTx tx)
method System.Void Lilja.Repository.PersistedKeyedRepositoryBase<TEntity, TKey, TDto>.Create(Lilja.Repository.IReadWriteTx tx, TEntity entity)
method System.Void Lilja.Repository.PersistedKeyedRepositoryBase<TEntity, TKey, TDto>.Delete(Lilja.Repository.IReadWriteTx tx, TKey key)
method Cysharp.Threading.Tasks.UniTask Lilja.Repository.PersistedKeyedRepositoryBase<TEntity, TKey, TDto>.InitializeAsync(System.Threading.CancellationToken ct)
method TEntity Lilja.Repository.PersistedKeyedRepositoryBase<TEntity, TKey, TDto>.Read(Lilja.Repository.IReadOnlyTx tx, TKey key)
method System.Void Lilja.Repository.PersistedKeyedRepositoryBase<TEntity, TKey, TDto>.Update(Lilja.Repository.IReadWriteTx tx, TEntity entity)
type Lilja.Repository.PersistedSingletonRepositoryBase<TEntity, TDto>
method System.Void Lilja.Repository.PersistedSingletonRepositoryBase<TEntity, TDto>.Create(Lilja.Repository.IReadWriteTx tx, TEntity entity)
method System.Void Lilja.Repository.PersistedSingletonRepositoryBase<TEntity, TDto>.Delete(Lilja.Repository.IReadWriteTx tx)
method Cysharp.Threading.Tasks.UniTask Lilja.Repository.PersistedSingletonRepositoryBase<TEntity, TDto>.InitializeAsync(System.Threading.CancellationToken ct)
method TEntity Lilja.Repository.PersistedSingletonRepositoryBase<TEntity, TDto>.Read(Lilja.Repository.IReadOnlyTx tx)
method System.Void Lilja.Repository.PersistedSingletonRepositoryBase<TEntity, TDto>.Update(Lilja.Repository.IReadWriteTx tx, TEntity entity)
type Lilja.Repository.ToPrimitiveAttribute
ctor Lilja.Repository.ToPrimitiveAttribute()
type Lilja.Repository.TxManager
ctor Lilja.Repository.TxManager()
method System.Void Lilja.Repository.TxManager.BeginROTransaction(System.Action<Lilja.Repository.IReadOnlyTx> action)
method Cysharp.Threading.Tasks.UniTask Lilja.Repository.TxManager.BeginROTransactionAsync(System.Func<Lilja.Repository.IReadOnlyTx, Cysharp.Threading.Tasks.UniTask> action)
method Cysharp.Threading.Tasks.UniTask Lilja.Repository.TxManager.BeginRWTransactionAsync(System.Action<Lilja.Repository.IReadWriteTx> action, System.Threading.CancellationToken ct)
method Cysharp.Threading.Tasks.UniTask Lilja.Repository.TxManager.BeginRWTransactionAsync(System.Func<Lilja.Repository.IReadWriteTx, Cysharp.Threading.Tasks.UniTask> action, System.Threading.CancellationToken ct)
```

### 実装由来の注記

- reflection snapshot は nullable annotation を完全には表現しない。ソース契約では `Read` は nullable reference を返す。

### 非保証

- internal helper を public に戻す変更は互換とみなさない。

## 27. 非保証事項一覧

### 非保証

以下は現行実装で明示的に保証していないか、意図的に public contract から外されている。

1. low-level transaction helper の直接利用
2. custom repository 実装の公式サポート
3. multi-file atomic commit
4. distributed transaction
5. nested transaction
6. query/filter/sort API の自動生成
7. DTO schema の後方互換ポリシー
8. JSON field order の安定ソート
9. dictionary enumeration order 依存の永続化順序
10. mutable entity を安全に扱うこと
11. Editor UI レイアウトの恒久固定
12. MessagePack API 変更への forward-compatibility
13. runtime build での `RepositoryTracker` 利用

## 28. 再実装チェックリスト

### 仕様

互換実装を作る場合、最低限以下を満たすこと。

1. public runtime surface が第 26 章と一致する
2. Analyzer diagnostics が第 10 章と一致する
3. generator output の namespace/type/hint/storage path 規約が第 12 章と一致する
4. strict CRUD と transaction publish sequence が第 17 章・第 18 章と一致する
5. keyed/singleton envelope shape が第 15 章・第 20 章と一致する
6. MessagePack optional generation が compilation 参照有無で切り替わる
7. same-named entity が namespace-qualified storage identifier で分離される
8. Editor が persisted file の Unknown/Error fallback を持つ

### 実装由来の注記

- 現行 test suite は上記の大半を回帰テストとして固定している。

### 非保証

- 文書に未記載の incidental behavior まで完全同一であることは要求しない。ただし公開 API、使用方法、内部挙動、保存形式、diagnostics の実質的互換は必要である。
