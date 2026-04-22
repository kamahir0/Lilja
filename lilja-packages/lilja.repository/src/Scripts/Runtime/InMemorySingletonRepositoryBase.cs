#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Lilja.Repository.Internal;

namespace Lilja.Repository
{
public abstract class InMemorySingletonRepositoryBase<TEntity> : IRepositoryParticipant
    where TEntity : class
{
    private TEntity? _committedValue;

    public UniTask InitializeAsync(CancellationToken ct = default)
    {
        return UniTask.CompletedTask;
    }

    public TEntity? Read(IReadOnlyTx tx)
    {
        if (tx is null)
        {
            throw new ArgumentNullException(nameof(tx));
        }

        if (TryGetWriteState(tx, out var writeState))
        {
            return writeState.HasValue ? writeState.Value : null;
        }

        return _committedValue;
    }

    public void Create(IReadWriteTx tx, TEntity entity)
    {
        if (entity is null)
        {
            throw new ArgumentNullException(nameof(entity));
        }

        var state = GetWriteState(tx);
        if (state.HasValue)
        {
            throw new InvalidOperationException($"Create failed for {GetType().Name}. A value already exists.");
        }

        state.Value = entity;
        state.HasValue = true;
    }

    public void Update(IReadWriteTx tx, TEntity entity)
    {
        if (entity is null)
        {
            throw new ArgumentNullException(nameof(entity));
        }

        var state = GetWriteState(tx);
        if (!state.HasValue)
        {
            throw new InvalidOperationException($"Update failed for {GetType().Name}. A value does not exist.");
        }

        state.Value = entity;
        state.HasValue = true;
    }

    public void Delete(IReadWriteTx tx)
    {
        var state = GetWriteState(tx);
        if (!state.HasValue)
        {
            throw new InvalidOperationException($"Delete failed for {GetType().Name}. A value does not exist.");
        }

        state.Value = null;
        state.HasValue = false;
    }

    protected virtual UniTask PersistStateAsync(TEntity? state, CancellationToken ct)
    {
        return UniTask.CompletedTask;
    }

    UniTask IRepositoryParticipant.PrepareCommitAsync(object transactionState, CancellationToken ct)
    {
        var state = (SingletonTransactionState)transactionState;
        state.PreparedValue = state.WriteState.HasValue ? state.WriteState.Value : null;
        return PersistStateAsync(state.PreparedValue, ct);
    }

    void IRepositoryParticipant.ApplyCommit(object transactionState)
    {
        var state = (SingletonTransactionState)transactionState;
        _committedValue = state.PreparedValue;
    }

    private RepositoryWriteState<TEntity> GetWriteState(IReadWriteTx tx)
    {
        if (tx is null)
        {
            throw new ArgumentNullException(nameof(tx));
        }

        if (tx is not RepositoryTx repositoryTx || !repositoryTx.IsReadWrite)
        {
            throw new InvalidOperationException("Writes require a transaction created by TxManager.");
        }

        return repositoryTx
            .GetOrCreateParticipantState(this, () => new SingletonTransactionState(new RepositoryWriteState<TEntity>(_committedValue, _committedValue is not null)))
            .WriteState;
    }

    private bool TryGetWriteState(IReadOnlyTx tx, out RepositoryWriteState<TEntity> writeState)
    {
        writeState = default!;

        if (tx is RepositoryTx repositoryTx &&
            repositoryTx.IsReadWrite &&
            repositoryTx.TryGetParticipantState(this, out var transactionState))
        {
            writeState = ((SingletonTransactionState)transactionState).WriteState;
            return true;
        }

        return false;
    }

    private sealed class SingletonTransactionState
    {
        public SingletonTransactionState(RepositoryWriteState<TEntity> writeState)
        {
            WriteState = writeState;
        }

        public RepositoryWriteState<TEntity> WriteState { get; }

        public TEntity? PreparedValue { get; set; }
    }
}
}
