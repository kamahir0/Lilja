using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Reflection;
using Lilja.Repository.Analyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Lilja.Repository.Analyzer.Test;

public sealed class GeneratorTests
{
    [Fact]
    public void Generates_keyed_persisted_contract_with_messagepack()
    {
        const string source = """
using System;
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
""";

        var result = RunGenerator(source, includeMessagePack: true);
        var generated = result.GeneratedSources.ToDictionary(item => item.HintName, item => item.SourceText.ToString());

        Assert.Contains("Demo.Item.IItemRepository.g.cs", generated.Keys);
        Assert.Contains("Demo.Item.InMemoryItemRepository.g.cs", generated.Keys);
        Assert.Contains("Demo.Item.JsonItemRepository.g.cs", generated.Keys);
        Assert.Contains("Demo.Item.MessagePackItemRepository.g.cs", generated.Keys);
        Assert.Contains("Demo.Item.ItemDto.g.cs", generated.Keys);
        Assert.Contains("Demo.Item.ItemStorageEnvelope.g.cs", generated.Keys);
        Assert.Contains("Demo.Item.ItemDtoFormatter.g.cs", generated.Keys);
        Assert.Contains("Demo.Item.ItemStorageEnvelopeFormatter.g.cs", generated.Keys);
        Assert.Contains("Demo.Item.Item.Converter.g.cs", generated.Keys);
        Assert.Contains("Demo.Item.Item.KeyAccessor.g.cs", generated.Keys);
        Assert.Contains("public int Position_x = default!;", generated["Demo.Item.ItemDto.g.cs"]);
        Assert.Contains("Demo.Item.json", generated["Demo.Item.JsonItemRepository.g.cs"]);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Generates_singleton_persisted_contract_without_key_accessor()
    {
        const string source = """
using Lilja.Repository;

namespace Demo.Profile;

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

        var result = RunGenerator(source, includeMessagePack: false);
        var generatedNames = result.GeneratedSources.Select(item => item.HintName).ToArray();

        Assert.Contains("Demo.Profile.Settings.ISettingsRepository.g.cs", generatedNames);
        Assert.Contains("Demo.Profile.Settings.InMemorySettingsRepository.g.cs", generatedNames);
        Assert.Contains("Demo.Profile.Settings.JsonSettingsRepository.g.cs", generatedNames);
        Assert.Contains("Demo.Profile.Settings.SettingsDto.g.cs", generatedNames);
        Assert.Contains("Demo.Profile.Settings.SettingsStorageEnvelope.g.cs", generatedNames);
        Assert.DoesNotContain("Demo.Profile.Settings.Settings.KeyAccessor.g.cs", generatedNames);
    }

    [Fact]
    public void Reports_missing_partial_diagnostic()
    {
        const string source = """
using Lilja.Repository;

[Entity]
public class Item
{
}
""";

        var result = RunGenerator(source, includeMessagePack: false);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "LILJAREPO001");
    }

    [Fact]
    public void Reports_missing_persist_on_key_for_persisted_entity()
    {
        const string source = """
using Lilja.Repository;

namespace Demo;

[Entity]
public partial class Item
{
    [Key]
    public int Id { get; }

    [Persist(0)]
    public string Name { get; }

    public Item(int id, string name)
    {
        Id = id;
        Name = name;
    }
}
""";

        var result = RunGenerator(source, includeMessagePack: false);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "LILJAREPO006");
    }

    [Fact]
    public void Reports_invalid_to_primitive_definition()
    {
        const string source = """
using Lilja.Repository;

namespace Demo;

public readonly struct Coordinate
{
    [FromPrimitive]
    public Coordinate(int x, int y)
    {
    }

    [ToPrimitive]
    public static (int x, int y) ToPrimitive() => (0, 0);
}

[Entity]
public partial class Item
{
    [Persist(0)]
    public Coordinate Position { get; }

    public Item(Coordinate position)
    {
        Position = position;
    }
}
""";

        var result = RunGenerator(source, includeMessagePack: false);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "LILJAREPO007");
    }

    [Fact]
    public void Runtime_and_editor_sources_compile_against_unity_stubs()
    {
        var scriptsRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Scripts"));
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: new[] { "UNITY_EDITOR" });
        var syntaxTrees = new List<SyntaxTree>
        {
            CSharpSyntaxTree.ParseText(UnityStubSource, parseOptions),
            CSharpSyntaxTree.ParseText(UniTaskStubSource, parseOptions),
        };

        foreach (var filePath in Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
        {
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(filePath), parseOptions, filePath));
        }

        var compilation = CSharpCompilation.Create(
            "Lilja.Repository.RuntimeCompilation",
            syntaxTrees,
            GetPlatformReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var diagnostics = compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Generated_sources_compile_against_runtime_sources_and_messagepack_stubs()
    {
        const string source = """
using Lilja.Repository;

namespace Demo;

[Entity]
public partial class Monster
{
    [Key]
    [Persist(0)]
    public int Id { get; }

    [Persist(1)]
    public string Name { get; }

    public Monster(int id, string name)
    {
        Id = id;
        Name = name;
    }
}
""";

        var result = RunGenerator(source, includeMessagePack: true);
        var scriptsRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Scripts"));
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: new[] { "UNITY_EDITOR" });
        var syntaxTrees = new List<SyntaxTree>
        {
            CSharpSyntaxTree.ParseText(UnityStubSource, parseOptions),
            CSharpSyntaxTree.ParseText(UniTaskStubSource, parseOptions),
            CSharpSyntaxTree.ParseText(MessagePackStubSource, parseOptions),
            CSharpSyntaxTree.ParseText(source, parseOptions),
        };

        foreach (var filePath in Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
        {
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(filePath), parseOptions, filePath));
        }

        foreach (var generatedSource in result.GeneratedSources)
        {
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(generatedSource.SourceText.ToString(), parseOptions, generatedSource.HintName));
        }

        var compilation = CSharpCompilation.Create(
            "Lilja.Repository.GeneratedCompilation",
            syntaxTrees,
            GetPlatformReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var diagnostics = compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.Empty(diagnostics);
    }

    private static GeneratorRunResult RunGenerator(string source, bool includeMessagePack)
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
            "GeneratorTests",
            syntaxTrees,
            GetReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new LiljaRepositoryGenerator());
        driver = driver.RunGenerators(compilation);
        return driver.GetRunResult().Results.Single();
    }

    private static ImmutableArray<MetadataReference> GetReferences()
    {
        return ImmutableArray.Create<MetadataReference>(
            MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).GetTypeInfo().Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).GetTypeInfo().Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ValueTuple<>).GetTypeInfo().Assembly.Location));
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

    private const string RuntimeStubSource = """
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

    private const string MessagePackStubSource = """
using System;
using System.Collections.Generic;

namespace MessagePack
{
    public interface IFormatterResolver
    {
        MessagePack.Formatters.IMessagePackFormatter<T>? GetFormatter<T>();
    }

    public sealed class MessagePackSerializerOptions
    {
        public static MessagePackSerializerOptions Standard { get; } = new MessagePackSerializerOptions(Resolvers.StandardResolver.Instance);

        public MessagePackSerializerOptions(IFormatterResolver resolver)
        {
            Resolver = resolver;
        }

        public IFormatterResolver Resolver { get; }

        public MessagePackSerializerOptions WithResolver(IFormatterResolver resolver) => new MessagePackSerializerOptions(resolver);
    }

    public static class MessagePackSerializer
    {
        public static byte[] Serialize<T>(T value, MessagePackSerializerOptions options) => Array.Empty<byte>();
        public static T Deserialize<T>(byte[] bytes, MessagePackSerializerOptions options) => default!;
    }

    public struct MessagePackWriter
    {
        public void WriteNil()
        {
        }

        public void WriteArrayHeader(int count)
        {
        }
    }

    public struct MessagePackReader
    {
        public bool TryReadNil() => false;
        public int ReadArrayHeader() => 0;

        public void Skip()
        {
        }
    }

    public class MessagePackSerializationException : Exception
    {
        public MessagePackSerializationException()
        {
        }

        public MessagePackSerializationException(string message)
            : base(message)
        {
        }
    }
}

namespace MessagePack.Formatters
{
    public interface IMessagePackFormatter
    {
    }

    public interface IMessagePackFormatter<T> : IMessagePackFormatter
    {
        void Serialize(ref MessagePack.MessagePackWriter writer, T value, MessagePack.MessagePackSerializerOptions options);
        T Deserialize(ref MessagePack.MessagePackReader reader, MessagePack.MessagePackSerializerOptions options);
    }
}

namespace MessagePack.Resolvers
{
    using MessagePack.Formatters;

    public sealed class CompositeResolver
    {
        public static MessagePack.IFormatterResolver Create(IReadOnlyList<IMessagePackFormatter> formatters, IReadOnlyList<MessagePack.IFormatterResolver> resolvers)
        {
            return StandardResolver.Instance;
        }
    }

    public sealed class StandardResolver : MessagePack.IFormatterResolver
    {
        public static StandardResolver Instance { get; } = new StandardResolver();

        public IMessagePackFormatter<T>? GetFormatter<T>()
        {
            return default;
        }
    }
}
""";

    private const string UniTaskStubSource = """
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Cysharp.Threading.Tasks
{
    [AsyncMethodBuilder(typeof(AsyncUniTaskMethodBuilder))]
    public readonly struct UniTask
    {
        private readonly Task _task;

        public UniTask(Task task)
        {
            _task = task;
        }

        public static UniTask CompletedTask => new UniTask(Task.CompletedTask);

        public static UniTask RunOnThreadPool(Action action, bool configureAwait = true, CancellationToken cancellationToken = default)
        {
            action();
            return CompletedTask;
        }

        public static UniTask<T> RunOnThreadPool<T>(Func<T> action, bool configureAwait = true, CancellationToken cancellationToken = default)
        {
            return new UniTask<T>(Task.FromResult(action()));
        }

        public Awaiter GetAwaiter() => new Awaiter(_task ?? Task.CompletedTask);

        public readonly struct Awaiter : ICriticalNotifyCompletion
        {
            private readonly TaskAwaiter _awaiter;

            public Awaiter(Task task)
            {
                _awaiter = task.GetAwaiter();
            }

            public bool IsCompleted => _awaiter.IsCompleted;

            public void GetResult()
            {
                _awaiter.GetResult();
            }

            public void OnCompleted(Action continuation)
            {
                _awaiter.OnCompleted(continuation);
            }

            public void UnsafeOnCompleted(Action continuation)
            {
                _awaiter.UnsafeOnCompleted(continuation);
            }
        }
    }

    [AsyncMethodBuilder(typeof(AsyncUniTaskMethodBuilder<>))]
    public readonly struct UniTask<T>
    {
        private readonly Task<T> _task;

        public UniTask(Task<T> task)
        {
            _task = task;
        }

        public Awaiter GetAwaiter() => new Awaiter(_task ?? Task.FromResult(default(T)!));

        public readonly struct Awaiter : ICriticalNotifyCompletion
        {
            private readonly TaskAwaiter<T> _awaiter;

            public Awaiter(Task<T> task)
            {
                _awaiter = task.GetAwaiter();
            }

            public bool IsCompleted => _awaiter.IsCompleted;

            public T GetResult() => _awaiter.GetResult();

            public void OnCompleted(Action continuation)
            {
                _awaiter.OnCompleted(continuation);
            }

            public void UnsafeOnCompleted(Action continuation)
            {
                _awaiter.UnsafeOnCompleted(continuation);
            }
        }
    }

    public struct AsyncUniTaskMethodBuilder
    {
        private AsyncTaskMethodBuilder _builder;

        public static AsyncUniTaskMethodBuilder Create()
        {
            return new AsyncUniTaskMethodBuilder
            {
                _builder = AsyncTaskMethodBuilder.Create(),
            };
        }

        public UniTask Task => new UniTask(_builder.Task);

        public void SetException(Exception exception) => _builder.SetException(exception);
        public void SetResult() => _builder.SetResult();
        public void SetStateMachine(IAsyncStateMachine stateMachine) => _builder.SetStateMachine(stateMachine);
        public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine => _builder.Start(ref stateMachine);
        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine => _builder.AwaitOnCompleted(ref awaiter, ref stateMachine);
        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine => _builder.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);
    }

    public struct AsyncUniTaskMethodBuilder<T>
    {
        private AsyncTaskMethodBuilder<T> _builder;

        public static AsyncUniTaskMethodBuilder<T> Create()
        {
            return new AsyncUniTaskMethodBuilder<T>
            {
                _builder = AsyncTaskMethodBuilder<T>.Create(),
            };
        }

        public UniTask<T> Task => new UniTask<T>(_builder.Task);

        public void SetException(Exception exception) => _builder.SetException(exception);
        public void SetResult(T result) => _builder.SetResult(result);
        public void SetStateMachine(IAsyncStateMachine stateMachine) => _builder.SetStateMachine(stateMachine);
        public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine => _builder.Start(ref stateMachine);
        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine => _builder.AwaitOnCompleted(ref awaiter, ref stateMachine);
        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine => _builder.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);
    }
}
""";

    private const string UnityStubSource = """
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public struct Vector2
    {
        public float x;
        public float y;
    }

    public struct Rect
    {
        public float width;
    }

    public sealed class GUILayoutOption
    {
    }

    public static class Application
    {
        public static string persistentDataPath => ".";
        public static bool isPlaying => false;
    }

    public static class Debug
    {
        public static void Log(object message)
        {
        }

        public static void LogWarning(object message)
        {
        }
    }

    public static class JsonUtility
    {
        public static string ToJson(object obj, bool prettyPrint = false) => string.Empty;
        public static T? FromJson<T>(string json) => default;
        public static object? FromJson(string json, Type type) => null;
    }
}

namespace UnityEditor
{
    using UnityEngine;

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MenuItemAttribute : Attribute
    {
        public MenuItemAttribute(string itemName)
        {
        }
    }

    public enum MessageType
    {
        Info,
    }

    public class EditorWindow
    {
        public Rect position => default;

        protected static T GetWindow<T>(string title) where T : EditorWindow, new()
        {
            return new T();
        }

        protected void Repaint()
        {
        }
    }

    public static class EditorStyles
    {
        public static object toolbar => new object();
        public static object toolbarPopup => new object();
        public static object toolbarButton => new object();
        public static object helpBox => new object();
        public static object boldLabel => new object();
    }

    public static class EditorGUILayout
    {
        public sealed class HorizontalScope : IDisposable
        {
            public HorizontalScope(params object[] options)
            {
            }

            public void Dispose()
            {
            }
        }

        public sealed class VerticalScope : IDisposable
        {
            public VerticalScope(params object[] options)
            {
            }

            public void Dispose()
            {
            }
        }

        public static Enum EnumPopup(Enum selected, params object[] options) => selected;
        public static void Space()
        {
        }

        public static void LabelField(string label, params object[] options)
        {
        }

        public static void HelpBox(string message, MessageType messageType)
        {
        }

        public static Vector2 BeginScrollView(Vector2 scrollPosition, params GUILayoutOption[] options) => scrollPosition;
        public static void EndScrollView()
        {
        }

        public static string TextArea(string text, params GUILayoutOption[] options) => text;
    }

    public static class GUILayout
    {
        public static bool Button(string text, params object[] options) => false;
        public static bool Toggle(bool value, string text, string style) => value;
        public static void FlexibleSpace()
        {
        }

        public static GUILayoutOption Width(float width) => new GUILayoutOption();
        public static GUILayoutOption ExpandHeight(bool expand) => new GUILayoutOption();
    }

    public static class EditorUtility
    {
        public static void RevealInFinder(string path)
        {
        }
    }
}
""";
}
