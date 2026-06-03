using System.Collections.Immutable;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lilja.Persistence.Analyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Lilja.Persistence.Analyzer.Test;

public sealed class GeneratorTests
{
    [Fact]
    public void Generates_nested_staging_and_root_repository_sources()
    {
        const string source = """
using System.Collections.Generic;
using Lilja.Persistence;

namespace Demo;

[Persistable]
public partial class Skill
{
    [Key]
    [Persist(0)]
    public string Id { get; }

    [Persist(1)]
    public int Level { get; private set; }
}

[Persistable]
public partial class Actor
{
    [Key]
    [Persist(0)]
    public int Id { get; }

    [Persist(1)]
    public KeyedStaging<Skill, string> Skills { get; }

    [Persist(2)]
    public List<Skill> InitialSkills { get; }
}

[Persistable(IsRoot = true)]
public partial class SaveData
{
    [Persist(0)]
    [Key]
    private readonly string _slotId;

    [Persist(1)]
    public KeyedStaging<Actor, int> Actors { get; }

    [Persist(2)]
    public int Day { get; set; }
}
""";

        var result = RunGenerator(source, includeMessagePack: true);
        var generated = result.GeneratedSources.ToDictionary(static item => item.HintName, static item => item.SourceText.ToString());

        Assert.Contains("Demo.Skill.SkillDto.g.cs", generated.Keys);
        Assert.Contains("Demo.Skill.SkillStaging.g.cs", generated.Keys);
        Assert.Contains("Demo.Actor.ActorDto.g.cs", generated.Keys);
        Assert.Contains("Demo.Actor.ActorStaging.g.cs", generated.Keys);
        Assert.Contains("Demo.SaveData.SaveDataDto.g.cs", generated.Keys);
        Assert.Contains("Demo.SaveData.ISaveDataRepository.g.cs", generated.Keys);
        Assert.Contains("Demo.SaveData.JsonSaveDataRepository.g.cs", generated.Keys);
        Assert.Contains("Demo.SaveData.InMemorySaveDataRepository.g.cs", generated.Keys);
        Assert.Contains("Demo.SaveData.MessagePackSaveDataRepository.g.cs", generated.Keys);
        Assert.Contains("Skills = new global::Demo.SkillStaging();", generated["Demo.Actor.Actor.Persistence.g.cs"]);
        Assert.Contains("Actors = new global::Demo.ActorStaging();", generated["Demo.SaveData.SaveData.Persistence.g.cs"]);
        Assert.Contains("string p0SlotId", generated["Demo.SaveData.SaveData.Persistence.g.cs"]);
        Assert.Contains("this._slotId = p0SlotId;", generated["Demo.SaveData.SaveData.Persistence.g.cs"]);
        Assert.Contains("local0SlotId", generated["Demo.SaveData.SaveData.Persistence.g.cs"]);
        Assert.Contains("public interface ISaveDataRepository", generated["Demo.SaveData.ISaveDataRepository.g.cs"]);
        Assert.Contains("UniTask<global::Demo.SaveData> LoadAsync(string key", generated["Demo.SaveData.ISaveDataRepository.g.cs"]);
        Assert.Contains("IReadOnlyList<global::Demo.SaveData>> LoadAllAsync", generated["Demo.SaveData.ISaveDataRepository.g.cs"]);
        Assert.Contains("UniTask SaveAsync(global::Demo.SaveData data", generated["Demo.SaveData.ISaveDataRepository.g.cs"]);
        Assert.Contains("bool Exists(string key);", generated["Demo.SaveData.ISaveDataRepository.g.cs"]);
        Assert.Contains(", global::Demo.Repositories.ISaveDataRepository", generated["Demo.SaveData.JsonSaveDataRepository.g.cs"]);
        Assert.Contains("public sealed class InMemorySaveDataRepository", generated["Demo.SaveData.InMemorySaveDataRepository.g.cs"]);
        Assert.Contains(", global::Demo.Repositories.ISaveDataRepository", generated["Demo.SaveData.InMemorySaveDataRepository.g.cs"]);
        Assert.Contains("IReadOnlyList<global::Demo.SaveData>? initialValues", generated["Demo.SaveData.InMemorySaveDataRepository.g.cs"]);
        Assert.Contains("private readonly global::System.Collections.Generic.Dictionary<string, global::Lilja.Persistence.Generated.Dtos.Demo.SaveDataDto> _values", generated["Demo.SaveData.InMemorySaveDataRepository.g.cs"]);
        Assert.Contains("_values[key] = data.ToDto();", generated["Demo.SaveData.InMemorySaveDataRepository.g.cs"]);
        Assert.Contains("LoadAllAsync", generated["Demo.SaveData.InMemorySaveDataRepository.g.cs"]);
        Assert.Contains("public override bool Exists(string key)", generated["Demo.SaveData.InMemorySaveDataRepository.g.cs"]);
        Assert.Contains("LoadAllAsync", generated["Demo.SaveData.MessagePackSaveDataRepository.g.cs"]);
        Assert.Contains("public override bool Exists(string key)", generated["Demo.SaveData.MessagePackSaveDataRepository.g.cs"]);

        AssertGeneratedSourcesCompile(source, result.GeneratedSources.Select(static item => item.SourceText.ToString()), includeMessagePack: true);
    }

