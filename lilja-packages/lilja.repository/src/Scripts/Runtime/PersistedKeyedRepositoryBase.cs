#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Lilja.Repository.Internal;

namespace Lilja.Repository
{
public abstract class PersistedKeyedRepositoryBase<TEntity, TKey, TDto> : IRepositoryParticipant
    where TEntity : class
    where TKey : notnull
    where TDto : class
{
    private readonly SemaphoreSlim _initializationGate = new SemaphoreSlim(1, 1);
    private Dictionary<TKey, TEntity> _committedState = new Dictionary<TKey, TEntity>();
    private bool _initialized;

    protected PersistedKeyedRepositoryBase(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path must not be null, empty, or whitespace.", nameof(filePath));
        }

        FilePath = filePath;
        RuntimeInstanceMonitor.TrackPersistedRepository(GetType(), filePath, this);
    }

    protected string FilePath { get; }

    public async UniTask InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationGate.WaitAsync(ct);
        try
        {
            if (_initialized)
            {
                return;
            }

            ct.ThrowIfCancellationRequested();
            var items = await LoadItemsAsync(ct);
            var loaded = new Dictionary<TKey, TEntity>();

            if (items is not null)
            {
                foreach (var dto in items)
                {
                    if (dto is null)
                    {
                        continue;
                    }

                    loaded[GetKeyFromDto(dto)] = FromDto(dto);
                }
            }

            _committedState = loaded;
            _initialized = true;
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    public TEntity? Read(IReadOnlyTx tx, TKey key)
    {
        if (tx is null)
        {
            throw new ArgumentNullException(nameof(tx));
        }

        EnsureInitialized();

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

        EnsureInitialized();
        var overlay = GetWriteOverlay(tx);
        var key = GetKeyFromEntity(entity);
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

        EnsureInitialized();
        var overlay = GetWriteOverlay(tx);
        var key = GetKeyFromEntity(entity);
        if (!overlay.ContainsKey(key))
        {
            throw new InvalidOperationException($"Update failed for {GetType().Name}. Entity with key '{key}' does not exist.");
        }

        overlay.Upsert(key, entity);
    }

    public void Delete(IReadWriteTx tx, TKey key)
    {
        EnsureInitialized();
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

        EnsureInitialized();

        if (TryGetOverlay(tx, out var overlay))
        {
            return new List<TEntity>(overlay.Materialize().Values);
        }

        return new List<TEntity>(_committedState.Values);
    }

    protected abstract TDto ToDto(TEntity entity);

    protected abstract TEntity FromDto(TDto dto);

    protected abstract TKey GetKeyFromDto(TDto dto);

    protected abstract UniTask<IReadOnlyList<TDto>?> LoadItemsAsync(CancellationToken ct);

    protected abstract UniTask SaveItemsAsync(IReadOnlyList<TDto> items, CancellationToken ct);

    UniTask IRepositoryParticipant.PrepareCommitAsync(object transactionState, CancellationToken ct)
    {
        var state = (KeyedTransactionState)transactionState;
        state.PreparedState = state.Overlay.Materialize();
        state.PreparedItems = ToDtoList(state.PreparedState);
        return SaveItemsAsync(state.PreparedItems, ct);
    }

    void IRepositoryParticipant.ApplyCommit(object transactionState)
    {
        var state = (KeyedTransactionState)transactionState;
        _committedState = state.PreparedState ?? state.Overlay.Materialize();
    }

    private TKey GetKeyFromEntity(TEntity entity)
    {
        return GetKeyFromDto(ToDto(entity));
    }

    private List<TDto> ToDtoList(Dictionary<TKey, TEntity> state)
    {
        var items = new List<TDto>(state.Count);
        foreach (var value in state.Values)
        {
            items.Add(ToDto(value));
        }

        return items;
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

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException($"{GetType().Name} has not been initialized. Call InitializeAsync before use.");
        }
    }

    private sealed class KeyedTransactionState
    {
        public KeyedTransactionState(RepositoryOverlayState<TKey, TEntity> overlay)
        {
            Overlay = overlay;
        }

        public RepositoryOverlayState<TKey, TEntity> Overlay { get; }

        public Dictionary<TKey, TEntity>? PreparedState { get; set; }

        public IReadOnlyList<TDto>? PreparedItems { get; set; }
    }
}
}
