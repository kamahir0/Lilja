using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Lilja.Repository.Analyzer;
using Xunit;

namespace Lilja.Repository.Analyzer.Test;

public sealed class GeneratorDriverTests
{
    [Fact]
    public void GeneratesRepositoriesForAutoPropertyEntity()
    {
        var result = RunGenerator(
            """
            using Lilja.Repository;

            namespace Demo;

            public readonly struct Point
            {
                [FromPrimitive]
                public Point(int x, int y)
                {
                }

                [ToPrimitive]
                public (int x, int y) ToPrimitive() => (1, 2);
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
                public Point Position { get; }

                public Item(int id, string name, Point position)
                {
                    Id = id;
                    Name = name;
                    Position = position;
                }
            }
            """,
            includeMessagePack: true);

        var hintNames = result.Results.Single().GeneratedSources.Select(source => source.HintName).ToArray();
        Assert.Contains(GetHintName("Demo.Item", "ItemDto.g.cs"), hintNames);
        Assert.Contains(GetHintName("Demo.Item", "ItemStorageEnvelope.g.cs"), hintNames);
        Assert.Contains(GetHintName("Demo.Item", "Item.Converter.g.cs"), hintNames);
        Assert.Contains(GetHintName("Demo.Item", "Item.KeyAccessor.g.cs"), hintNames);
        Assert.Contains(GetHintName("Demo.Item", "IItemRepository.g.cs"), hintNames);
        Assert.Contains(GetHintName("Demo.Item", "JsonItemRepository.g.cs"), hintNames);
        Assert.Contains(GetHintName("Demo.Item", "MessagePackItemRepository.g.cs"), hintNames);
        Assert.Contains(GetHintName("Demo.Item", "ItemStorageEnvelopeFormatter.g.cs"), hintNames);

        var generatedSources = result.Results.Single().GeneratedSources.ToDictionary(
            source => source.HintName,
            source => source.SourceText.ToString(),
            StringComparer.Ordinal);
        Assert.Contains("InMemoryKeyedRepositoryBase", generatedSources[GetHintName("Demo.Item", "InMemoryItemRepository.g.cs")], StringComparison.Ordinal);
        Assert.Contains("PersistedKeyedRepositoryBase", generatedSources[GetHintName("Demo.Item", "JsonItemRepository.g.cs")], StringComparison.Ordinal);
        Assert.Contains("PersistedKeyedRepositoryBase", generatedSources[GetHintName("Demo.Item", "MessagePackItemRepository.g.cs")], StringComparison.Ordinal);
        Assert.Contains(
            "public global::Lilja.Repository.Generated.Dtos.Demo.ItemDto Deserialize",
            generatedSources[GetHintName("Demo.Item", "ItemDtoFormatter.g.cs")],
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "public global::Lilja.Repository.Generated.Dtos.Demo.ItemDto? Deserialize",
            generatedSources[GetHintName("Demo.Item", "ItemDtoFormatter.g.cs")],
            StringComparison.Ordinal);
        Assert.Contains(
            "public global::Lilja.Repository.Generated.Storage.Demo.ItemStorageEnvelope Deserialize",
            generatedSources[GetHintName("Demo.Item", "ItemStorageEnvelopeFormatter.g.cs")],
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "public global::Lilja.Repository.Generated.Storage.Demo.ItemStorageEnvelope? Deserialize",
            generatedSources[GetHintName("Demo.Item", "ItemStorageEnvelopeFormatter.g.cs")],
            StringComparison.Ordinal);
        Assert.Contains(
            "Items = new global::System.Collections.Generic.List<global::Lilja.Repository.Generated.Dtos.Demo.ItemDto>()",
            generatedSources[GetHintName("Demo.Item", "ItemStorageEnvelope.g.cs")],
            StringComparison.Ordinal);
    }

