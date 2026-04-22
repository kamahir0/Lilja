#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Lilja.Repository.Internal;

namespace Lilja.Repository
{
/// <summary>
/// Provides transactional CRUD behavior for a keyed repository backed by persisted DTO payloads.
/// </summary>
/// <typeparam name="TEntity">The entity type managed by the repository.</typeparam>
/// <typeparam name="TKey">The key used to identify entities.</typeparam>
/// <typeparam name="TDto">The DTO type written to and read from storage.</typeparam>
public abstract class PersistedKeyedRepositoryBase<TEntity, TKey, TDto> : IRepositoryParticipant
    where TEntity : class
    where TKey : notnull
    where TDto : class
{
    private readonly SemaphoreSlim _initializationGate = new SemaphoreSlim(1, 1);
    private Dictionary<TKey, TEntity> _committedState = new Dictionary<TKey, TEntity>();
    private bool _initialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="PersistedKeyedRepositoryBase{TEntity, TKey, TDto}"/> class.
    /// </summary>
    /// <param name="filePath">The file path used for persistence.</param>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is blank.</exception>
    protected PersistedKeyedRepositoryBase(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path must not be null, empty, or whitespace.", nameof(filePath));
        }

        FilePath = filePath;
        RuntimeInstanceMonitor.TrackPersistedRepository(GetType(), filePath, this);
    }

    /// <summary>
    /// Gets the file path used by the repository backend.
    /// </summary>
    protected string FilePath { get; }

    /// <summary>
    /// Loads persisted state into memory before the repository is used.
    /// </summary>
    /// <param name="ct">A token that can cancel initialization.</param>
    /// <returns>A task that completes when the initial load has finished.</returns>
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

    /// <summary>
    /// Reads an entity visible within the supplied transaction.
    /// </summary>
    /// <param name="tx">The transaction to read through.</param>
    /// <param name="key">The entity key.</param>
    /// <returns>The committed or staged entity, or <see langword="null"/> when no entity exists.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The repository has not been initialized.</exception>
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

    /// <summary>
    /// Creates an entity within a read-write transaction.
    /// </summary>
    /// <param name="tx">The transaction that stages the change.</param>
    /// <param name="entity">The entity to create.</param>
    /// <exception cref="ArgumentNullException"><paramref name="entity"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The repository has not been initialized, an entity with the same key already exists, or the transaction is invalid.</exception>
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

    /// <summary>
    /// Updates an entity within a read-write transaction.
    /// </summary>
    /// <param name="tx">The transaction that stages the change.</param>
    /// <param name="entity">The replacement entity.</param>
    /// <exception cref="ArgumentNullException"><paramref name="entity"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The repository has not been initialized, the entity does not exist, or the transaction is invalid.</exception>
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

    /// <summary>
    /// Deletes an entity within a read-write transaction.
    /// </summary>
    /// <param name="tx">The transaction that stages the change.</param>
    /// <param name="key">The key of the entity to delete.</param>
    /// <exception cref="InvalidOperationException">The repository has not been initialized, the entity does not exist, or the transaction is invalid.</exception>
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

    /// <summary>
    /// Returns a snapshot of all entities visible within the supplied transaction.
    /// </summary>
    /// <param name="tx">The transaction to read through.</param>
    /// <returns>A materialized list of entities.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The repository has not been initialized.</exception>
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

    /// <summary>
    /// Converts an entity instance to the DTO persisted by this repository.
    /// </summary>
    /// <param name="entity">The entity to convert.</param>
    /// <returns>The DTO representation.</returns>
    protected abstract TDto ToDto(TEntity entity);

    /// <summary>
    /// Rebuilds an entity instance from the persisted DTO representation.
    /// </summary>
    /// <param name="dto">The DTO to convert.</param>
    /// <returns>The reconstructed entity.</returns>
    protected abstract TEntity FromDto(TDto dto);

    /// <summary>
    /// Extracts the repository key from a persisted DTO.
    /// </summary>
    /// <param name="dto">The DTO whose key should be returned.</param>
    /// <returns>The entity key.</returns>
    protected abstract TKey GetKeyFromDto(TDto dto);

    /// <summary>
    /// Loads all persisted DTOs from storage.
    /// </summary>
    /// <param name="ct">A token that can cancel the load.</param>
    /// <returns>The stored DTOs, or <see langword="null"/> when no state exists.</returns>
    protected abstract UniTask<IReadOnlyList<TDto>?> LoadItemsAsync(CancellationToken ct);

    /// <summary>
    /// Saves the prepared DTO snapshot to storage during commit.
    /// </summary>
    /// <param name="items">The DTOs to persist.</param>
    /// <param name="ct">A token that can cancel the save.</param>
    /// <returns>A task that completes when persistence finishes.</returns>
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
