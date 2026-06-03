# Lilja.Persistence

Source Generator based persistence helpers for Unity game data.

Lilja.Persistence separates two concerns:

- `Repository`: async persistence boundary for save data, config data, and other root data.
- `KeyedStaging`: synchronous in-memory editing area for entities inside a root.

The usual flow is:

1. Define `partial` classes with `[Persistable]`.
2. Mark persisted members with `[Persist(index)]`.
3. Mark identity members with `[Key]`.
4. Mark root data with `[Persistable(IsRoot = true)]`.
5. Use generated staging and repositories.

## Install

Add the package to `Packages/manifest.json`.

```json
{
  "dependencies": {
    "com.kamahir0.lilja.persistence": "file:../../lilja/lilja-packages/lilja.persistence"
  }
}
```

The package depends on UniTask.

MessagePack repositories are generated only when a compatible MessagePack package is present in the project.

## Minimal Example

```csharp
using Lilja.Persistence;

namespace Game;

[Persistable]
public partial class Skill
{
    [Key]
    [Persist(0)]
    public string Id { get; }

    [Persist(1)]
    public int Level { get; private set; }

    public Skill(string id, int level)
    {
        Id = id;
        Level = level;
    }

    public void LevelUp()
    {
        Level++;
    }
}

[Persistable(IsRoot = true)]
public partial class GameSaveData
{
    [Key]
    [Persist(0)]
    public string SlotId { get; }

    [Persist(1)]
    public KeyedStaging<Skill, string> Skills { get; }

    [Persist(2)]
    public long Gold { get; set; }

    public GameSaveData(string slotId)
    {
        SlotId = slotId;
        Skills = new SkillStaging();
    }
}
```

This generates:

- `SkillDto`
- `SkillStaging`
- `GameSaveDataDto`
- `IGameSaveDataRepository`
- `JsonGameSaveDataRepository`
- `InMemoryGameSaveDataRepository`
- `MessagePackGameSaveDataRepository`, when MessagePack is available

Generated repository types are placed in a `.Repositories` namespace next to the root type.

```csharp
using Cysharp.Threading.Tasks;
using Game;
using Game.Repositories;
using Lilja.Persistence;

public sealed class SaveUseCase
{
    private readonly IGameSaveDataRepository _repository;

    public SaveUseCase(IGameSaveDataRepository repository)
    {
        _repository = repository;
    }

    public async UniTask RunAsync()
    {
        var save = new GameSaveData("slot-a");
        save.Skills.Update(new Skill("slash", 1));
        save.Gold = 100;

        await _repository.SaveAsync(save);

        var loaded = await _repository.LoadAsync("slot-a");
        var slash = loaded.Skills.GetOrDefault("slash");
    }
}
```

## Persistable Types

`[Persistable]` can be used for both entity-like objects and root data.

Rules:

- The type must be `partial`.
- Persisted members must have `[Persist(index)]`.
- `index` must be unique and non-negative.
- Key members must have both `[Key]` and `[Persist(index)]`.
- Static members are not supported.

```csharp
[Persistable]
public partial class Actor
{
    [Key]
    [Persist(0)]
    public string Id { get; }

    [Persist(1)]
    public int Level { get; private set; }
}
```

The generator adds:

- DTO conversion methods.
- `IKeyed<TKey>` implementation when `[Key]` exists.
- restore constructor used by generated `FromDto`.
- staging class when the type is keyed and not root.

## Staging

Use `KeyedStaging<TEntity, TKey>` when a root or entity owns a keyed collection of child entities.

```csharp
[Persistable]
public partial class Actor
{
    [Key]
    [Persist(0)]
    public string Id { get; }

    [Persist(1)]
    public KeyedStaging<Skill, string> Skills { get; }
}
```

Staging is synchronous and keeps DTOs as the authoritative in-memory state. It does not write files by itself.

