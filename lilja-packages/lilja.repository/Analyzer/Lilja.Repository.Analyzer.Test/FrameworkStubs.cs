using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace Cysharp.Threading.Tasks
{
    [AsyncMethodBuilder(typeof(AsyncUniTaskMethodBuilder))]
    public readonly struct UniTask
    {
        private readonly Task _task;

        public UniTask(Task task)
        {
            _task = task ?? Task.CompletedTask;
        }

        public static UniTask CompletedTask => new UniTask(Task.CompletedTask);

        public TaskAwaiter GetAwaiter()
        {
            return (_task ?? Task.CompletedTask).GetAwaiter();
        }

        public Task AsTask()
        {
            return _task ?? Task.CompletedTask;
        }

        public static UniTask RunOnThreadPool(Action action)
        {
            return new UniTask(Task.Run(action));
        }

        public static UniTask<T> RunOnThreadPool<T>(Func<T> func)
        {
            return new UniTask<T>(Task.Run(func));
        }
    }

    [AsyncMethodBuilder(typeof(AsyncUniTaskMethodBuilder<>))]
    public readonly struct UniTask<T>
    {
        private readonly Task<T> _task;

        public UniTask(Task<T> task)
        {
            _task = task ?? Task.FromResult(default(T)!);
        }

        public TaskAwaiter<T> GetAwaiter()
        {
            return (_task ?? Task.FromResult(default(T)!)).GetAwaiter();
        }

        public Task<T> AsTask()
        {
            return _task ?? Task.FromResult(default(T)!);
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

        public void Start<TStateMachine>(ref TStateMachine stateMachine)
            where TStateMachine : IAsyncStateMachine
        {
            _builder.Start(ref stateMachine);
        }

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            _builder.AwaitOnCompleted(ref awaiter, ref stateMachine);
        }

        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            _builder.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);
        }
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

        public void Start<TStateMachine>(ref TStateMachine stateMachine)
            where TStateMachine : IAsyncStateMachine
        {
            _builder.Start(ref stateMachine);
        }

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            _builder.AwaitOnCompleted(ref awaiter, ref stateMachine);
        }

        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            _builder.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);
        }
    }
}

namespace UnityEngine
{
    public static class Application
    {
        public static string persistentDataPath { get; set; } =
            Path.Combine(Path.GetTempPath(), "LiljaRepositoryAnalyzerTests");

        public static bool isPlaying { get; set; } = true;
    }

    public static class Debug
    {
        public static List<string> LogMessages { get; } = new List<string>();

        public static List<string> ErrorMessages { get; } = new List<string>();

        public static List<string> WarningMessages { get; } = new List<string>();

        public static void Log(string message)
        {
            LogMessages.Add(message);
        }

        public static void LogError(string message)
        {
            ErrorMessages.Add(message);
        }

        public static void LogWarning(string message)
        {
            WarningMessages.Add(message);
        }

        public static void ResetTestState()
        {
            LogMessages.Clear();
            ErrorMessages.Clear();
            WarningMessages.Clear();
        }
    }

    public static class JsonUtility
    {
        private static readonly JsonSerializerOptions DefaultOptions = new JsonSerializerOptions
        {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true,
        };

        public static string ToJson(object obj, bool prettyPrint = false)
        {
            if (obj == null)
            {
                return "null";
            }

            var options = new JsonSerializerOptions(DefaultOptions)
            {
                WriteIndented = prettyPrint,
            };
            return JsonSerializer.Serialize(obj, obj.GetType(), options);
        }

        public static T FromJson<T>(string json)
        {
            return string.IsNullOrWhiteSpace(json)
                ? default!
                : JsonSerializer.Deserialize<T>(json, DefaultOptions)!;
        }

        public static object? FromJson(string json, Type type)
        {
            return string.IsNullOrWhiteSpace(json)
                ? null
                : JsonSerializer.Deserialize(json, type, DefaultOptions);
        }
    }
}

namespace MessagePack
{
    public interface IFormatterResolver
    {
    }

    public readonly struct MessagePackSerializerOptions
    {
        private readonly IFormatterResolver _resolver;

        public MessagePackSerializerOptions(IFormatterResolver resolver)
        {
            _resolver = resolver;
        }

        public static MessagePackSerializerOptions Standard =>
            new MessagePackSerializerOptions(TestFormatterResolver.Instance);

        public IFormatterResolver Resolver => _resolver ?? TestFormatterResolver.Instance;

        public MessagePackSerializerOptions WithResolver(IFormatterResolver resolver)
        {
            return new MessagePackSerializerOptions(resolver);
        }
    }

    public static class FormatterResolverExtensions
    {
        public static MessagePack.Formatters.IMessagePackFormatter<T> GetFormatterWithVerify<T>(this IFormatterResolver resolver)
        {
            return new MessagePack.Formatters.NoOpFormatter<T>();
        }
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
        public bool TryReadNil()
        {
            return false;
        }

        public int ReadArrayHeader()
        {
            return 0;
        }

        public void Skip()
        {
        }
    }

    public static class MessagePackSerializer
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true,
        };

        public static byte[] Serialize<T>(T value, MessagePackSerializerOptions options = default)
        {
            return JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        }

        public static T Deserialize<T>(byte[] bytes, MessagePackSerializerOptions options = default)
        {
            return JsonSerializer.Deserialize<T>(bytes, JsonOptions)!;
        }
    }

    internal sealed class TestFormatterResolver : IFormatterResolver
    {
        public static readonly TestFormatterResolver Instance = new TestFormatterResolver();

        private TestFormatterResolver()
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

        T? Deserialize(ref MessagePack.MessagePackReader reader, MessagePack.MessagePackSerializerOptions options);
    }

    public sealed class NoOpFormatter<T> : IMessagePackFormatter<T>
    {
        public void Serialize(ref MessagePack.MessagePackWriter writer, T value, MessagePack.MessagePackSerializerOptions options)
        {
        }

        public T? Deserialize(ref MessagePack.MessagePackReader reader, MessagePack.MessagePackSerializerOptions options)
        {
            return default!;
        }
    }
}

namespace MessagePack.Resolvers
{
    public static class CompositeResolver
    {
        public static MessagePack.IFormatterResolver Create(
            MessagePack.Formatters.IMessagePackFormatter[] formatters,
            MessagePack.IFormatterResolver[] resolvers)
        {
            return MessagePack.TestFormatterResolver.Instance;
        }
    }

    public static class StandardResolver
    {
        public static MessagePack.IFormatterResolver Instance => MessagePack.TestFormatterResolver.Instance;
    }
}

namespace UnityEditor.IMGUI.Controls
{
    public class TreeViewItem<T>
    {
        public TreeViewItem()
        {
        }

        public TreeViewItem(T id)
        {
            this.id = id;
        }

        public T id { get; set; } = default!;

        public int depth { get; set; }

        public List<TreeViewItem<T>>? children { get; set; }

        public bool hasChildren => children != null && children.Count > 0;
    }
}
