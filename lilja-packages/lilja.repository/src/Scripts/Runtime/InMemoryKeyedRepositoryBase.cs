#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Lilja.Repository.Internal;

namespace Lilja.Repository
{
public abstract class InMemoryKeyedRepositoryBase<TEntity, TKey> : IRepositoryParticipant
    where TEntity : class
    where TKey : notnull
{
    private Dictionary<TKey, TEntity> _committedState = new Dictionary<TKey, TEntity>();

    public UniTask InitializeAsync(CancellationToken ct = default)
    {
        return UniTask.CompletedTask;
    }

    public TEntity? Read(IReadOnlyTx tx, TKey key)
    {
        if (tx is null)
        {
            throw new ArgumentNullException(nameof(tx));
        }

        if (TryGetOverlay(tx, out var overlay) && overlay.TryGetValue(key, out var stagedEntity))
        {
            return stagedEntity;
        }

        return _committedState.TryGetValue(key, out var entity) ? entity : null;
    }

    public void Create(IReadWriteTx tx, TEntity entity)
    {
        if (entity is null)
        {
            throw new ArgumentNullException(nameof(entity));
        }

        var overlay = GetWriteOverlay(tx);
        var key = GetKey(entity);
        if (overlay.ContainsKey(key))
        {
            throw new InvalidOperationException($"Create failed for {GetType().Name}. Entity with key '{key}' already exists.");
        }

        overlay.Upsert(key, entity);
    }

    public void Update(IReadWriteTx tx, TEntity entity)
    {
        if (entity is null)
        {
            throw new ArgumentNullException(nameof(entity));
        }

        var overlay = GetWriteOverlay(tx);
        var key = GetKey(entity);
        if (!overlay.ContainsKey(key))
        {
            throw new InvalidOperationException($"Update failed for {GetType().Name}. Entity with key '{key}' does not exist.");
        }

        overlay.Upsert(key, entity);
    }

    public void Delete(IReadWriteTx tx, TKey key)
    {
        var overlay = GetWriteOverlay(tx);
        if (!overlay.ContainsKey(key))
        {
            throw new InvalidOperationException($"Delete failed for {GetType().Name}. Entity with key '{key}' does not exist.");
        }

        overlay.Delete(key);
    }

    public IReadOnlyList<TEntity> All(IReadOnlyTx tx)
    {
        if (tx is null)
        {
            throw new ArgumentNullException(nameof(tx));
        }

        if (TryGetOverlay(tx, out var overlay))
        {
            return new List<TEntity>(overlay.Materialize().Values);
        }

        return new List<TEntity>(_committedState.Values);
    }

    protected abstract TKey GetKey(TEntity entity);

    protected virtual UniTask PersistStateAsync(Dictionary<TKey, TEntity> state, CancellationToken ct)
    {
        return UniTask.CompletedTask;
    }

    UniTask IRepositoryParticipant.PrepareCommitAsync(object transactionState, CancellationToken ct)
    {
        var state = (KeyedTransactionState)transactionState;
        state.PreparedState = state.Overlay.Materialize();
        return PersistStateAsync(state.PreparedState, ct);
    }

    void IRepositoryParticipant.ApplyCommit(object transactionState)
    {
        var state = (KeyedTransactionState)transactionState;
        _committedState = state.PreparedState ?? state.Overlay.Materialize();
    }

    private RepositoryOverlayState<TKey, TEntity> GetWriteOverlay(IReadWriteTx tx)
    {
        if (tx is null)
        {
            throw new ArgumentNullException(nameof(tx));
        }

        if (tx is not RepositoryTx repositoryTx || !repositoryTx.IsReadWrite)
        {
            throw new InvalidOperationException("Writes require a transaction created by TxManager.");
        }

        return repositoryTx.GetOrCreateParticipantState(this, () => new KeyedTransactionState(new RepositoryOverlayState<TKey, TEntity>(_committedState))).Overlay;
    }

    private bool TryGetOverlay(IReadOnlyTx tx, out RepositoryOverlayState<TKey, TEntity> overlay)
    {
        overlay = default!;

        if (tx is RepositoryTx repositoryTx &&
            repositoryTx.IsReadWrite &&
            repositoryTx.TryGetParticipantState(this, out var transactionState))
        {
            overlay = ((KeyedTransactionState)transactionState).Overlay;
            return true;
        }

        return false;
    }

    private sealed class KeyedTransactionState
    {
        public KeyedTransactionState(RepositoryOverlayState<TKey, TEntity> overlay)
        {
            Overlay = overlay;
        }

        public RepositoryOverlayState<TKey, TEntity> Overlay { get; }

        public Dictionary<TKey, TEntity>? PreparedState { get; set; }
    }
}
}