```csharp
actor.Skills.Update(new Skill("slash", 1));

if (actor.Skills.TryGet("slash", out var skill))
{
    skill.LevelUp();
    actor.Skills.Update(skill);
}

var maybeSkill = actor.Skills.GetOrDefault("heal");
var allSkills = actor.Skills.All();
var deleted = actor.Skills.Delete("slash");
```

API:

```csharp
TEntity? GetOrDefault(TKey key);
bool TryGet(TKey key, out TEntity? entity);
bool Contains(TKey key);
IReadOnlyList<TEntity> All();
void Update(TEntity entity);
bool Delete(TKey key);
```

`Update` means add-or-replace. To persist staged state, save the root through a repository.

## Root Repositories

Add `IsRoot = true` to generate repositories.

```csharp
[Persistable(IsRoot = true)]
public partial class GameSaveData
{
    [Key]
    [Persist(0)]
    public string SlotId { get; }
}
```

For keyed roots, the generated interface has:

```csharp
UniTask<GameSaveData> LoadAsync(string key, CancellationToken ct = default);
UniTask<IReadOnlyList<GameSaveData>> LoadAllAsync(CancellationToken ct = default);
UniTask SaveAsync(GameSaveData data, CancellationToken ct = default);
bool Exists(string key);
```

For keyless roots, the generated interface has:

```csharp
UniTask<AppConfig> LoadAsync(CancellationToken ct = default);
UniTask SaveAsync(AppConfig data, CancellationToken ct = default);
```

Generated implementations:

- `Json{Name}Repository`: file-backed JSON repository.
- `InMemory{Name}Repository`: DTO snapshot repository for tests and temporary data.
- `MessagePack{Name}Repository`: file-backed MessagePack repository when MessagePack is available.

JSON and MessagePack repositories run file I/O on a worker thread. Unity path resolution is done before entering the worker thread.

## InMemory Repository

InMemory repositories implement the same generated interface as Json and MessagePack repositories.

```csharp
IGameSaveDataRepository repository = new InMemoryGameSaveDataRepository();
```

Keyed roots can receive initial values.

```csharp
var repository = new InMemoryGameSaveDataRepository(new[]
{
    new GameSaveData("slot-a"),
    new GameSaveData("slot-b"),
});
```

The repository stores DTO snapshots, not entity references. Mutating an object after `SaveAsync` does not mutate the repository state until `SaveAsync` is called again.

## Value Objects

Value objects can be persisted through primitive conversion methods.

```csharp
public readonly struct ActorId
{
    public ActorId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    [ToPrimitive]
    public string ToPrimitive()
    {
        return Value;
    }

    [FromPrimitive]
    public static ActorId FromPrimitive(string value)
    {
        return new ActorId(value);
    }
}
```

Then use the value object normally.

```csharp
[Persistable]
public partial class Actor
{
    [Key]
    [Persist(0)]
    public ActorId Id { get; }
}
```

`[FromPrimitive]` can be placed on a static factory method or a constructor that accepts the primitive value.

## Lists And Nested Persistables

Persisted members can include:

- primitive or serializable values
- value objects with `[ToPrimitive]` / `[FromPrimitive]`
- another `[Persistable]` object
- `List<T>` where `T` is `[Persistable]`
- `KeyedStaging<TEntity, TKey>`

`KeyedStaging` members are serialized as DTO lists inside the owning DTO.

## File Layout

Keyed JSON roots are saved as one file per key under:

```text
Application.persistentDataPath/{Full.Type.Name}/{encoded-key}.json
```

Keyless JSON roots are saved as:

```text
Application.persistentDataPath/{Full.Type.Name}.json
```

MessagePack repositories use `.msgpack` files.

`LoadAllAsync` does not restore keys from file names. It deserializes files and restores keys from DTO `[Key]` members.

## Notes

- This package is currently generator-first. Most user-facing code should be authored as `partial` persistable types.
- Staging is not persistence. Repository save/load is the persistence boundary.
- `GetOrDefault` returns `null` when an entity is missing. It does not create a default entity.
- `Delete` returns `false` when the key does not exist.