    [Fact]
    public void Generates_in_memory_repository_for_keyless_root()
    {
        const string source = """
using Lilja.Persistence;

namespace Demo;

[Persistable(IsRoot = true)]
public partial class AppConfig
{
    [Persist(0)]
    public int Volume { get; set; }
}
""";

        var result = RunGenerator(source);
        var generated = result.GeneratedSources.ToDictionary(static item => item.HintName, static item => item.SourceText.ToString());

        Assert.Contains("Demo.AppConfig.IAppConfigRepository.g.cs", generated.Keys);
        Assert.Contains("Demo.AppConfig.InMemoryAppConfigRepository.g.cs", generated.Keys);
        Assert.DoesNotContain("LoadAllAsync", generated["Demo.AppConfig.IAppConfigRepository.g.cs"]);
        Assert.DoesNotContain("Exists", generated["Demo.AppConfig.IAppConfigRepository.g.cs"]);
        Assert.Contains("public sealed class InMemoryAppConfigRepository", generated["Demo.AppConfig.InMemoryAppConfigRepository.g.cs"]);
        Assert.Contains(", global::Demo.Repositories.IAppConfigRepository", generated["Demo.AppConfig.InMemoryAppConfigRepository.g.cs"]);
        Assert.Contains("public InMemoryAppConfigRepository(global::Demo.AppConfig? initialValue)", generated["Demo.AppConfig.InMemoryAppConfigRepository.g.cs"]);
        Assert.Contains("private global::Lilja.Persistence.Generated.Dtos.Demo.AppConfigDto? _value;", generated["Demo.AppConfig.InMemoryAppConfigRepository.g.cs"]);
        Assert.Contains("_value = data.ToDto();", generated["Demo.AppConfig.InMemoryAppConfigRepository.g.cs"]);
        Assert.DoesNotContain("LoadAllAsync", generated["Demo.AppConfig.InMemoryAppConfigRepository.g.cs"]);
        Assert.DoesNotContain("Exists", generated["Demo.AppConfig.InMemoryAppConfigRepository.g.cs"]);

        AssertGeneratedSourcesCompile(source, result.GeneratedSources.Select(static item => item.SourceText.ToString()));
    }