    [Fact]
    public void SingletonPersistedEntity_KeepsSingletonStorageShape()
    {
        var result = RunGenerator(
            """
            using Lilja.Repository;

            namespace Demo;

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
            """,
            includeMessagePack: true);

        var generatedSources = result.Results.Single().GeneratedSources.ToDictionary(
            source => source.HintName,
            source => source.SourceText.ToString(),
            StringComparer.Ordinal);

        Assert.Contains("PersistedSingletonRepositoryBase", generatedSources[GetHintName("Demo.Settings", "JsonSettingsRepository.g.cs")], StringComparison.Ordinal);
        Assert.Contains("PersistedSingletonRepositoryBase", generatedSources[GetHintName("Demo.Settings", "MessagePackSettingsRepository.g.cs")], StringComparison.Ordinal);
        Assert.Contains("public bool HasValue;", generatedSources[GetHintName("Demo.Settings", "SettingsStorageEnvelope.g.cs")], StringComparison.Ordinal);
        Assert.Contains("public global::Lilja.Repository.Generated.Dtos.Demo.SettingsDto? Item;", generatedSources[GetHintName("Demo.Settings", "SettingsStorageEnvelope.g.cs")], StringComparison.Ordinal);
        Assert.DoesNotContain("Items = new global::System.Collections.Generic.List", generatedSources[GetHintName("Demo.Settings", "SettingsStorageEnvelope.g.cs")], StringComparison.Ordinal);
    }

    [Fact]
    public void NamespacedEntitiesWithSameClassName_GenerateUniqueHintsAndStoragePaths()
    {
        var result = RunGenerator(
            """
            using Lilja.Repository;

            namespace Demo.Inventory
            {
                [Entity]
                public partial class Item
                {
                    [Key]
                    [Persist(0)]
                    public int Id { get; }

                    public Item(int id)
                    {
                        Id = id;
                    }
                }
            }

            namespace Demo.Profile
            {
                [Entity]
                public partial class Item
                {
                    [Key]
                    [Persist(0)]
                    public int Id { get; }

                    public Item(int id)
                    {
                        Id = id;
                    }
                }
            }
            """,
            includeMessagePack: true);

        var generatedSources = result.Results.Single().GeneratedSources.ToDictionary(
            source => source.HintName,
            source => source.SourceText.ToString(),
            StringComparer.Ordinal);

        Assert.Contains(GetHintName("Demo.Inventory.Item", "JsonItemRepository.g.cs"), generatedSources.Keys);
        Assert.Contains(GetHintName("Demo.Profile.Item", "JsonItemRepository.g.cs"), generatedSources.Keys);
        Assert.Contains(
            "\"Demo.Inventory.Item.json\"",
            generatedSources[GetHintName("Demo.Inventory.Item", "JsonItemRepository.g.cs")],
            StringComparison.Ordinal);
        Assert.Contains(
            "\"Demo.Profile.Item.json\"",
            generatedSources[GetHintName("Demo.Profile.Item", "JsonItemRepository.g.cs")],
            StringComparison.Ordinal);
        Assert.Contains(
            "\"Demo.Inventory.Item.msgpack\"",
            generatedSources[GetHintName("Demo.Inventory.Item", "MessagePackItemRepository.g.cs")],
            StringComparison.Ordinal);
        Assert.Contains(
            "\"Demo.Profile.Item.msgpack\"",
            generatedSources[GetHintName("Demo.Profile.Item", "MessagePackItemRepository.g.cs")],
            StringComparison.Ordinal);
    }

    [Fact]
    public void SkipsMessagePackOutputsWhenReferenceIsMissing()
    {
        var result = RunGenerator(
            """
            using Lilja.Repository;

            namespace Demo;

            [Entity]
            public partial class Item
            {
                [Key]
                [Persist(0)]
                private readonly int _id;

                public Item(int id)
                {
                    _id = id;
                }
            }
            """,
            includeMessagePack: false);

        var hintNames = result.Results.Single().GeneratedSources.Select(source => source.HintName).ToArray();
        Assert.DoesNotContain(GetHintName("Demo.Item", "MessagePackItemRepository.g.cs"), hintNames);
        Assert.DoesNotContain(GetHintName("Demo.Item", "ItemDtoFormatter.g.cs"), hintNames);
        Assert.DoesNotContain(GetHintName("Demo.Item", "ItemStorageEnvelopeFormatter.g.cs"), hintNames);
    }

