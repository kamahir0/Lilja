using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Lilja.Repository;
using InventorySample = Lilja.Repository.Analyzer.Tests.Samples.Inventory.SharedNameEntity;
using InventoryJsonRepository = Lilja.Repository.Analyzer.Tests.Samples.Inventory.Repositories.JsonSharedNameEntityRepository;
using Lilja.Repository.Analyzer.Tests.Samples;
using Lilja.Repository.Analyzer.Tests.Samples.Repositories;
using Lilja.Repository.Diagnostics;
using Lilja.Repository.Editor;
using Lilja.Repository.Generated.Dtos.Lilja.Repository.Analyzer.Tests.Samples;
using Lilja.Repository.Generated.Formatters.Lilja.Repository.Analyzer.Tests.Samples;
using Lilja.Repository.Generated.Storage.Lilja.Repository.Analyzer.Tests.Samples;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using ProfileSample = Lilja.Repository.Analyzer.Tests.Samples.Profile.SharedNameEntity;
using ProfileJsonRepository = Lilja.Repository.Analyzer.Tests.Samples.Profile.Repositories.JsonSharedNameEntityRepository;
using UnityEngine;
using Xunit;

namespace Lilja.Repository.Analyzer.Test;

public sealed class RuntimeRepositoryTests
{
    private const string ItemEntityStorageIdentifier = "Lilja.Repository.Analyzer.Tests.Samples.ItemEntity";
    private const string SettingsEntityStorageIdentifier = "Lilja.Repository.Analyzer.Tests.Samples.SettingsEntity";
    private const string InventorySharedNameStorageIdentifier =
        "Lilja.Repository.Analyzer.Tests.Samples.Inventory.SharedNameEntity";
    private const string ProfileSharedNameStorageIdentifier =
        "Lilja.Repository.Analyzer.Tests.Samples.Profile.SharedNameEntity";

    public RuntimeRepositoryTests()
    {
        Debug.ResetTestState();
        RuntimeInstanceMonitor.ResetForTests();
        Application.isPlaying = true;
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
    public async Task InMemoryRepository_StrictCrudRejectsInvalidLifecycleOperations()
    {
        var txManager = new TxManager();
        var repository = new InMemoryItemEntityRepository();

        await txManager.BeginRWTransactionAsync(tx =>
        {
            repository.Create(tx, CreateItem(1, "before"));
        });

        var createException = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await txManager.BeginRWTransactionAsync(tx =>
            {
                repository.Create(tx, CreateItem(1, "duplicate"));
            });
        });
        Assert.Contains("Create", createException.Message, StringComparison.Ordinal);
        Assert.Contains("1", createException.Message, StringComparison.Ordinal);

