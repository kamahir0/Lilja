using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Lilja.Repository.Analyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Lilja.Repository.Analyzer.Test;

public sealed class MessagePackCompatibilityTests
{
    private const string MessagePackV2Version = "2.5.187";
    private const string MessagePackV3Version = "3.1.4";

    private const string PersistedEntitiesSource = """
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

    public Item(int id, string name)
    {
        Id = id;
        Name = name;
    }
}

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
""";

    [Theory]
    [InlineData(MessagePackV2Version)]
    [InlineData(MessagePackV3Version)]
    public void Generated_messagepack_sources_compile_against_actual_package(string version)
    {
        var package = EnsureMessagePackPackage(version);
        var result = RunGenerator(PersistedEntitiesSource, package.MetadataReferences);

        var syntaxTrees = new List<SyntaxTree>
        {
            ParseText(RuntimeAttributeSource, "UNITY_EDITOR"),
            ParseText(GeneratedRepositorySupportSource, "UNITY_EDITOR"),
            ParseText(PersistedEntitiesSource, "UNITY_EDITOR"),
        };

        foreach (var generatedSource in GetGeneratedSources(
                     result,
                     "Demo.Item.IItemRepository.g.cs",
                     "Demo.Item.MessagePackItemRepository.g.cs",
                     "Demo.Item.ItemDto.g.cs",
                     "Demo.Item.ItemStorageEnvelope.g.cs",
                     "Demo.Item.ItemDtoFormatter.g.cs",
                     "Demo.Item.ItemStorageEnvelopeFormatter.g.cs",
                     "Demo.Item.Item.Converter.g.cs",
                     "Demo.Item.Item.KeyAccessor.g.cs",
                     "Demo.Settings.ISettingsRepository.g.cs",
                     "Demo.Settings.MessagePackSettingsRepository.g.cs",
                     "Demo.Settings.SettingsDto.g.cs",
                     "Demo.Settings.SettingsStorageEnvelope.g.cs",
                     "Demo.Settings.SettingsDtoFormatter.g.cs",
                     "Demo.Settings.SettingsStorageEnvelopeFormatter.g.cs",
                     "Demo.Settings.Settings.Converter.g.cs"))
        {
            syntaxTrees.Add(ParseText(generatedSource.SourceText.ToString(), "UNITY_EDITOR", generatedSource.HintName));
        }

        var compilation = CSharpCompilation.Create(
            $"Lilja.Repository.Generated.MessagePack.{version}.Compilation",
            syntaxTrees,
            GetPlatformReferences().AddRange(package.MetadataReferences),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        AssertNoCompilationErrors(compilation);
    }

    [Theory]
    [InlineData(MessagePackV2Version)]
    [InlineData(MessagePackV3Version)]
    public void Editor_bridge_detects_and_roundtrips_actual_package(string version)
    {
        var package = EnsureMessagePackPackage(version);
        using var assembly = CompileAndLoad(
            $"Lilja.Repository.Editor.MessagePackBridge.{version}",
            new[]
            {
                ParseText(File.ReadAllText(GetEditorSourcePath("MessagePackCompatibilityProbe.cs")), "UNITY_EDITOR", "MessagePackCompatibilityProbe.cs"),
                ParseText(File.ReadAllText(GetEditorSourcePath("MessagePackReflectionBridge.cs")), "UNITY_EDITOR", "MessagePackReflectionBridge.cs"),
                ParseText(EditorBridgeHarnessSource, "UNITY_EDITOR", "EditorBridgeHarness.cs"),
            },
            GetPlatformReferences().AddRange(package.MetadataReferences),
            package.RuntimeAssemblyPaths);

        Assert.True(assembly.InvokeStatic<bool>("Lilja.Repository.Editor.MessagePackBridgeHarness", "ProbeIsAvailable"));
        Assert.True(assembly.InvokeStatic<bool>("Lilja.Repository.Editor.MessagePackBridgeHarness", "IsAvailable"));
        Assert.Equal(42, assembly.InvokeStatic<int>("Lilja.Repository.Editor.MessagePackBridgeHarness", "RoundTripValue"));
    }

    [Fact]
    public void Generated_keyed_fixtures_roundtrip_between_v2_and_v3()
    {
        var v2Package = EnsureMessagePackPackage(MessagePackV2Version);
        var v3Package = EnsureMessagePackPackage(MessagePackV3Version);

        using var v2Assembly = BuildFixtureHarness(v2Package);
        using var v3Assembly = BuildFixtureHarness(v3Package);

        var bytesFromV2 = v2Assembly.InvokeStatic<byte[]>("Demo.GeneratedFixtureHarness", "SerializeKeyed");
        Assert.Equal("1:1:Slime", v3Assembly.InvokeStatic<string>("Demo.GeneratedFixtureHarness", "DeserializeKeyed", bytesFromV2));

        var bytesFromV3 = v3Assembly.InvokeStatic<byte[]>("Demo.GeneratedFixtureHarness", "SerializeKeyed");
        Assert.Equal("1:1:Slime", v2Assembly.InvokeStatic<string>("Demo.GeneratedFixtureHarness", "DeserializeKeyed", bytesFromV3));
    }

    [Fact]
    public void Generated_singleton_fixtures_roundtrip_between_v2_and_v3()
    {
        var v2Package = EnsureMessagePackPackage(MessagePackV2Version);
        var v3Package = EnsureMessagePackPackage(MessagePackV3Version);

        using var v2Assembly = BuildFixtureHarness(v2Package);
        using var v3Assembly = BuildFixtureHarness(v3Package);

        var bytesFromV2 = v2Assembly.InvokeStatic<byte[]>("Demo.GeneratedFixtureHarness", "SerializeSingleton");
        Assert.Equal("True:7", v3Assembly.InvokeStatic<string>("Demo.GeneratedFixtureHarness", "DeserializeSingleton", bytesFromV2));

        var bytesFromV3 = v3Assembly.InvokeStatic<byte[]>("Demo.GeneratedFixtureHarness", "SerializeSingleton");
        Assert.Equal("True:7", v2Assembly.InvokeStatic<string>("Demo.GeneratedFixtureHarness", "DeserializeSingleton", bytesFromV3));
    }

    private static LoadedAssembly BuildFixtureHarness(MessagePackPackage package)
    {
        var result = RunGenerator(PersistedEntitiesSource, package.MetadataReferences);
        var syntaxTrees = new List<SyntaxTree>
        {
            ParseText(RuntimeAttributeSource),
            ParseText(PersistedEntitiesSource),
            ParseText(GeneratedFixtureHarnessSource),
        };

        foreach (var generatedSource in GetGeneratedSources(
                     result,
                     "Demo.Item.ItemDto.g.cs",
                     "Demo.Item.ItemStorageEnvelope.g.cs",
                     "Demo.Item.ItemDtoFormatter.g.cs",
                     "Demo.Item.ItemStorageEnvelopeFormatter.g.cs",
                     "Demo.Settings.SettingsDto.g.cs",
                     "Demo.Settings.SettingsStorageEnvelope.g.cs",
                     "Demo.Settings.SettingsDtoFormatter.g.cs",
                     "Demo.Settings.SettingsStorageEnvelopeFormatter.g.cs"))
        {
            syntaxTrees.Add(ParseText(generatedSource.SourceText.ToString(), filePath: generatedSource.HintName));
        }

        return CompileAndLoad(
            $"Lilja.Repository.Generated.Fixture.{package.Version}",
            syntaxTrees,
            GetPlatformReferences().AddRange(package.MetadataReferences),
            package.RuntimeAssemblyPaths);
    }

    private static GeneratorRunResult RunGenerator(string source, IEnumerable<MetadataReference> messagePackReferences)
    {
        var compilation = CSharpCompilation.Create(
            "Lilja.Repository.MessagePack.GeneratorTests",
            new[]
            {
                ParseText(RuntimeAttributeSource),
                ParseText(source),
            },
            GetPlatformReferences().AddRange(messagePackReferences),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new LiljaRepositoryGenerator());
        driver = driver.RunGenerators(compilation);
        return driver.GetRunResult().Results.Single();
    }

    private static IEnumerable<GeneratedSourceResult> GetGeneratedSources(GeneratorRunResult result, params string[] hintNames)
    {
        foreach (var hintName in hintNames)
        {
            var generatedSource = result.GeneratedSources.SingleOrDefault(item => item.HintName == hintName);
            Assert.True(generatedSource.HintName is not null, $"Generated source '{hintName}' was not found.");
            yield return generatedSource;
        }
    }

    private static MessagePackPackage EnsureMessagePackPackage(string version)
    {
        var packageRoot = GetNuGetPackageRoot();
        if (!TryCreateMessagePackPackage(packageRoot, version, out var package))
        {
            RestoreMessagePackPackage(version);
            Assert.True(TryCreateMessagePackPackage(packageRoot, version, out package), $"MessagePack {version} package could not be located after restore.");
        }

        return package!;
    }

    private static bool TryCreateMessagePackPackage(string packageRoot, string version, out MessagePackPackage? package)
    {
        package = null;
        var runtimeAssemblyPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var messagePackPath = FindPackageAssembly(packageRoot, "messagepack", version, "MessagePack.dll");
        if (messagePackPath is null)
        {
            return false;
        }

        runtimeAssemblyPaths["MessagePack"] = messagePackPath;

        var annotationsPath = FindPackageAssembly(packageRoot, "messagepack.annotations", version, "MessagePack.Annotations.dll");
        if (annotationsPath is not null)
        {
            runtimeAssemblyPaths["MessagePack.Annotations"] = annotationsPath;
        }

        package = new MessagePackPackage(
            version,
            ImmutableArray.CreateRange(runtimeAssemblyPaths.Values.Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))),
            runtimeAssemblyPaths.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase));
        return true;
    }

    private static string? FindPackageAssembly(string packageRoot, string packageId, string version, string assemblyFileName)
    {
        var packageDirectory = Path.Combine(packageRoot, packageId, version, "lib");
        if (!Directory.Exists(packageDirectory))
        {
            return null;
        }

        return Directory.GetFiles(packageDirectory, assemblyFileName, SearchOption.AllDirectories)
            .OrderByDescending(path => path.Contains("netstandard2.1", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(path => path.Contains("netstandard2.0", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();
    }

    private static string GetNuGetPackageRoot()
    {
        var packageRoot = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (!string.IsNullOrWhiteSpace(packageRoot))
        {
            return packageRoot;
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
    }

    private static void RestoreMessagePackPackage(string version)
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), "Lilja.Repository.MessagePackRestore", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);
        var projectPath = Path.Combine(workingDirectory, "Restore.csproj");
        File.WriteAllText(
            projectPath,
            $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="MessagePack" Version="{{version}}" />
              </ItemGroup>
            </Project>
            """);

        RunProcess("dotnet", $"restore \"{projectPath}\"", workingDirectory);
    }

    private static void RunProcess(string fileName, string arguments, string workingDirectory)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };

        process.Start();
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(process.ExitCode == 0, $"{fileName} {arguments} failed.{Environment.NewLine}{standardOutput}{Environment.NewLine}{standardError}");
    }

    private static LoadedAssembly CompileAndLoad(
        string assemblyName,
        IEnumerable<SyntaxTree> syntaxTrees,
        IEnumerable<MetadataReference> references,
        ImmutableDictionary<string, string> runtimeAssemblyPaths)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        AssertNoCompilationErrors(compilation);

        using var assemblyStream = new MemoryStream();
        var emitResult = compilation.Emit(assemblyStream);
        Assert.True(
            emitResult.Success,
            string.Join(Environment.NewLine, emitResult.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));

        return new LoadedAssembly(assemblyStream.ToArray(), runtimeAssemblyPaths);
    }

    private static void AssertNoCompilationErrors(Compilation compilation)
    {
        var diagnostics = compilation.GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.Empty(diagnostics);
    }

    private static SyntaxTree ParseText(string source, string? preprocessorSymbol = null, string? filePath = null)
    {
        var symbols = string.IsNullOrEmpty(preprocessorSymbol) ? Array.Empty<string>() : new[] { preprocessorSymbol };
        return CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: symbols), filePath ?? string.Empty);
    }

    private static ImmutableArray<MetadataReference> GetBasicReferences()
    {
        return ImmutableArray.Create<MetadataReference>(
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ValueTuple<>).Assembly.Location));
    }

    private static ImmutableArray<MetadataReference> GetPlatformReferences()
    {
        var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        Assert.NotNull(trustedPlatformAssemblies);

        return trustedPlatformAssemblies!
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();
    }

    private static string GetEditorSourcePath(string fileName)
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Scripts/Editor", fileName));
    }

    private sealed class LoadedAssembly : IDisposable
    {
        private readonly AssemblyLoadContext _loadContext;

        public LoadedAssembly(byte[] assemblyBytes, ImmutableDictionary<string, string> runtimeAssemblyPaths)
        {
            _loadContext = new AssemblyLoadContext(Guid.NewGuid().ToString("N"), isCollectible: true);
            _loadContext.Resolving += (_, assemblyName) =>
            {
                if (runtimeAssemblyPaths.TryGetValue(assemblyName.Name ?? string.Empty, out var assemblyPath))
                {
                    return _loadContext.LoadFromAssemblyPath(assemblyPath);
                }

                return null;
            };

            using var stream = new MemoryStream(assemblyBytes);
            Assembly = _loadContext.LoadFromStream(stream);
        }

        public Assembly Assembly { get; }

        public T InvokeStatic<T>(string typeName, string methodName, params object?[]? args)
        {
            var type = Assembly.GetType(typeName, throwOnError: true)!;
            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)!;
            return (T)method.Invoke(null, args ?? Array.Empty<object>())!;
        }

        public void Dispose()
        {
            _loadContext.Unload();
        }
    }

    private sealed record MessagePackPackage(
        string Version,
        ImmutableArray<MetadataReference> MetadataReferences,
        ImmutableDictionary<string, string> RuntimeAssemblyPaths);

    private const string RuntimeAttributeSource = """
using System;

namespace Lilja.Repository
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class EntityAttribute : Attribute {}

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class KeyAttribute : Attribute {}

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class PersistAttribute : Attribute
    {
        public PersistAttribute(int index) { }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class ToPrimitiveAttribute : Attribute {}

    [AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class FromPrimitiveAttribute : Attribute {}
}
""";

    private const string GeneratedRepositorySupportSource = """
using System;
using System.Collections.Generic;
using System.Threading;

namespace Cysharp.Threading.Tasks
{
    public readonly struct UniTask
    {
        public static UniTask CompletedTask => default;
        public static UniTask RunOnThreadPool(Action action, bool configureAwait = true, CancellationToken cancellationToken = default)
        {
            action();
            return CompletedTask;
        }

        public static UniTask<T> RunOnThreadPool<T>(Func<T> action, bool configureAwait = true, CancellationToken cancellationToken = default)
        {
            return default;
        }
    }

    public readonly struct UniTask<T>
    {
    }
}

namespace UnityEngine
{
    public static class Application
    {
        public static string persistentDataPath => string.Empty;
    }
}

namespace Lilja.Repository
{
    public interface IReadOnlyTx {}

    public interface IReadWriteTx : IReadOnlyTx {}

    public static class AtomicFileWriter
    {
        public static void WriteAllBytes(string path, byte[] bytes)
        {
        }
    }

    public abstract class PersistedKeyedRepositoryBase<TEntity, TKey, TDto>
        where TEntity : class
        where TKey : notnull
        where TDto : class
    {
        protected PersistedKeyedRepositoryBase(string filePath)
        {
            FilePath = filePath;
        }

        protected string FilePath { get; }

        public Cysharp.Threading.Tasks.UniTask InitializeAsync(CancellationToken ct = default) => default;
        public TEntity? Read(IReadOnlyTx tx, TKey key) => default;
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
        protected PersistedSingletonRepositoryBase(string filePath)
        {
            FilePath = filePath;
        }

        protected string FilePath { get; }

        public Cysharp.Threading.Tasks.UniTask InitializeAsync(CancellationToken ct = default) => default;
        public TEntity? Read(IReadOnlyTx tx) => default;
        public void Create(IReadWriteTx tx, TEntity entity) {}
        public void Update(IReadWriteTx tx, TEntity entity) {}
        public void Delete(IReadWriteTx tx) {}

        protected abstract TDto ToDto(TEntity entity);
        protected abstract TEntity FromDto(TDto dto);
        protected abstract Cysharp.Threading.Tasks.UniTask<TDto?> LoadValueAsync(CancellationToken ct);
        protected abstract Cysharp.Threading.Tasks.UniTask SaveValueAsync(TDto? value, CancellationToken ct);
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

        public static void Track(object repository, RepositoryType type)
        {
        }
    }
}
""";

    private const string EditorBridgeHarnessSource = """
using MessagePack;
using MessagePack.Formatters;

namespace Lilja.Repository.Editor
{
    public static class MessagePackBridgeHarness
    {
        public static bool ProbeIsAvailable()
        {
            _ = typeof(MessagePackSerializer);
            return MessagePackCompatibilityProbe.Create(new[] { typeof(MessagePackSerializer).Assembly }) is not null;
        }

        public static bool IsAvailable()
        {
            _ = typeof(MessagePackSerializer);
            return MessagePackReflectionBridge.IsAvailable;
        }

        public static int RoundTripValue()
        {
            var options = (MessagePackSerializerOptions?)MessagePackReflectionBridge.CreateOptions(typeof(SampleFormatter));
            if (options is null)
            {
                return -1;
            }

            var bytes = MessagePackSerializer.Serialize(new Sample(42), options);
            var value = MessagePackReflectionBridge.Deserialize(bytes, typeof(Sample), options);
            return value is Sample sample ? sample.Value : -2;
        }
    }

    internal sealed class Sample
    {
        public Sample(int value)
        {
            Value = value;
        }

        public int Value { get; }
    }

    internal sealed class SampleFormatter : IMessagePackFormatter<Sample>
    {
        private static IMessagePackFormatter<int> ResolveFormatter(MessagePackSerializerOptions options)
        {
            return options.Resolver.GetFormatter<int>() ?? throw new MessagePackSerializationException("Formatter not found for int.");
        }

        public void Serialize(ref MessagePackWriter writer, Sample value, MessagePackSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNil();
                return;
            }

            ResolveFormatter(options).Serialize(ref writer, value.Value, options);
        }

        public Sample Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null!;
            }

            return new Sample(ResolveFormatter(options).Deserialize(ref reader, options));
        }
    }
}
""";

    private const string GeneratedFixtureHarnessSource = """
using System.Collections.Generic;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using Lilja.Repository.Generated.Dtos.Demo;
using Lilja.Repository.Generated.Formatters.Demo;
using Lilja.Repository.Generated.Storage.Demo;

namespace Demo
{
    public static class GeneratedFixtureHarness
    {
        private static MessagePackSerializerOptions CreateOptions()
        {
            var resolver = CompositeResolver.Create(
                new IMessagePackFormatter[]
                {
                    new ItemStorageEnvelopeFormatter(),
                    new ItemDtoFormatter(),
                    new SettingsStorageEnvelopeFormatter(),
                    new SettingsDtoFormatter(),
                },
                new IFormatterResolver[]
                {
                    StandardResolver.Instance,
                });

            return MessagePackSerializerOptions.Standard.WithResolver(resolver);
        }

        public static byte[] SerializeKeyed()
        {
            var envelope = new ItemStorageEnvelope
            {
                Items = new List<ItemDto>
                {
                    new ItemDto
                    {
                        Id = 1,
                        Name = "Slime",
                    },
                },
            };

            return MessagePackSerializer.Serialize(envelope, CreateOptions());
        }

        public static string DeserializeKeyed(byte[] bytes)
        {
            var envelope = MessagePackSerializer.Deserialize<ItemStorageEnvelope>(bytes, CreateOptions());
            return $"{envelope.Items.Count}:{envelope.Items[0].Id}:{envelope.Items[0].Name}";
        }

        public static byte[] SerializeSingleton()
        {
            var envelope = new SettingsStorageEnvelope
            {
                HasValue = true,
                Item = new SettingsDto
                {
                    Volume = 7,
                },
            };

            return MessagePackSerializer.Serialize(envelope, CreateOptions());
        }

        public static string DeserializeSingleton(byte[] bytes)
        {
            var envelope = MessagePackSerializer.Deserialize<SettingsStorageEnvelope>(bytes, CreateOptions());
            return $"{envelope.HasValue}:{envelope.Item?.Volume}";
        }
    }
}
""";
}
