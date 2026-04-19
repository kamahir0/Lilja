using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Lilja.Repository;
using Lilja.Repository.Analyzer.Tests.Samples;
using Lilja.Repository.Analyzer.Tests.Samples.Repositories;
using Lilja.Repository.Diagnostics;
using Lilja.Repository.Generated.Dtos.Lilja.Repository.Analyzer.Tests.Samples;
using Lilja.Repository.Generated.Formatters.Lilja.Repository.Analyzer.Tests.Samples;
using Lilja.Repository.Generated.Storage.Lilja.Repository.Analyzer.Tests.Samples;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using UnityEngine;
using Xunit;

namespace Lilja.Repository.Analyzer.Test;

public sealed class RuntimeRepositoryTests
{
    public RuntimeRepositoryTests()
    {
        Debug.ResetTestState();
        RuntimeInstanceMonitor.ResetForTests();
    }

    [Fact]
    public async Task InMemoryRepository_StagesWritesInsideRwTransaction()
    {
        var txManager = new TxManager();
        var repository = new InMemoryItemEntityRepository();
        var item = CreateItem(1, "sword");

        await txManager.BeginRWTransactionAsync(async tx =>
        {
            repository.Create(tx, item);

            Assert.NotNull(repository.Read(tx, 1));

            txManager.BeginROTransaction(ro =>
            {
                Assert.Null(repository.Read(ro, 1));
            });

            await UniTask.CompletedTask;
        });

        txManager.BeginROTransaction(ro =>
        {
            var committed = repository.Read(ro, 1);
            Assert.NotNull(committed);
            Assert.Equal("sword", committed!.Name);
        });
    }