        var updateException = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await txManager.BeginRWTransactionAsync(tx =>
            {
                repository.Update(tx, CreateItem(2, "missing"));
            });
        });
        Assert.Contains("Update", updateException.Message, StringComparison.Ordinal);
        Assert.Contains("2", updateException.Message, StringComparison.Ordinal);

        var deleteException = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await txManager.BeginRWTransactionAsync(tx =>
            {
                repository.Delete(tx, 2);
            });
        });
        Assert.Contains("Delete", deleteException.Message, StringComparison.Ordinal);
        Assert.Contains("2", deleteException.Message, StringComparison.Ordinal);

        txManager.BeginROTransaction(ro =>
        {
            var item = repository.Read(ro, 1);
            Assert.NotNull(item);
            Assert.Equal("before", item!.Name);
            Assert.Null(repository.Read(ro, 2));
        });
    }

    [Fact]
    public async Task InMemoryRepository_CreateThenUpdateWithinSameTransactionSucceeds()
    {
        var txManager = new TxManager();
        var repository = new InMemoryItemEntityRepository();

        await txManager.BeginRWTransactionAsync(tx =>
        {
            repository.Create(tx, CreateItem(1, "alpha"));
            repository.Update(tx, CreateItem(1, "beta"));

            Assert.Equal("beta", repository.Read(tx, 1)!.Name);
        });

        txManager.BeginROTransaction(ro =>
        {
            Assert.Equal("beta", repository.Read(ro, 1)!.Name);
        });
    }

    [Fact]
    public async Task InMemoryRepository_DeleteThenCreateWithinSameTransactionSucceeds()
    {
        var txManager = new TxManager();
        var repository = new InMemoryItemEntityRepository();

        await txManager.BeginRWTransactionAsync(tx =>
        {
            repository.Create(tx, CreateItem(1, "before"));
        });

        await txManager.BeginRWTransactionAsync(tx =>
        {
            repository.Delete(tx, 1);
            repository.Create(tx, CreateItem(1, "after"));

            Assert.Equal("after", repository.Read(tx, 1)!.Name);
        });

        txManager.BeginROTransaction(ro =>
        {
            Assert.Equal("after", repository.Read(ro, 1)!.Name);
        });
    }

    [Fact]
    public async Task InMemoryRepository_DeleteThenUpdateWithinSameTransactionThrowsAndDeleteStillCommits()
    {
        var txManager = new TxManager();
        var repository = new InMemoryItemEntityRepository();

        await txManager.BeginRWTransactionAsync(tx =>
        {
            repository.Create(tx, CreateItem(1, "before"));
        });

        await txManager.BeginRWTransactionAsync(tx =>
        {
            repository.Delete(tx, 1);

            var exception = Assert.Throws<InvalidOperationException>(() =>
            {
                repository.Update(tx, CreateItem(1, "after"));
            });
            Assert.Contains("Update", exception.Message, StringComparison.Ordinal);
            Assert.Contains("1", exception.Message, StringComparison.Ordinal);
        });

        txManager.BeginROTransaction(ro =>
        {
            Assert.Null(repository.Read(ro, 1));
        });
    }

    [Fact]
    public async Task InMemoryRepository_CreateThenCreateWithinSameTransactionThrowsAndFirstCreateCommits()
    {
        var txManager = new TxManager();
        var repository = new InMemoryItemEntityRepository();

        await txManager.BeginRWTransactionAsync(tx =>
        {
            repository.Create(tx, CreateItem(1, "first"));

            var exception = Assert.Throws<InvalidOperationException>(() =>
            {
                repository.Create(tx, CreateItem(1, "duplicate"));
            });
            Assert.Contains("Create", exception.Message, StringComparison.Ordinal);
            Assert.Contains("1", exception.Message, StringComparison.Ordinal);

            repository.Create(tx, CreateItem(2, "second"));
        });

        txManager.BeginROTransaction(ro =>
        {
            Assert.Equal("first", repository.Read(ro, 1)!.Name);
            Assert.Equal("second", repository.Read(ro, 2)!.Name);
        });
    }

    [Fact]
    public async Task InMemoryRepository_DeleteThenDeleteWithinSameTransactionThrowsAndFirstDeleteCommits()
    {
        var txManager = new TxManager();
        var repository = new InMemoryItemEntityRepository();

        await txManager.BeginRWTransactionAsync(tx =>
        {
            repository.Create(tx, CreateItem(1, "before"));
        });

        await txManager.BeginRWTransactionAsync(tx =>
        {
            repository.Delete(tx, 1);

            var exception = Assert.Throws<InvalidOperationException>(() =>
            {
                repository.Delete(tx, 1);
            });
            Assert.Contains("Delete", exception.Message, StringComparison.Ordinal);
            Assert.Contains("1", exception.Message, StringComparison.Ordinal);
        });

        txManager.BeginROTransaction(ro =>
        {
            Assert.Null(repository.Read(ro, 1));
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
            GetJsonPath(dataPath, ItemEntityStorageIdentifier),
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
        var filePath = GetJsonPath(dataPath, ItemEntityStorageIdentifier);
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
    public async Task JsonKeyedRepository_StrictCrudRejectsInvalidLifecycleOperations()
    {
        var dataPath = CreateTempDataPath();
        Application.persistentDataPath = dataPath;

        var repository = new JsonItemEntityRepository();
        var txManager = new TxManager();
        await repository.InitializeAsync();

        await txManager.BeginRWTransactionAsync(tx =>
        {
            repository.Create(tx, CreateItem(1, "before"));
        });

        var createException = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await txManager.BeginRWTransactionAsync(tx =>
            {
                repository.Create(tx, CreateItem(1, "duplicate"));
            });
        });
        Assert.Contains("Create", createException.Message, StringComparison.Ordinal);
        Assert.Contains("1", createException.Message, StringComparison.Ordinal);

        var updateException = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await txManager.BeginRWTransactionAsync(tx =>
            {
                repository.Update(tx, CreateItem(2, "missing"));
            });
        });
        Assert.Contains("Update", updateException.Message, StringComparison.Ordinal);
        Assert.Contains("2", updateException.Message, StringComparison.Ordinal);

        var deleteException = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await txManager.BeginRWTransactionAsync(tx =>
            {
                repository.Delete(tx, 2);
            });
        });
        Assert.Contains("Delete", deleteException.Message, StringComparison.Ordinal);
        Assert.Contains("2", deleteException.Message, StringComparison.Ordinal);

        txManager.BeginROTransaction(tx =>
        {
            Assert.Equal("before", repository.Read(tx, 1)!.Name);
            Assert.Null(repository.Read(tx, 2));
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
    public async Task ReadWriteTransaction_CancellationDuringPublishStillCommits()
    {
        var txManager = new TxManager();
        var repository = new InMemoryItemEntityRepository();
        await txManager.BeginRWTransactionAsync(tx =>
        {
            repository.Create(tx, CreateItem(1, "before"));
        });

        var releaseReader = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var readerEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var readerTask = txManager.BeginROTransactionAsync(async ro =>
        {
            Assert.Equal("before", repository.Read(ro, 1)!.Name);
            readerEntered.TrySetResult(true);
            await releaseReader.Task;
        });

        await readerEntered.Task;

        using var cancellationTokenSource = new System.Threading.CancellationTokenSource();
        var writerTask = AwaitUniTask(txManager.BeginRWTransactionAsync(tx =>
        {
            repository.Update(tx, CreateItem(1, "after"));
        }, cancellationTokenSource.Token));

        await Task.Delay(50);
        cancellationTokenSource.Cancel();

        await Task.Delay(50);
        Assert.False(writerTask.IsCompleted);

        releaseReader.TrySetResult(true);
        await Task.WhenAll(AwaitUniTask(readerTask), writerTask);

        txManager.BeginROTransaction(ro =>
        {
            Assert.Equal("after", repository.Read(ro, 1)!.Name);
        });

        await txManager.BeginROTransactionAsync(ro =>
        {
            Assert.Equal("after", repository.Read(ro, 1)!.Name);
            return UniTask.CompletedTask;
        });
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

        var filePath = GetJsonPath(dataPath, ItemEntityStorageIdentifier);
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

        var bytes = File.ReadAllBytes(GetMessagePackPath(dataPath, ItemEntityStorageIdentifier));
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

        var filePath = GetJsonPath(dataPath, SettingsEntityStorageIdentifier);
        Assert.True(File.Exists(filePath));

        var envelope = JsonUtility.FromJson<SettingsEntityStorageEnvelope>(File.ReadAllText(filePath));
        Assert.NotNull(envelope);
        Assert.NotNull(envelope);
        Assert.False(envelope.HasValue);
        Assert.Null(envelope.Item);
    }

    [Fact]
    public async Task InMemorySingletonRepository_StrictCrudRejectsInvalidLifecycleOperations()
    {
        var txManager = new TxManager();
        var repository = new InMemorySettingsEntityRepository();

        var updateException = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await txManager.BeginRWTransactionAsync(tx =>
            {
                repository.Update(tx, new SettingsEntity(10));
            });
        });
        Assert.Contains("Update", updateException.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(InMemorySettingsEntityRepository), updateException.Message, StringComparison.Ordinal);

        var deleteException = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await txManager.BeginRWTransactionAsync(tx =>
            {
                repository.Delete(tx);
            });
        });
        Assert.Contains("Delete", deleteException.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(InMemorySettingsEntityRepository), deleteException.Message, StringComparison.Ordinal);

        await txManager.BeginRWTransactionAsync(tx =>
        {
            repository.Create(tx, new SettingsEntity(10));
        });

        var createException = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await txManager.BeginRWTransactionAsync(tx =>
            {
                repository.Create(tx, new SettingsEntity(20));
            });
        });
        Assert.Contains("Create", createException.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(InMemorySettingsEntityRepository), createException.Message, StringComparison.Ordinal);

        txManager.BeginROTransaction(tx =>
        {
            Assert.Equal(10, repository.Read(tx)!.Volume);
        });
    }

    [Fact]
    public async Task JsonSingletonRepository_StrictCrudRejectsInvalidLifecycleOperations()
    {
        var dataPath = CreateTempDataPath();
        Application.persistentDataPath = dataPath;

        var repository = new JsonSettingsEntityRepository();
        var txManager = new TxManager();
        await repository.InitializeAsync();

        var updateException = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await txManager.BeginRWTransactionAsync(tx =>
            {
                repository.Update(tx, new SettingsEntity(10));
            });
        });
        Assert.Contains("Update", updateException.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(JsonSettingsEntityRepository), updateException.Message, StringComparison.Ordinal);

        var deleteException = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await txManager.BeginRWTransactionAsync(tx =>
            {
                repository.Delete(tx);
            });
        });
        Assert.Contains("Delete", deleteException.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(JsonSettingsEntityRepository), deleteException.Message, StringComparison.Ordinal);

        await txManager.BeginRWTransactionAsync(tx =>
        {
            repository.Create(tx, new SettingsEntity(10));
        });

        var createException = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await txManager.BeginRWTransactionAsync(tx =>
            {
                repository.Create(tx, new SettingsEntity(20));
            });
        });
        Assert.Contains("Create", createException.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(JsonSettingsEntityRepository), createException.Message, StringComparison.Ordinal);

        txManager.BeginROTransaction(tx =>
        {
            Assert.Equal(10, repository.Read(tx)!.Volume);
        });
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

    [Fact]
    public void RuntimeSurface_ExposesGeneratedRepositoryFacingTypesOnly()
    {
        Assert.True(typeof(InMemoryKeyedRepositoryBase<ItemEntity, int>).IsPublic);
        Assert.True(typeof(InMemorySingletonRepositoryBase<SettingsEntity>).IsPublic);
        Assert.True(typeof(PersistedKeyedRepositoryBase<ItemEntity, int, ItemEntityDto>).IsPublic);
        Assert.True(typeof(PersistedSingletonRepositoryBase<SettingsEntity, SettingsEntityDto>).IsPublic);

        Assert.False(typeof(RepositoryTx).IsPublic);
        Assert.False(typeof(RepositoryWriteState<ItemEntity>).IsPublic);
        Assert.False(typeof(RepositoryOverlayState<int, ItemEntityDto>).IsPublic);
        Assert.False(typeof(RuntimeInstanceMonitor).IsPublic);

        var publicLowLevelMethods = typeof(RepositoryTx).GetMethods(BindingFlags.Public | BindingFlags.Static);
        Assert.DoesNotContain(publicLowLevelMethods, method => method.Name == "UpsertKeyedValue");
        Assert.DoesNotContain(publicLowLevelMethods, method => method.Name == "RemoveKeyedValue");
    }

    [Fact]
    public void RuntimeSurface_PublicApiSnapshotMatchesExpected()
    {
        var expected = new[]
        {
            "type Lilja.Repository.AtomicFileWriter",
            "method System.Void Lilja.Repository.AtomicFileWriter.DeleteIfExists(System.String filePath)",
            "method System.Void Lilja.Repository.AtomicFileWriter.WriteAllBytes(System.String filePath, System.Byte[] bytes)",
            "method System.Void Lilja.Repository.AtomicFileWriter.WriteAllText(System.String filePath, System.String content)",
            "type Lilja.Repository.Diagnostics.RepositoryTracker",
            "method System.Collections.Generic.IEnumerable<System.Object> Lilja.Repository.Diagnostics.RepositoryTracker.GetAll(Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType type)",
            "method System.Void Lilja.Repository.Diagnostics.RepositoryTracker.Track(System.Object repository, Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType type)",
            "type Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType",
            "field Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType.InMemory",
            "field Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType.Json",
            "field Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType.MessagePack",
            "type Lilja.Repository.EntityAttribute",
            "ctor Lilja.Repository.EntityAttribute()",
            "type Lilja.Repository.FromPrimitiveAttribute",
            "ctor Lilja.Repository.FromPrimitiveAttribute()",
            "type Lilja.Repository.IReadOnlyTx",
            "type Lilja.Repository.IReadWriteTx",
            "type Lilja.Repository.InMemoryKeyedRepositoryBase<TEntity, TKey>",
            "method System.Collections.Generic.IReadOnlyList<TEntity> Lilja.Repository.InMemoryKeyedRepositoryBase<TEntity, TKey>.All(Lilja.Repository.IReadOnlyTx tx)",
            "method System.Void Lilja.Repository.InMemoryKeyedRepositoryBase<TEntity, TKey>.Create(Lilja.Repository.IReadWriteTx tx, TEntity entity)",
            "method System.Void Lilja.Repository.InMemoryKeyedRepositoryBase<TEntity, TKey>.Delete(Lilja.Repository.IReadWriteTx tx, TKey key)",
            "method Cysharp.Threading.Tasks.UniTask Lilja.Repository.InMemoryKeyedRepositoryBase<TEntity, TKey>.InitializeAsync(System.Threading.CancellationToken ct)",
            "method TEntity Lilja.Repository.InMemoryKeyedRepositoryBase<TEntity, TKey>.Read(Lilja.Repository.IReadOnlyTx tx, TKey key)",
            "method System.Void Lilja.Repository.InMemoryKeyedRepositoryBase<TEntity, TKey>.Update(Lilja.Repository.IReadWriteTx tx, TEntity entity)",
            "type Lilja.Repository.InMemorySingletonRepositoryBase<TEntity>",
            "method System.Void Lilja.Repository.InMemorySingletonRepositoryBase<TEntity>.Create(Lilja.Repository.IReadWriteTx tx, TEntity entity)",
            "method System.Void Lilja.Repository.InMemorySingletonRepositoryBase<TEntity>.Delete(Lilja.Repository.IReadWriteTx tx)",
            "method Cysharp.Threading.Tasks.UniTask Lilja.Repository.InMemorySingletonRepositoryBase<TEntity>.InitializeAsync(System.Threading.CancellationToken ct)",
            "method TEntity Lilja.Repository.InMemorySingletonRepositoryBase<TEntity>.Read(Lilja.Repository.IReadOnlyTx tx)",
            "method System.Void Lilja.Repository.InMemorySingletonRepositoryBase<TEntity>.Update(Lilja.Repository.IReadWriteTx tx, TEntity entity)",
            "type Lilja.Repository.KeyAttribute",
            "ctor Lilja.Repository.KeyAttribute()",
            "type Lilja.Repository.PersistAttribute",
            "ctor Lilja.Repository.PersistAttribute(System.Int32 index)",
            "property System.Int32 Lilja.Repository.PersistAttribute.Index { get; }",
            "type Lilja.Repository.PersistedKeyedRepositoryBase<TEntity, TKey, TDto>",
            "method System.Collections.Generic.IReadOnlyList<TEntity> Lilja.Repository.PersistedKeyedRepositoryBase<TEntity, TKey, TDto>.All(Lilja.Repository.IReadOnlyTx tx)",
            "method System.Void Lilja.Repository.PersistedKeyedRepositoryBase<TEntity, TKey, TDto>.Create(Lilja.Repository.IReadWriteTx tx, TEntity entity)",
            "method System.Void Lilja.Repository.PersistedKeyedRepositoryBase<TEntity, TKey, TDto>.Delete(Lilja.Repository.IReadWriteTx tx, TKey key)",
            "method Cysharp.Threading.Tasks.UniTask Lilja.Repository.PersistedKeyedRepositoryBase<TEntity, TKey, TDto>.InitializeAsync(System.Threading.CancellationToken ct)",
            "method TEntity Lilja.Repository.PersistedKeyedRepositoryBase<TEntity, TKey, TDto>.Read(Lilja.Repository.IReadOnlyTx tx, TKey key)",
            "method System.Void Lilja.Repository.PersistedKeyedRepositoryBase<TEntity, TKey, TDto>.Update(Lilja.Repository.IReadWriteTx tx, TEntity entity)",
            "type Lilja.Repository.PersistedSingletonRepositoryBase<TEntity, TDto>",
            "method System.Void Lilja.Repository.PersistedSingletonRepositoryBase<TEntity, TDto>.Create(Lilja.Repository.IReadWriteTx tx, TEntity entity)",
            "method System.Void Lilja.Repository.PersistedSingletonRepositoryBase<TEntity, TDto>.Delete(Lilja.Repository.IReadWriteTx tx)",
            "method Cysharp.Threading.Tasks.UniTask Lilja.Repository.PersistedSingletonRepositoryBase<TEntity, TDto>.InitializeAsync(System.Threading.CancellationToken ct)",
            "method TEntity Lilja.Repository.PersistedSingletonRepositoryBase<TEntity, TDto>.Read(Lilja.Repository.IReadOnlyTx tx)",
            "method System.Void Lilja.Repository.PersistedSingletonRepositoryBase<TEntity, TDto>.Update(Lilja.Repository.IReadWriteTx tx, TEntity entity)",
            "type Lilja.Repository.ToPrimitiveAttribute",
            "ctor Lilja.Repository.ToPrimitiveAttribute()",
            "type Lilja.Repository.TxManager",
            "ctor Lilja.Repository.TxManager()",
            "method System.Void Lilja.Repository.TxManager.BeginROTransaction(System.Action<Lilja.Repository.IReadOnlyTx> action)",
            "method Cysharp.Threading.Tasks.UniTask Lilja.Repository.TxManager.BeginROTransactionAsync(System.Func<Lilja.Repository.IReadOnlyTx, Cysharp.Threading.Tasks.UniTask> action)",
            "method Cysharp.Threading.Tasks.UniTask Lilja.Repository.TxManager.BeginRWTransactionAsync(System.Action<Lilja.Repository.IReadWriteTx> action, System.Threading.CancellationToken ct)",
            "method Cysharp.Threading.Tasks.UniTask Lilja.Repository.TxManager.BeginRWTransactionAsync(System.Func<Lilja.Repository.IReadWriteTx, Cysharp.Threading.Tasks.UniTask> action, System.Threading.CancellationToken ct)",
        };

        Assert.Equal(expected, DescribePublicRuntimeSurface().ToArray());
    }

    [Fact]
    public async Task NamespacedJsonRepositories_UseDistinctStorageFiles()
    {
        var dataPath = CreateTempDataPath();
        Application.persistentDataPath = dataPath;

        var inventoryRepository = new InventoryJsonRepository();
        var profileRepository = new ProfileJsonRepository();
        var txManager = new TxManager();

        await inventoryRepository.InitializeAsync();
        await profileRepository.InitializeAsync();

        await txManager.BeginRWTransactionAsync(tx =>
        {
            inventoryRepository.Create(tx, new InventorySample(1, "inventory"));
            profileRepository.Create(tx, new ProfileSample(2, "profile"));
        });

        Assert.True(File.Exists(GetJsonPath(dataPath, InventorySharedNameStorageIdentifier)));
        Assert.True(File.Exists(GetJsonPath(dataPath, ProfileSharedNameStorageIdentifier)));

        txManager.BeginROTransaction(tx =>
        {
            Assert.Equal("inventory", inventoryRepository.Read(tx, 1)!.Name);
            Assert.Equal("profile", profileRepository.Read(tx, 2)!.Name);
        });
    }

    [Fact]
    public async Task RepositoryTreeDataLoader_UsesStorageIdentifierToResolveSameNamedPersistedTypes()
    {
        var dataPath = CreateTempDataPath();
        Application.persistentDataPath = dataPath;

        var inventoryRepository = new InventoryJsonRepository();
        var profileRepository = new ProfileJsonRepository();
        var txManager = new TxManager();

        await inventoryRepository.InitializeAsync();
        await profileRepository.InitializeAsync();

        await txManager.BeginRWTransactionAsync(tx =>
        {
            inventoryRepository.Create(tx, new InventorySample(1, "inventory"));
            profileRepository.Create(tx, new ProfileSample(2, "profile"));
        });

        var wasPlaying = Application.isPlaying;
        Application.isPlaying = false;
        try
        {
            var id = 0;
            var nodes = RepositoryTreeDataLoader.Load(RepositoryTracker.RepositoryType.Json, ref id)
                .Cast<RepositoryTrackerViewItem>()
                .ToList();

            Assert.Equal(2, nodes.Count);

            var inventoryNode = nodes.Single(node => node.RepositoryName == Path.GetFileName(GetJsonPath(dataPath, InventorySharedNameStorageIdentifier)));
            var profileNode = nodes.Single(node => node.RepositoryName == Path.GetFileName(GetJsonPath(dataPath, ProfileSharedNameStorageIdentifier)));

            Assert.NotNull(inventoryNode.children);
            Assert.NotNull(profileNode.children);

            var inventoryItem = Assert.Single(inventoryNode.children!.Cast<RepositoryTrackerViewItem>());
            var profileItem = Assert.Single(profileNode.children!.Cast<RepositoryTrackerViewItem>());
            Assert.NotNull(inventoryItem.FullValue);
            Assert.NotNull(profileItem.FullValue);

            Assert.Equal("1", inventoryItem.Key);
            Assert.Equal("2", profileItem.Key);
            Assert.Equal(
                "Lilja.Repository.Generated.Dtos.Lilja.Repository.Analyzer.Tests.Samples.Inventory.SharedNameEntityDto",
                inventoryItem.FullValue!.GetType().FullName);
            Assert.Equal(
                "Lilja.Repository.Generated.Dtos.Lilja.Repository.Analyzer.Tests.Samples.Profile.SharedNameEntityDto",
                profileItem.FullValue!.GetType().FullName);
        }
        finally
        {
            Application.isPlaying = wasPlaying;
        }
    }

    [Fact]
    public void RepositoryTreeDataLoader_FallsBackToUnknownForUnresolvedPersistedFiles()
    {
        var dataPath = CreateTempDataPath();
        Application.persistentDataPath = dataPath;
        File.WriteAllText(Path.Combine(dataPath, "Unknown.Namespace.Entity.json"), "{\"Items\":[]}");

        var wasPlaying = Application.isPlaying;
        Application.isPlaying = false;
        try
        {
            var id = 0;
            var node = Assert.Single(RepositoryTreeDataLoader.Load(RepositoryTracker.RepositoryType.Json, ref id).Cast<RepositoryTrackerViewItem>());

            Assert.Equal("Unknown", node.Type);
            Assert.Contains("Items", node.ValuePreview, StringComparison.Ordinal);
        }
        finally
        {
            Application.isPlaying = wasPlaying;
        }
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

    private static string GetJsonPath(string dataPath, string storageIdentifier)
    {
        return Path.Combine(dataPath, storageIdentifier + ".json");
    }

    private static string GetMessagePackPath(string dataPath, string storageIdentifier)
    {
        return Path.Combine(dataPath, storageIdentifier + ".msgpack");
    }

    private static string CreateTempDataPath()
    {
        var path = Path.Combine(Path.GetTempPath(), "LiljaRepositoryTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static IEnumerable<string> DescribePublicRuntimeSurface()
    {
        var assembly = typeof(TxManager).Assembly;
        var types = assembly.GetTypes()
            .Where(IsTrackedRuntimeSurfaceType)
            .OrderBy(type => FormatTypeName(type), StringComparer.Ordinal);

        foreach (var type in types)
        {
            yield return "type " + FormatTypeName(type);

            foreach (var constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                         .OrderBy(static constructor => constructor.ToString(), StringComparer.Ordinal))
            {
                yield return $"ctor {FormatTypeName(type)}({FormatParameters(constructor.GetParameters())})";
            }

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                         .OrderBy(static property => property.Name, StringComparer.Ordinal))
            {
                var accessors = new List<string>(2);
                if (property.GetMethod?.IsPublic == true)
                {
                    accessors.Add("get;");
                }

                if (property.SetMethod?.IsPublic == true)
                {
                    accessors.Add("set;");
                }

                yield return $"property {FormatTypeName(property.PropertyType)} {FormatTypeName(type)}.{property.Name} {{ {string.Join(" ", accessors)} }}";
            }

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                         .Where(static method => !method.IsSpecialName)
                         .OrderBy(static method => method.Name, StringComparer.Ordinal)
                         .ThenBy(static method => method.ToString(), StringComparer.Ordinal))
            {
                yield return $"method {FormatTypeName(method.ReturnType)} {FormatTypeName(type)}.{method.Name}({FormatParameters(method.GetParameters())})";
            }

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                         .Where(static field => !field.IsSpecialName)
                         .OrderBy(static field => field.Name, StringComparer.Ordinal))
            {
                yield return $"field {FormatTypeName(field.FieldType)} {FormatTypeName(type)}.{field.Name}";
            }
        }
    }

    private static bool IsTrackedRuntimeSurfaceType(Type type)
    {
        if (!type.IsPublic && !type.IsNestedPublic)
        {
            return false;
        }

        if (type.Namespace == null)
        {
            return false;
        }

        return type.Namespace == "Lilja.Repository" || type.Namespace == "Lilja.Repository.Diagnostics";
    }

    private static string FormatParameters(ParameterInfo[] parameters)
    {
        return string.Join(
            ", ",
            parameters.Select(parameter => $"{FormatTypeName(parameter.ParameterType)} {parameter.Name}"));
    }

    private static string FormatTypeName(Type type)
    {
        if (type.IsGenericParameter)
        {
            return type.Name;
        }

        if (type.IsArray)
        {
            return $"{FormatTypeName(type.GetElementType()!)}[]";
        }

        if (type.IsGenericType)
        {
            var genericType = type.IsGenericTypeDefinition ? type : type.GetGenericTypeDefinition();
            var genericTypeName = (genericType.FullName ?? genericType.Name).Replace('+', '.');
            var backtickIndex = genericTypeName.IndexOf('`');
            if (backtickIndex >= 0)
            {
                genericTypeName = genericTypeName.Substring(0, backtickIndex);
            }

            return $"{genericTypeName}<{string.Join(", ", type.GetGenericArguments().Select(FormatTypeName))}>";
        }

        return (type.FullName ?? type.Name).Replace('+', '.');
    }
}
