using System.Collections.Immutable;
using System.Reflection;
using Lilja.Repository.Analyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Lilja.Repository.Analyzer.Test;

public sealed class GeneratorTests
{
    [Fact]
    public void Entity_without_options_generates_dto_support_only()
    {
        const string source = """
using Lilja.Repository;

namespace Demo;

[Entity]
public partial class Skill
{
    [Persist(0)] public string Id { get; }

    public Skill(string id)
    {
        Id = id;
    }
}
""";

        var result = RunGenerator(source);
        var generated = ToGeneratedMap(result);

        Assert.Contains("Demo.Skill.SkillDto.g.cs", generated.Keys);
        Assert.Contains("Demo.Skill.Skill.RepositorySupport.g.cs", generated.Keys);
        Assert.DoesNotContain("Demo.Skill.ISkillRepository.g.cs", generated.Keys);
        Assert.DoesNotContain("Demo.Skill.SkillRepository.g.cs", generated.Keys);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Repository_options_generate_interface_and_factories()
    {
        const string source = """
using System.Collections.Generic;
using Lilja.Repository;

namespace Demo;

[Entity]
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

}

[Entity(RepositoryOptions.InMemory | RepositoryOptions.Json)]
public partial class SaveData
{
    [Key]
    [Persist(0)]
    public string SlotId { get; }

    [Persist(1)]
    public List<Skill> Skills { get; }

    public SaveData(string slotId, List<Skill> skills)
    {
        SlotId = slotId;
        Skills = skills;
    }
}
""";

        var result = RunGenerator(source);
        var generated = ToGeneratedMap(result);

        Assert.Contains("Demo.SaveData.ISaveDataRepository.g.cs", generated.Keys);
        Assert.Contains("Demo.SaveData.SaveDataRepository.g.cs", generated.Keys);
        Assert.Contains("public interface ISaveDataRepository", generated["Demo.SaveData.ISaveDataRepository.g.cs"]);
        Assert.Contains("UniTask<global::Demo.SaveData> LoadAsync(string key", generated["Demo.SaveData.ISaveDataRepository.g.cs"]);
        Assert.Contains("public static class InMemory", generated["Demo.SaveData.SaveDataRepository.g.cs"]);
        Assert.Contains("public static class Json", generated["Demo.SaveData.SaveDataRepository.g.cs"]);
        Assert.Contains("global::System.Collections.Generic.List<global::Lilja.Repository.Generated.Dtos.Demo.SkillDto> Skills", generated["Demo.SaveData.SaveDataDto.g.cs"]);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        AssertCompiles(source, result.Results.Single().GeneratedSources);
    }

    [Fact]
    public void Persist_without_index_uses_declaration_order_and_mixes_with_explicit_indexes()
    {
        const string source = """
using Lilja.Repository;

namespace Demo;

[Entity]
public partial class Config
{
    [Persist] public string Name { get; }
    [Persist(3)] public int Level { get; }
    [Persist] public bool Enabled { get; }

    public Config(string name, bool enabled, int level)
    {
        Name = name;
        Enabled = enabled;
        Level = level;
    }
}
""";

        var result = RunGenerator(source);
        var generated = ToGeneratedMap(result);
        var support = generated["Demo.Config.Config.RepositorySupport.g.cs"];

        Assert.Contains("new global::Demo.Config(local0Name, local1Enabled, local3Level)", support);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Missing_matching_constructor_generates_restore_constructor()
    {
        const string source = """
using Lilja.Repository;

namespace Demo;

[Entity]
public partial class Config
{
    [Persist(0)] public int Volume { get; private set; }

    public Config()
    {
    }
}
""";

        var result = RunGenerator(source);
        var generated = ToGeneratedMap(result);

        Assert.Contains("private Config(global::Lilja.Repository.RestoreToken _", generated["Demo.Config.Config.RepositorySupport.g.cs"]);
        Assert.Contains("return new global::Demo.Config(default(global::Lilja.Repository.RestoreToken), local0Volume);", generated["Demo.Config.Config.RepositorySupport.g.cs"]);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void MessagePack_requested_without_contract_reports_warning_and_skips_factory()
    {
        const string source = """
using Lilja.Repository;

namespace Demo;

[Entity(RepositoryOptions.MessagePack)]
public partial class Config
{
    [Persist(0)] public int Volume { get; }

    public Config(int volume)
    {
        Volume = volume;
    }
}
""";

        var result = RunGenerator(source);
        var generated = ToGeneratedMap(result);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "LILJAREPO010");
        Assert.Contains("public static class ConfigRepository", generated["Demo.Config.ConfigRepository.g.cs"]);
        Assert.DoesNotContain("public static class MessagePack", generated["Demo.Config.ConfigRepository.g.cs"]);
    }

    [Fact]
    public void Key_without_persist_reports_error()
    {
        const string source = """
using Lilja.Repository;

[Entity(RepositoryOptions.Json)]
public partial class SaveData
{
    [Key] public string SlotId { get; }
}
""";

        var result = RunGenerator(source);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "LILJAREPO006");
    }

    private static GeneratorDriverRunResult RunGenerator(string source, bool includeMessagePack = false)
    {
        var syntaxTrees = new[] { CSharpSyntaxTree.ParseText(RuntimeStubs), CSharpSyntaxTree.ParseText(source) };
        var references = CreateReferences();
        if (includeMessagePack)
        {
            syntaxTrees = syntaxTrees.Append(CSharpSyntaxTree.ParseText(MessagePackStubs)).ToArray();
        }

        var compilation = CSharpCompilation.Create(
            "Tests",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new LiljaRepositoryGenerator());
        driver = driver.RunGenerators(compilation);
        return driver.GetRunResult();
    }

    private static Dictionary<string, string> ToGeneratedMap(GeneratorDriverRunResult result)
    {
        return result.Results.Single().GeneratedSources.ToDictionary(item => item.HintName, item => item.SourceText.ToString());
    }

    private static void AssertCompiles(string source, ImmutableArray<GeneratedSourceResult> generatedSources)
    {
        var trees = new List<SyntaxTree>
        {
            CSharpSyntaxTree.ParseText(RuntimeStubs),
            CSharpSyntaxTree.ParseText(source),
        };
        trees.AddRange(generatedSources.Select(item => CSharpSyntaxTree.ParseText(item.SourceText.ToString())));

        var compilation = CSharpCompilation.Create(
            "Generated",
            trees,
            CreateReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var diagnostics = compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.Empty(diagnostics);
    }

    private static ImmutableArray<MetadataReference> CreateReferences()
    {
        return ImmutableArray.Create<MetadataReference>(
            MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).GetTypeInfo().Assembly.Location),
            MetadataReference.CreateFromFile(typeof(List<>).GetTypeInfo().Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).GetTypeInfo().Assembly.Location));
    }

    private const string RuntimeStubs = """
using System;
using System.Collections.Generic;
using System.Threading;

namespace Lilja.Repository
{
    [Flags] public enum RepositoryOptions { None = 0, InMemory = 1, Json = 2, MessagePack = 4 }
    public sealed class EntityAttribute : Attribute { public EntityAttribute(RepositoryOptions repositoryOptions = RepositoryOptions.None) {} }
    public sealed class PersistAttribute : Attribute { public PersistAttribute() {} public PersistAttribute(int index) {} }
    public sealed class KeyAttribute : Attribute {}
    public sealed class ToPrimitiveAttribute : Attribute {}
    public sealed class FromPrimitiveAttribute : Attribute {}
    public readonly struct RestoreToken {}
    public static class RepositoryFileName { public static string Encode<TKey>(TKey key) => ""; }
    public static class AtomicFileWriter { public static void WriteAllText(string path, string value) {} public static void WriteAllBytes(string path, byte[] value) {} public static bool DeleteIfExists(string path) => true; }
    public abstract class InMemoryRepository<TEntity, TDto> where TEntity : class where TDto : class
    {
        protected InMemoryRepository(Func<TEntity, TDto> toDto, Func<TDto, TEntity> fromDto, Func<TDto> createDefaultDto, TEntity? initialValue = null) {}
        public Cysharp.Threading.Tasks.UniTask<TEntity> LoadAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Cysharp.Threading.Tasks.UniTask SaveAsync(TEntity entity, CancellationToken ct = default) => throw new NotImplementedException();
        public Cysharp.Threading.Tasks.UniTask<bool> DeleteAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public bool Exists() => throw new NotImplementedException();
    }
    public abstract class InMemoryKeyedRepository<TKey, TEntity, TDto> where TKey : notnull where TEntity : class where TDto : class
    {
        protected InMemoryKeyedRepository(Func<TEntity, TDto> toDto, Func<TDto, TEntity> fromDto, Func<TEntity, TKey> getKeyFromEntity, Func<TDto, TKey> getKeyFromDto, Func<TKey, TDto> createDefaultDto, IReadOnlyList<TEntity>? initialValues = null) {}
        public Cysharp.Threading.Tasks.UniTask<TEntity> LoadAsync(TKey key, CancellationToken ct = default) => throw new NotImplementedException();
        public Cysharp.Threading.Tasks.UniTask<IReadOnlyList<TEntity>> LoadAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Cysharp.Threading.Tasks.UniTask SaveAsync(TEntity entity, CancellationToken ct = default) => throw new NotImplementedException();
        public Cysharp.Threading.Tasks.UniTask<bool> DeleteAsync(TKey key, CancellationToken ct = default) => throw new NotImplementedException();
        public bool Exists(TKey key) => throw new NotImplementedException();
    }
    public abstract class JsonRepository<TEntity, TDto> : InMemoryRepository<TEntity, TDto> where TEntity : class where TDto : class { protected JsonRepository(string filePath, Func<TEntity, TDto> toDto, Func<TDto, TEntity> fromDto, Func<TDto> createDefaultDto) : base(toDto, fromDto, createDefaultDto) {} }
    public abstract class JsonKeyedRepository<TKey, TEntity, TDto> : InMemoryKeyedRepository<TKey, TEntity, TDto> where TKey : notnull where TEntity : class where TDto : class { protected JsonKeyedRepository(string directoryPath, Func<TEntity, TDto> toDto, Func<TDto, TEntity> fromDto, Func<TEntity, TKey> getKeyFromEntity, Func<TKey, TDto> createDefaultDto) : base(toDto, fromDto, getKeyFromEntity, _ => default!, createDefaultDto) {} }
}

namespace Cysharp.Threading.Tasks
{
    public readonly struct UniTask {}
    public readonly struct UniTask<T> {}
}

namespace UnityEngine
{
    public static class Application { public static string persistentDataPath => ""; }
    public static class JsonUtility { public static string ToJson(object value, bool prettyPrint) => ""; public static T FromJson<T>(string json) => default!; }
}

""";

    private const string MessagePackStubs = """
namespace MessagePack
{
    public interface IFormatterResolver { MessagePack.Formatters.IMessagePackFormatter<T>? GetFormatter<T>(); }
    public sealed class MessagePackSerializerOptions { public static MessagePackSerializerOptions Standard { get; } = new(); public IFormatterResolver Resolver { get; } = default!; public MessagePackSerializerOptions WithResolver(IFormatterResolver resolver) => this; }
    public ref struct MessagePackWriter { public void WriteNil() {} public void WriteMapHeader(int count) {} public void Write(string value) {} }
    public ref struct MessagePackReader { public bool TryReadNil() => false; public int ReadMapHeader() => 0; public string ReadString() => ""; public void Skip() {} }
    public sealed class MessagePackSerializationException : System.Exception { public MessagePackSerializationException(string message) : base(message) {} }
    public static class MessagePackSerializer { public static byte[] Serialize<T>(T value, MessagePackSerializerOptions options) => []; public static T Deserialize<T>(byte[] bytes, MessagePackSerializerOptions options) => default!; }
}
namespace MessagePack.Formatters { public interface IMessagePackFormatter {} public interface IMessagePackFormatter<T> : IMessagePackFormatter { void Serialize(ref MessagePack.MessagePackWriter writer, T value, MessagePack.MessagePackSerializerOptions options); T Deserialize(ref MessagePack.MessagePackReader reader, MessagePack.MessagePackSerializerOptions options); } }
namespace MessagePack.Resolvers { public sealed class StandardResolver : MessagePack.IFormatterResolver { public static StandardResolver Instance { get; } = new(); public MessagePack.Formatters.IMessagePackFormatter<T>? GetFormatter<T>() => default; } public static class CompositeResolver { public static MessagePack.IFormatterResolver Create(MessagePack.Formatters.IMessagePackFormatter[] formatters, MessagePack.IFormatterResolver[] resolvers) => StandardResolver.Instance; } }
""";
}