    [Theory]
    [InlineData(
        """
        using Lilja.Repository;
        namespace Demo;
        [Entity]
        public class Broken
        {
            [Persist(0)] private int _id;
        }
        """,
        "LILJAREPO001")]
    [InlineData(
        """
        using Lilja.Repository;
        namespace Demo;
        [Entity]
        public partial class Broken
        {
            [Persist(0)] private int _id;
            [Persist(0)] private int _other;
        }
        """,
        "LILJAREPO005")]
    [InlineData(
        """
        using Lilja.Repository;
        namespace Demo;
        [Entity]
        public partial class Broken
        {
            [Persist(0)]
            public int Value
            {
                get => 1;
            }
        }
        """,
        "LILJAREPO004")]
    [InlineData(
        """
        using Lilja.Repository;
        namespace Demo;
        [Entity]
        public partial class Broken
        {
            [Key] private readonly int _id;
            [Persist(0)] private readonly string _name = string.Empty;
        }
        """,
        "LILJAREPO006")]
    [InlineData(
        """
        using Lilja.Repository;
        namespace Demo;
        public readonly struct BrokenValueObject
        {
            [ToPrimitive]
            public (int x, int y) ToPrimitive() => (1, 2);
        }
        [Entity]
        public partial class Broken
        {
            [Persist(0)] private readonly BrokenValueObject _value;
            public Broken(BrokenValueObject value)
            {
                _value = value;
            }
        }
        """,
        "LILJAREPO008")]
    public void ReportsDiagnosticsForInvalidShapes(string source, string diagnosticId)
    {
        var result = RunGenerator(source, includeMessagePack: true);
        var diagnostics = result.Results.Single().Diagnostics;
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == diagnosticId);
    }

    [Fact]
    public void EditorSource_DoesNotRequireCompileTimeMessagePackUsing()
    {
        var editorDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "Scripts", "Editor");
        var editorSources = Directory.GetFiles(editorDirectory, "*.cs")
            .Select(File.ReadAllText)
            .ToArray();

        Assert.DoesNotContain(editorSources, source => source.Contains("using MessagePack", StringComparison.Ordinal));
        Assert.True(File.Exists(Path.Combine(editorDirectory, "MessagePackReflectionBridge.cs")));
    }

    private static GeneratorDriverRunResult RunGenerator(string entitySource, bool includeMessagePack)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "GeneratorTests",
            syntaxTrees: includeMessagePack
                ? new[]
                {
                    CSharpSyntaxTree.ParseText(SharedStubs),
                    CSharpSyntaxTree.ParseText(entitySource),
                    CSharpSyntaxTree.ParseText(MessagePackMarkerStub),
                }
                : new[]
                {
                    CSharpSyntaxTree.ParseText(SharedStubs),
                    CSharpSyntaxTree.ParseText(entitySource),
                },
            references: BasicReferences,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var compilationErrors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        if (compilationErrors.Length > 0)
        {
            throw new InvalidOperationException(new StringBuilder()
                .AppendLine("Generator test compilation failed:")
                .AppendJoin(Environment.NewLine, compilationErrors.Select(static diagnostic => diagnostic.ToString()))
                .ToString());
        }

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new[] { new RepositoryGenerator().AsSourceGenerator() },
            parseOptions: (CSharpParseOptions)compilation.SyntaxTrees[0].Options);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        var result = driver.GetRunResult();
        var generatorResult = result.Results.Single();
        if (generatorResult.Exception is not null)
        {
            throw generatorResult.Exception;
        }

        return result;
    }

    private static string GetHintName(string storageIdentifier, string fileName)
    {
        return storageIdentifier + "." + fileName;
    }

    private static readonly MetadataReference[] BasicReferences = CreateBasicReferences();

    private static MetadataReference[] CreateBasicReferences()
    {
        var trustedPlatformAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            !.Split(Path.PathSeparator);
        var assemblyPaths = trustedPlatformAssemblies
            .GroupBy(path => Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);

        var requiredAssemblies = new[]
        {
            "System.Private.CoreLib",
            "System.Runtime",
            "netstandard",
            "System.Collections",
            "System.Linq",
            "System.Console",
            "System.Threading",
            "System.Threading.Tasks",
            "System.Runtime.Extensions",
        };

        return requiredAssemblies
            .Where(assemblyPaths.ContainsKey)
            .Select(name => MetadataReference.CreateFromFile(assemblyPaths[name]))
            .ToArray();
    }

    private const string SharedStubs =
        """
        using System;
        using System.Collections.Generic;
        using System.Runtime.CompilerServices;
        using System.Threading;

        namespace Lilja.Repository
        {
            [AttributeUsage(AttributeTargets.Class)]
            public sealed class EntityAttribute : Attribute {}

            [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
            public sealed class KeyAttribute : Attribute {}

            [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
            public sealed class PersistAttribute : Attribute
            {
                public PersistAttribute(int index) {}
            }

            [AttributeUsage(AttributeTargets.Method)]
            public sealed class ToPrimitiveAttribute : Attribute {}

            [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor)]
            public sealed class FromPrimitiveAttribute : Attribute {}

            public interface IReadOnlyTx : IDisposable {}
            public interface IReadWriteTx : IReadOnlyTx {}

            public abstract class InMemoryKeyedRepositoryBase<TEntity, TKey>
                where TEntity : class
                where TKey : notnull
            {
                protected InMemoryKeyedRepositoryBase() {}
                public Cysharp.Threading.Tasks.UniTask InitializeAsync(CancellationToken ct = default) => default;
                public TEntity Read(IReadOnlyTx tx, TKey key) => default;
                public void Create(IReadWriteTx tx, TEntity entity) {}
                public void Update(IReadWriteTx tx, TEntity entity) {}
                public void Delete(IReadWriteTx tx, TKey key) {}
                public IReadOnlyList<TEntity> All(IReadOnlyTx tx) => Array.Empty<TEntity>();
                protected abstract TKey GetKey(TEntity entity);
            }

            public abstract class InMemorySingletonRepositoryBase<TEntity>
                where TEntity : class
            {
                protected InMemorySingletonRepositoryBase() {}
                public Cysharp.Threading.Tasks.UniTask InitializeAsync(CancellationToken ct = default) => default;
                public TEntity Read(IReadOnlyTx tx) => default;
                public void Create(IReadWriteTx tx, TEntity entity) {}
                public void Update(IReadWriteTx tx, TEntity entity) {}
                public void Delete(IReadWriteTx tx) {}
            }

            public abstract class PersistedKeyedRepositoryBase<TEntity, TKey, TDto>
                where TEntity : class
                where TKey : notnull
                where TDto : class
            {
                protected PersistedKeyedRepositoryBase(string filePath) {}
                protected string FilePath => string.Empty;
                public Cysharp.Threading.Tasks.UniTask InitializeAsync(CancellationToken ct = default) => default;
                public TEntity Read(IReadOnlyTx tx, TKey key) => default;
                public void Create(IReadWriteTx tx, TEntity entity) {}
                public void Update(IReadWriteTx tx, TEntity entity) {}
                public void Delete(IReadWriteTx tx, TKey key) {}
                public IReadOnlyList<TEntity> All(IReadOnlyTx tx) => Array.Empty<TEntity>();
                protected abstract TDto ToDto(TEntity entity);
                protected abstract TEntity FromDto(TDto dto);
                protected abstract TKey GetKeyFromDto(TDto dto);
                protected abstract Cysharp.Threading.Tasks.UniTask<IReadOnlyList<TDto>?> LoadItemsAsync(CancellationToken ct);
                protected abstract Cysharp.Threading.Tasks.UniTask SaveItemsAsync(IReadOnlyList<TDto> items, CancellationToken ct);
            }

            public abstract class PersistedSingletonRepositoryBase<TEntity, TDto>
                where TEntity : class
                where TDto : class
            {
                protected PersistedSingletonRepositoryBase(string filePath) {}
                protected string FilePath => string.Empty;
                public Cysharp.Threading.Tasks.UniTask InitializeAsync(CancellationToken ct = default) => default;
                public TEntity Read(IReadOnlyTx tx) => default;
                public void Create(IReadWriteTx tx, TEntity entity) {}
                public void Update(IReadWriteTx tx, TEntity entity) {}
                public void Delete(IReadWriteTx tx) {}
                protected abstract TDto ToDto(TEntity entity);
                protected abstract TEntity FromDto(TDto dto);
                protected abstract Cysharp.Threading.Tasks.UniTask<TDto?> LoadValueAsync(CancellationToken ct);
                protected abstract Cysharp.Threading.Tasks.UniTask SaveValueAsync(TDto? value, CancellationToken ct);
            }

            public static class AtomicFileWriter
            {
                public static void WriteAllText(string filePath, string content) {}
                public static void WriteAllBytes(string filePath, byte[] bytes) {}
                public static void DeleteIfExists(string filePath) {}
            }

        }

        namespace Lilja.Repository.Diagnostics
        {
            public static class RepositoryTracker
            {
                public enum RepositoryType
                {
                    InMemory,
                    Json,
                    MessagePack,
                }
            }
        }

        namespace Cysharp.Threading.Tasks
        {
            public readonly struct UniTask
            {
                public static UniTask CompletedTask => default;
                public static UniTask<T> RunOnThreadPool<T>(Func<T> func) => new UniTask<T>(func());
                public static UniTask RunOnThreadPool(Action action)
                {
                    action();
                    return default;
                }

                public Awaiter GetAwaiter() => default;

                public readonly struct Awaiter : INotifyCompletion
                {
                    public bool IsCompleted => true;
                    public void OnCompleted(Action continuation) => continuation();
                    public void GetResult() {}
                }
            }

            public readonly struct UniTask<T>
            {
                private readonly T _result;
                public UniTask(T result) { _result = result; }
                public Awaiter GetAwaiter() => new Awaiter(_result);

                public readonly struct Awaiter : INotifyCompletion
                {
                    private readonly T _result;
                    public Awaiter(T result) { _result = result; }
                    public bool IsCompleted => true;
                    public void OnCompleted(Action continuation) => continuation();
                    public T GetResult() => _result;
                }
            }
        }

        namespace UnityEngine
        {
            public static class Application
            {
                public static string persistentDataPath => "";
            }

            public static class JsonUtility
            {
                public static string ToJson(object obj, bool prettyPrint = false) => "";
                public static T FromJson<T>(string json) => default;
                public static object FromJson(string json, Type type) => null;
            }
        }
        """;

    private const string MessagePackMarkerStub =
        """
        using System;
        using MessagePack.Formatters;

        namespace MessagePack
        {
            public interface IFormatterResolver {}

            public sealed class MessagePackSerializerOptions
            {
                public static MessagePackSerializerOptions Standard { get; } = new MessagePackSerializerOptions();
                public MessagePackSerializerOptions WithResolver(IFormatterResolver resolver) => this;
            }

            public static class MessagePackSerializer
            {
                public static byte[] Serialize<T>(T value, MessagePackSerializerOptions options = null) => Array.Empty<byte>();
                public static T Deserialize<T>(byte[] bytes, MessagePackSerializerOptions options = null) => default;
            }
        }

        namespace MessagePack.Formatters
        {
            public interface IMessagePackFormatter {}
            public interface IMessagePackFormatter<T> : IMessagePackFormatter {}
        }

        namespace MessagePack.Resolvers
        {
            public sealed class CompositeResolver : MessagePack.IFormatterResolver
            {
                public static MessagePack.IFormatterResolver Create(IMessagePackFormatter[] formatters, MessagePack.IFormatterResolver[] resolvers) => new CompositeResolver();
            }

            public sealed class StandardResolver : MessagePack.IFormatterResolver
            {
                public static StandardResolver Instance { get; } = new StandardResolver();
            }
        }
        """;
}
