#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Lilja.Repository.Internal;

namespace Lilja.Repository
{
/// <summary>
/// Provides transactional CRUD behavior for a singleton repository stored entirely in memory.
/// </summary>
/// <typeparam name="TEntity">The entity type managed by the repository.</typeparam>
public abstract class InMemorySingletonRepositoryBase<TEntity> : IRepositoryParticipant
    where TEntity : class
{
    private TEntity? _committedValue;

    /// <summary>
    /// Initializes the repository before first use.
    /// </summary>
    /// <param name="ct">A token that can cancel initialization.</param>
    /// <returns>A completed task for the in-memory implementation.</returns>
    public UniTask InitializeAsync(CancellationToken ct = default)
    {
        return UniTask.CompletedTask;
    }

    /// <summary>
    /// Reads the current entity value visible within the supplied transaction.
    /// </summary>
    /// <param name="tx">The transaction to read through.</param>
    /// <returns>The committed or staged entity, or <see langword="null"/> when no value exists.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>.</exception>
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

    /// <summary>
    /// Creates the singleton value within a read-write transaction.
    /// </summary>
    /// <param name="tx">The transaction that stages the change.</param>
    /// <param name="entity">The entity to create.</param>
    /// <exception cref="ArgumentNullException"><paramref name="entity"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">A value already exists or the transaction is invalid.</exception>
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

    /// <summary>
    /// Replaces the singleton value within a read-write transaction.
    /// </summary>
    /// <param name="tx">The transaction that stages the change.</param>
    /// <param name="entity">The replacement entity.</param>
    /// <exception cref="ArgumentNullException"><paramref name="entity"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No value exists or the transaction is invalid.</exception>
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

    /// <summary>
    /// Deletes the singleton value within a read-write transaction.
    /// </summary>
    /// <param name="tx">The transaction that stages the change.</param>
    /// <exception cref="InvalidOperationException">No value exists or the transaction is invalid.</exception>
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

    /// <summary>
    /// Persists the prepared state before it becomes the new committed value.
    /// </summary>
    /// <param name="state">The value that is about to become visible to readers.</param>
    /// <param name="ct">A token that can cancel persistence.</param>
    /// <returns>A task that completes when persistence finishes.</returns>
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
