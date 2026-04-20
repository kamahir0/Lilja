using BenchmarkDotNet.Attributes;
using Cysharp.Threading.Tasks;
using Lilja.Repository;
using Lilja.Repository.Analyzer.Tests.Samples;
using Lilja.Repository.Analyzer.Tests.Samples.Repositories;
using UnityEngine;

namespace Lilja.Repository.Analyzer.Benchmarks;

[MemoryDiagnoser]
public sealed class RepositoryBenchmarks
{
    [Params(100, 1000, 10000)]
    public int EntityCount { get; set; }

    [Params(RepositoryBackendKind.InMemory, RepositoryBackendKind.Json, RepositoryBackendKind.MessagePack)]
    public RepositoryBackendKind Backend { get; set; }

    private string? _seedDirectory;
    private string? _workingDirectory;
    private IItemEntityRepository? _repository;
    private TxManager? _txManager;

    [GlobalSetup]
    public void GlobalSetup()
    {
        if (Backend == RepositoryBackendKind.InMemory)
        {
            return;
        }

        _seedDirectory = CreateTempDirectory($"seed-{Backend}-{EntityCount}");
        Application.persistentDataPath = _seedDirectory;

        var repository = CreateRepository();
        RunUniTaskSynchronously(repository.InitializeAsync());
        RunUniTaskSynchronously(PopulateRepositoryAsync(repository, EntityCount));
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        CleanupWorkingDirectory();

        if (_seedDirectory != null && Directory.Exists(_seedDirectory))
        {
            Directory.Delete(_seedDirectory, recursive: true);
        }
    }

    [IterationSetup(Target = nameof(InitializeRepository))]
    public void SetupInitializeRepository()
    {
        PrepareWorkingDirectory(copySeed: true);
        _repository = CreateRepository();
    }

    [IterationSetup(Target = nameof(KeyedSingleUpdate))]
    public void SetupSingleUpdate()
    {
        SetupLoadedRepository();
    }

    [IterationSetup(Target = nameof(KeyedDelete))]
    public void SetupDelete()
    {
        SetupLoadedRepository();
    }

    [IterationSetup(Target = nameof(ReadAll))]
    public void SetupReadAll()
    {
        SetupLoadedRepository();
    }

    private void SetupLoadedRepository()
    {
        PrepareWorkingDirectory(copySeed: true);
        _repository = CreateRepository();
        RunUniTaskSynchronously(_repository.InitializeAsync());

        if (Backend == RepositoryBackendKind.InMemory)
        {
            RunUniTaskSynchronously(PopulateRepositoryAsync(_repository, EntityCount));
        }

        _txManager = new TxManager();
    }

    [IterationSetup(Target = nameof(CommitAfterOneWrite))]
    public void SetupCommitAfterOneWrite()
    {
        PrepareWorkingDirectory(copySeed: false);
        _repository = CreateRepository();
        RunUniTaskSynchronously(_repository.InitializeAsync());
        _txManager = new TxManager();
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _repository = null;
        _txManager = null;
        CleanupWorkingDirectory();
    }

    [Benchmark]
    public Task InitializeRepository()
    {
        return AwaitUniTask(_repository!.InitializeAsync());
    }

    [Benchmark]
    public Task KeyedSingleUpdate()
    {
        return AwaitUniTask(_txManager!.BeginRWTransactionAsync(tx =>
        {
            _repository!.Update(tx, CreateEntity(TargetKey, "updated"));
        }));
    }

    [Benchmark]
    public Task KeyedDelete()
    {
        return AwaitUniTask(_txManager!.BeginRWTransactionAsync(tx =>
        {
            _repository!.Delete(tx, TargetKey);
        }));
    }

    [Benchmark]
    public int ReadAll()
    {
        var count = 0;
        _txManager!.BeginROTransaction(tx =>
        {
            count = _repository!.All(tx).Count;
        });

        return count;
    }

    [Benchmark]
    public Task CommitAfterOneWrite()
    {
        return AwaitUniTask(_txManager!.BeginRWTransactionAsync(tx =>
        {
            _repository!.Create(tx, CreateEntity(EntityCount + 1, "created"));
        }));
    }

    private int TargetKey => Math.Max(1, EntityCount / 2);

    private IItemEntityRepository CreateRepository()
    {
        return Backend switch
        {
            RepositoryBackendKind.InMemory => new InMemoryItemEntityRepository(),
            RepositoryBackendKind.Json => new JsonItemEntityRepository(),
            RepositoryBackendKind.MessagePack => new MessagePackItemEntityRepository(),
            _ => throw new InvalidOperationException($"Unsupported backend: {Backend}"),
        };
    }

    private async UniTask PopulateRepositoryAsync(IItemEntityRepository repository, int count)
    {
        var txManager = new TxManager();
        await txManager.BeginRWTransactionAsync(tx =>
        {
            for (var index = 1; index <= count; index++)
            {
                repository.Create(tx, CreateEntity(index, $"seed-{index}"));
            }
        });
    }

    private ItemEntity CreateEntity(int id, string name)
    {
        return new ItemEntity(id, name, new SampleCoordinate(id, id + 1));
    }

    private void PrepareWorkingDirectory(bool copySeed)
    {
        CleanupWorkingDirectory();
        _workingDirectory = CreateTempDirectory($"bench-{Backend}-{EntityCount}");
        Application.persistentDataPath = _workingDirectory;

        if (!copySeed)
        {
            return;
        }

        var seedFileName = GetSeedFileName();
        if (seedFileName == null || _seedDirectory == null)
        {
            return;
        }

        File.Copy(
            Path.Combine(_seedDirectory, seedFileName),
            Path.Combine(_workingDirectory, seedFileName),
            overwrite: true);
    }

    private void CleanupWorkingDirectory()
    {
        if (_workingDirectory != null && Directory.Exists(_workingDirectory))
        {
            Directory.Delete(_workingDirectory, recursive: true);
        }

        _workingDirectory = null;
    }

    private string? GetSeedFileName()
    {
        return Backend switch
        {
            RepositoryBackendKind.Json => "Lilja.Repository.Analyzer.Tests.Samples.ItemEntity.json",
            RepositoryBackendKind.MessagePack => "Lilja.Repository.Analyzer.Tests.Samples.ItemEntity.msgpack",
            _ => null,
        };
    }

    private static async Task AwaitUniTask(UniTask task)
    {
        await task;
    }

    private static void RunUniTaskSynchronously(UniTask task)
    {
        task.GetAwaiter().GetResult();
    }

    private static string CreateTempDirectory(string prefix)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "LiljaRepositoryBenchmarks",
            prefix,
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(path);
        return path;
    }
}

public enum RepositoryBackendKind
{
    InMemory,
    Json,
    MessagePack,
}