    [Fact]
    public async Task InMemoryRepository_RollsBackOnFailure()
    {
        var txManager = new TxManager();
        var repository = new InMemoryItemEntityRepository();

        await txManager.BeginRWTransactionAsync(tx =>
        {
            repository.Create(tx, CreateItem(1, "before"));
        });

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await txManager.BeginRWTransactionAsync(async tx =>
            {
                repository.Update(tx, CreateItem(1, "after"));
                throw new InvalidOperationException("rollback");
            });
        });

        txManager.BeginROTransaction(ro =>
        {
            var item = repository.Read(ro, 1);
            Assert.NotNull(item);
            Assert.Equal("before", item!.Name);
        });
    }

    [Fact]
    public async Task JsonRepository_RequiresInitializeAsync()
    {
        Application.persistentDataPath = CreateTempDataPath();
        var repository = new JsonItemEntityRepository();
        var txManager = new TxManager();

        txManager.BeginROTransaction(tx =>
        {
            Assert.Throws<InvalidOperationException>(() => repository.Read(tx, 1));
        });

        await repository.InitializeAsync();

        await txManager.BeginRWTransactionAsync(tx =>
        {
            repository.Create(tx, CreateItem(1, "json"));
        });

        txManager.BeginROTransaction(tx =>
        {
            var item = repository.Read(tx, 1);
            Assert.NotNull(item);
            Assert.Equal("json", item!.Name);
        });
    }

    [Fact]
    public async Task JsonRepository_InitializeAsync_IsConcurrentSafe()
    {
        var dataPath = CreateTempDataPath();
        Application.persistentDataPath = dataPath;

        var dto = ItemEntity.ToDto(CreateItem(5, "preloaded"));
        File.WriteAllText(
            Path.Combine(dataPath, "ItemEntity.json"),
            JsonUtility.ToJson(
                new ItemEntityStorageEnvelope
                {
                    Items = new List<ItemEntityDto> { dto },
                },
                false));

        var repository = new JsonItemEntityRepository();
        await Task.WhenAll(
            AwaitUniTask(repository.InitializeAsync()),
            AwaitUniTask(repository.InitializeAsync()),
            AwaitUniTask(repository.InitializeAsync()));

        var txManager = new TxManager();
        txManager.BeginROTransaction(tx =>
        {
            var item = repository.Read(tx, 5);
            Assert.NotNull(item);
            Assert.Equal("preloaded", item!.Name);
        });
    }

    [Fact]
    public async Task JsonRepository_InitializeAsync_CanRetryAfterFailure()
    {
        var dataPath = CreateTempDataPath();
        Application.persistentDataPath = dataPath;
        var filePath = Path.Combine(dataPath, "ItemEntity.json");
        File.WriteAllText(filePath, "{ invalid json");

        var repository = new JsonItemEntityRepository();
        await Assert.ThrowsAnyAsync<Exception>(async () => await repository.InitializeAsync());

        File.WriteAllText(
            filePath,
            JsonUtility.ToJson(
                new ItemEntityStorageEnvelope
                {
                    Items = new List<ItemEntityDto>
                    {
                        ItemEntity.ToDto(CreateItem(9, "recovered")),
                    },
                },
                false));

        await repository.InitializeAsync();

        var txManager = new TxManager();
        txManager.BeginROTransaction(tx =>
        {
            var item = repository.Read(tx, 9);
            Assert.NotNull(item);
            Assert.Equal("recovered", item!.Name);
        });
    }

    [Fact]
    public async Task JsonRepository_DoesNotSwapCommittedStateWhenPersistFails()
    {
        var dataPath = CreateTempDataPath();
        Application.persistentDataPath = dataPath;

        var repository = new JsonItemEntityRepository();
        var txManager = new TxManager();
        await repository.InitializeAsync();

        await txManager.BeginRWTransactionAsync(tx =>
        {
            repository.Create(tx, CreateItem(1, "stable"));
        });

        Directory.Delete(dataPath, recursive: true);

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await txManager.BeginRWTransactionAsync(tx =>
            {
                repository.Update(tx, CreateItem(1, "unstable"));
            });
        });

        txManager.BeginROTransaction(tx =>
        {
            var item = repository.Read(tx, 1);
            Assert.NotNull(item);
            Assert.Equal("stable", item!.Name);
        });
    }

    [Fact]
    public async Task JsonKeyedRepository_OverlayUpdatePreservesUntouchedCommittedEntries()
    {
        var dataPath = CreateTempDataPath();
        Application.persistentDataPath = dataPath;

        var repository = new JsonItemEntityRepository();
        var txManager = new TxManager();
        await repository.InitializeAsync();

        await txManager.BeginRWTransactionAsync(tx =>
        {
            repository.Create(tx, CreateItem(1, "sword"));
            repository.Create(tx, CreateItem(2, "shield"));
        });

        await txManager.BeginRWTransactionAsync(tx =>
        {
            repository.Update(tx, CreateItem(1, "upgraded"));
        });

        txManager.BeginROTransaction(tx =>
        {
            var updated = repository.Read(tx, 1);
            var untouched = repository.Read(tx, 2);

            Assert.NotNull(updated);
            Assert.NotNull(untouched);
            Assert.Equal("upgraded", updated!.Name);
            Assert.Equal("shield", untouched!.Name);
        });
    }

    [Fact]
    public async Task JsonKeyedRepository_DeleteIsHiddenInsideRwTransactionAndAfterCommit()
    {
        var dataPath = CreateTempDataPath();
        Application.persistentDataPath = dataPath;

        var repository = new JsonItemEntityRepository();
        var txManager = new TxManager();
        await repository.InitializeAsync();

        await txManager.BeginRWTransactionAsync(tx =>
        {
            repository.Create(tx, CreateItem(1, "sword"));
            repository.Create(tx, CreateItem(2, "shield"));
        });

        await txManager.BeginRWTransactionAsync(async tx =>
        {
            repository.Delete(tx, 2);

            Assert.Null(repository.Read(tx, 2));
            Assert.Single(repository.All(tx));

            txManager.BeginROTransaction(ro =>
            {
                var committed = repository.Read(ro, 2);
                Assert.NotNull(committed);
                Assert.Equal("shield", committed!.Name);
            });

            await UniTask.CompletedTask;
        });

        txManager.BeginROTransaction(tx =>
        {
            Assert.Null(repository.Read(tx, 2));
            Assert.Single(repository.All(tx));
        });
    }

    [Fact]
    public async Task JsonKeyedRepository_CreateUpdateDeleteWithinSingleTransactionLeavesNoCommittedRecord()
    {
        var dataPath = CreateTempDataPath();
        Application.persistentDataPath = dataPath;

        var repository = new JsonItemEntityRepository();
        var txManager = new TxManager();
        await repository.InitializeAsync();

        await txManager.BeginRWTransactionAsync(tx =>
        {
            repository.Create(tx, CreateItem(10, "alpha"));
            repository.Update(tx, CreateItem(10, "beta"));
            repository.Delete(tx, 10);

            Assert.Null(repository.Read(tx, 10));
            Assert.Empty(repository.All(tx));
        });

        txManager.BeginROTransaction(tx =>
        {
            Assert.Null(repository.Read(tx, 10));
            Assert.Empty(repository.All(tx));
        });
    }

    [Fact]
    public async Task ReadOnlyTransaction_KeepsSnapshotAndBlocksPublishUntilDisposed()
    {
        var txManager = new TxManager();
        var repository = new InMemoryItemEntityRepository();
        await txManager.BeginRWTransactionAsync(tx =>
        {
            repository.Create(tx, CreateItem(1, "before"));
        });

        var readerEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseReader = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstReader = txManager.BeginROTransactionAsync(async ro =>
        {
            var first = repository.Read(ro, 1);
            Assert.NotNull(first);
            Assert.Equal("before", first!.Name);
            readerEntered.TrySetResult(true);

            await releaseReader.Task;

            var second = repository.Read(ro, 1);
            Assert.NotNull(second);
            Assert.Equal("before", second!.Name);
        });

        await readerEntered.Task;

        var writerTask = AwaitUniTask(txManager.BeginRWTransactionAsync(tx =>
        {
            repository.Update(tx, CreateItem(1, "after"));
        }));

        await Task.Delay(50);
        Assert.False(writerTask.IsCompleted);

        releaseReader.TrySetResult(true);
        await Task.WhenAll(AwaitUniTask(firstReader), writerTask);

        txManager.BeginROTransaction(ro =>
        {
            var committed = repository.Read(ro, 1);
            Assert.NotNull(committed);
            Assert.Equal("after", committed!.Name);
        });
    }

    [Fact]
    public async Task NewReadersWaitWhileCommitIsPublishing()
    {
        var txManager = new TxManager();
        var repository = new InMemoryItemEntityRepository();
        await txManager.BeginRWTransactionAsync(tx =>
        {
            repository.Create(tx, CreateItem(1, "before"));
        });

        var releaseFirstReader = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstReaderStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstReaderTask = txManager.BeginROTransactionAsync(async ro =>
        {
            Assert.Equal("before", repository.Read(ro, 1)!.Name);
            firstReaderStarted.TrySetResult(true);
            await releaseFirstReader.Task;
        });

        await firstReaderStarted.Task;

        var writerTask = AwaitUniTask(txManager.BeginRWTransactionAsync(tx =>
        {
            repository.Update(tx, CreateItem(1, "after"));
        }));

        await Task.Delay(50);

        var secondReaderTask = AwaitUniTask(txManager.BeginROTransactionAsync(ro =>
        {
            Assert.Equal("after", repository.Read(ro, 1)!.Name);
            return UniTask.CompletedTask;
        }));

        await Task.Delay(50);
        Assert.False(writerTask.IsCompleted);
        Assert.False(secondReaderTask.IsCompleted);

        releaseFirstReader.TrySetResult(true);
        await Task.WhenAll(AwaitUniTask(firstReaderTask), writerTask, secondReaderTask);
    }

    [Fact]
    public async Task MessagePackRepository_DoesNotSwapCommittedStateWhenPersistFails()
    {
        var dataPath = CreateTempDataPath();
        Application.persistentDataPath = dataPath;

        var repository = new MessagePackItemEntityRepository();
        var txManager = new TxManager();
        await repository.InitializeAsync();

        await txManager.BeginRWTransactionAsync(tx =>
        {
            repository.Create(tx, CreateItem(7, "packed"));
        });

        Directory.Delete(dataPath, recursive: true);

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await txManager.BeginRWTransactionAsync(tx =>
            {
                repository.Update(tx, CreateItem(7, "broken"));
            });
        });

        txManager.BeginROTransaction(tx =>
        {
            var item = repository.Read(tx, 7);
            Assert.NotNull(item);
            Assert.Equal("packed", item!.Name);
        });
    }

    [Fact]
    public async Task JsonRepository_PersistsEnvelopeFormat()
    {
        var dataPath = CreateTempDataPath();
        Application.persistentDataPath = dataPath;

        var repository = new JsonItemEntityRepository();
        var txManager = new TxManager();
        await repository.InitializeAsync();

        await txManager.BeginRWTransactionAsync(tx =>
        {
            repository.Create(tx, CreateItem(1, "json-one"));
            repository.Create(tx, CreateItem(2, "json-two"));
        });

        var filePath = Path.Combine(dataPath, "ItemEntity.json");
        var envelope = JsonUtility.FromJson<ItemEntityStorageEnvelope>(File.ReadAllText(filePath));

        Assert.NotNull(envelope);
        Assert.Equal(2, envelope!.Items.Count);
    }

    [Fact]
    public async Task MessagePackRepository_PersistsEnvelopeFormat()
    {
        var dataPath = CreateTempDataPath();
        Application.persistentDataPath = dataPath;

        var repository = new MessagePackItemEntityRepository();
        var txManager = new TxManager();
        await repository.InitializeAsync();

        await txManager.BeginRWTransactionAsync(tx =>
        {
            repository.Create(tx, CreateItem(1, "msg-one"));
            repository.Create(tx, CreateItem(2, "msg-two"));
        });

        var bytes = File.ReadAllBytes(Path.Combine(dataPath, "ItemEntity.msgpack"));
        var envelope = MessagePackSerializer.Deserialize<ItemEntityStorageEnvelope>(bytes, CreateItemEntityMessagePackOptions());

        Assert.NotNull(envelope);
        Assert.Equal(2, envelope.Items.Count);
    }

    [Fact]
    public async Task SingletonRepository_PersistsEnvelopeWhenDeleted()
    {
        var dataPath = CreateTempDataPath();
        Application.persistentDataPath = dataPath;

        var repository = new JsonSettingsEntityRepository();
        var txManager = new TxManager();
        await repository.InitializeAsync();

        await txManager.BeginRWTransactionAsync(tx =>
        {
            repository.Create(tx, new SettingsEntity(10));
        });

        await txManager.BeginRWTransactionAsync(tx =>
        {
            repository.Delete(tx);
        });

        txManager.BeginROTransaction(tx =>
        {
            Assert.Null(repository.Read(tx));
        });

        var filePath = Path.Combine(dataPath, "SettingsEntity.json");
        Assert.True(File.Exists(filePath));

        var envelope = JsonUtility.FromJson<SettingsEntityStorageEnvelope>(File.ReadAllText(filePath));
        Assert.NotNull(envelope);
        Assert.NotNull(envelope);
        Assert.False(envelope.HasValue);
        Assert.Null(envelope.Item);
    }

    [Fact]
    public void DuplicateTxManagers_LogWarning()
    {
        _ = new TxManager();
        _ = new TxManager();

        Assert.Contains(Debug.WarningMessages, message => message.Contains("Multiple TxManager instances", StringComparison.Ordinal));
    }

    [Fact]
    public void DuplicatePersistedRepositories_LogWarning()
    {
        Application.persistentDataPath = CreateTempDataPath();

        _ = new JsonItemEntityRepository();
        _ = new JsonItemEntityRepository();

        Assert.Contains(Debug.WarningMessages, message => message.Contains("Multiple persisted repository instances", StringComparison.Ordinal));
    }

    private static ItemEntity CreateItem(int id, string name)
    {
        return new ItemEntity(id, name, new SampleCoordinate(id, id + 1));
    }

    private static MessagePackSerializerOptions CreateItemEntityMessagePackOptions()
    {
        var resolver = CompositeResolver.Create(
            new IMessagePackFormatter[] { new ItemEntityStorageEnvelopeFormatter(), new ItemEntityDtoFormatter() },
            new IFormatterResolver[] { StandardResolver.Instance });

        return MessagePackSerializerOptions.Standard.WithResolver(resolver);
    }

    private static async Task AwaitUniTask(UniTask task)
    {
        await task;
    }

    private static string CreateTempDataPath()
    {
        var path = Path.Combine(Path.GetTempPath(), "LiljaRepositoryTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