    private static GeneratorRunResult RunGenerator(string source, bool includeMessagePack = false)
    {
        var syntaxTrees = new List<SyntaxTree>
        {
            CSharpSyntaxTree.ParseText(RuntimeStubSource, new CSharpParseOptions(LanguageVersion.Preview)),
            CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview)),
        };
        if (includeMessagePack)
        {
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(MessagePackStubSource, new CSharpParseOptions(LanguageVersion.Preview)));
        }

        var compilation = CSharpCompilation.Create(
            "Lilja.Persistence.GeneratorTests",
            syntaxTrees,
            GetReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new LiljaPersistenceGenerator());
        driver = driver.RunGenerators(compilation);
        return driver.GetRunResult().Results.Single();
    }

    private static void AssertGeneratedSourcesCompile(string source, IEnumerable<string> generatedSources, bool includeMessagePack = false)
    {
        var trees = new List<SyntaxTree>
        {
            CSharpSyntaxTree.ParseText(RuntimeStubSource, new CSharpParseOptions(LanguageVersion.Preview)),
            CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview)),
        };
        if (includeMessagePack)
        {
            trees.Add(CSharpSyntaxTree.ParseText(MessagePackStubSource, new CSharpParseOptions(LanguageVersion.Preview)));
        }

        trees.AddRange(generatedSources.Select(generated => CSharpSyntaxTree.ParseText(generated, new CSharpParseOptions(LanguageVersion.Preview))));

        var compilation = CSharpCompilation.Create(
            "Lilja.Persistence.Generated.Compilation",
            trees,
            GetReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var errors = compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.Empty(errors);
    }

    private static ImmutableArray<MetadataReference> GetReferences()
    {
        var assemblies = new[]
        {
            typeof(object).Assembly,
            typeof(Enumerable).Assembly,
            typeof(List<>).Assembly,
            typeof(System.Runtime.GCSettings).Assembly,
        };

        return assemblies
            .Select(static assembly => MetadataReference.CreateFromFile(assembly.Location))
            .Cast<MetadataReference>()
            .ToImmutableArray();
    }

    private const string RuntimeStubSource = """
using System;
using System.Collections.Generic;
using System.Threading;

namespace Lilja.Persistence
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class PersistableAttribute : Attribute
    {
        public bool IsRoot { get; set; }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class PersistAttribute : Attribute
    {
        public PersistAttribute(int index) { Index = index; }
        public int Index { get; }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class KeyAttribute : Attribute {}

    public readonly struct RestoreToken {}

    public interface IKeyed<TKey>
    {
        TKey Key { get; }
    }

    public interface IStagingSnapshot<TDto>
    {
        IReadOnlyList<TDto> ExportDtos();
        void ImportDtos(IEnumerable<TDto>? dtos);
    }

    public abstract class KeyedStaging<TEntity, TKey>
        where TEntity : class, IKeyed<TKey>
        where TKey : notnull
    {
        public abstract TEntity? GetOrDefault(TKey key);
        public abstract bool TryGet(TKey key, out TEntity? entity);
        public abstract bool Contains(TKey key);
        public abstract IReadOnlyList<TEntity> All();
        public abstract void Update(TEntity entity);
        public abstract bool Delete(TKey key);
    }

    public abstract class KeyedStaging<TEntity, TKey, TDto> : KeyedStaging<TEntity, TKey>, IStagingSnapshot<TDto>
        where TEntity : class, IKeyed<TKey>
        where TKey : notnull
        where TDto : class
    {
        public override TEntity? GetOrDefault(TKey key) => throw new NotImplementedException();
        public override bool TryGet(TKey key, out TEntity? entity) => throw new NotImplementedException();
        public override bool Contains(TKey key) => throw new NotImplementedException();
        public override IReadOnlyList<TEntity> All() => throw new NotImplementedException();
        public override void Update(TEntity entity) => throw new NotImplementedException();
        public override bool Delete(TKey key) => throw new NotImplementedException();
        public IReadOnlyList<TDto> ExportDtos() => throw new NotImplementedException();
        public void ImportDtos(IEnumerable<TDto>? dtos) => throw new NotImplementedException();
        protected abstract TEntity ToEntity(TDto dto);
        protected abstract TDto ToDto(TEntity entity);
        protected abstract TKey GetKey(TDto dto);
    }

    public abstract class Repository<TData> where TData : class
    {
        public abstract Cysharp.Threading.Tasks.UniTask<TData> LoadAsync(CancellationToken ct = default);
        public abstract Cysharp.Threading.Tasks.UniTask SaveAsync(TData data, CancellationToken ct = default);
    }

    public abstract class KeyedRepository<TKey, TData>
        where TData : class, IKeyed<TKey>
    {
        public abstract Cysharp.Threading.Tasks.UniTask<TData> LoadAsync(TKey key, CancellationToken ct = default);
        public abstract Cysharp.Threading.Tasks.UniTask<IReadOnlyList<TData>> LoadAllAsync(CancellationToken ct = default);
        public abstract Cysharp.Threading.Tasks.UniTask SaveAsync(TData data, CancellationToken ct = default);
        public abstract bool Exists(TKey key);
    }

    public abstract class JsonRepository<TData, TDto> : Repository<TData>
        where TData : class where TDto : class
    {
        protected JsonRepository(string filePath) {}
        public override Cysharp.Threading.Tasks.UniTask<TData> LoadAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public override Cysharp.Threading.Tasks.UniTask SaveAsync(TData data, CancellationToken ct = default) => throw new NotImplementedException();
        protected abstract TData CreateDefault();
        protected abstract TData FromDto(TDto dto);
        protected abstract TDto ToDto(TData data);
    }

    public abstract class JsonKeyedRepository<TKey, TData, TDto> : KeyedRepository<TKey, TData>
        where TData : class, IKeyed<TKey> where TDto : class
    {
        public override Cysharp.Threading.Tasks.UniTask<TData> LoadAsync(TKey key, CancellationToken ct = default) => throw new NotImplementedException();
        public override Cysharp.Threading.Tasks.UniTask<IReadOnlyList<TData>> LoadAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public override Cysharp.Threading.Tasks.UniTask SaveAsync(TData data, CancellationToken ct = default) => throw new NotImplementedException();
        public override bool Exists(TKey key) => throw new NotImplementedException();
        protected abstract string FileExtension { get; }
        protected abstract string GetDirectoryPath();
        protected abstract string GetFilePath(TKey key);
        protected abstract TData CreateDefault(TKey key);
        protected abstract TData FromDto(TDto dto);
        protected abstract TDto ToDto(TData data);
    }

    public static class PersistenceFileName
    {
        public static string Encode(object key) => key?.ToString() ?? "";
    }

    public static class AtomicFileWriter
    {
        public static void WriteAllBytes(string filePath, byte[] bytes) {}
    }
}

namespace Cysharp.Threading.Tasks
{
    public readonly struct UniTask
    {
        public static UniTask CompletedTask => default;
        public static UniTask RunOnThreadPool(Action action, CancellationToken cancellationToken = default) => default;
        public static UniTask<T> RunOnThreadPool<T>(Func<T> function, CancellationToken cancellationToken = default) => default;
        public static UniTask<T> FromResult<T>(T value) => default;
    }

    public readonly struct UniTask<T> {}
}

namespace UnityEngine
{
    public static class Application
    {
        public static string persistentDataPath => "";
    }
}
""";

    private const string MessagePackStubSource = """
using System;

namespace MessagePack
{
    public sealed class MessagePackSerializationException : Exception
    {
        public MessagePackSerializationException(string message) : base(message) {}
    }

    public interface IFormatterResolver
    {
        Formatters.IMessagePackFormatter<T>? GetFormatter<T>();
    }

    public sealed class MessagePackSerializerOptions
    {
        public static MessagePackSerializerOptions Standard { get; } = new MessagePackSerializerOptions();
        public IFormatterResolver Resolver { get; } = new Resolvers.StandardResolver();
        public MessagePackSerializerOptions WithResolver(IFormatterResolver resolver) => this;
    }

    public static class MessagePackSerializer
    {
        public static byte[] Serialize<T>(T value, MessagePackSerializerOptions options) => Array.Empty<byte>();
        public static T Deserialize<T>(byte[] bytes, MessagePackSerializerOptions options) => default!;
    }

    public struct MessagePackWriter
    {
        public void WriteNil() {}
        public void WriteArrayHeader(int count) {}
    }

    public struct MessagePackReader
    {
        public bool TryReadNil() => false;
        public int ReadArrayHeader() => 0;
        public void Skip() {}
    }
}

namespace MessagePack.Formatters
{
    public interface IMessagePackFormatter {}

    public interface IMessagePackFormatter<T> : IMessagePackFormatter
    {
        void Serialize(ref MessagePack.MessagePackWriter writer, T value, MessagePack.MessagePackSerializerOptions options);
        T Deserialize(ref MessagePack.MessagePackReader reader, MessagePack.MessagePackSerializerOptions options);
    }
}

namespace MessagePack.Resolvers
{
    public sealed class CompositeResolver : MessagePack.IFormatterResolver
    {
        public static MessagePack.IFormatterResolver Create(MessagePack.Formatters.IMessagePackFormatter[] formatters, MessagePack.IFormatterResolver[] resolvers) => new CompositeResolver();
        public MessagePack.Formatters.IMessagePackFormatter<T>? GetFormatter<T>() => default;
    }

    public sealed class StandardResolver : MessagePack.IFormatterResolver
    {
        public static StandardResolver Instance { get; } = new StandardResolver();
        public MessagePack.Formatters.IMessagePackFormatter<T>? GetFormatter<T>() => default;
    }
}
""";
}
