#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Lilja.Repository.Internal;

namespace Lilja.Repository
{
/// <summary>
/// Provides transactional CRUD behavior for a singleton repository backed by a persisted DTO payload.
/// </summary>
/// <typeparam name="TEntity">The entity type managed by the repository.</typeparam>
/// <typeparam name="TDto">The DTO type written to and read from storage.</typeparam>
public abstract class PersistedSingletonRepositoryBase<TEntity, TDto> : IRepositoryParticipant
    where TEntity : class
    where TDto : class
{
    private readonly SemaphoreSlim _initializationGate = new SemaphoreSlim(1, 1);
    private TEntity? _committedValue;
    private bool _initialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="PersistedSingletonRepositoryBase{TEntity, TDto}"/> class.
    /// </summary>
    /// <param name="filePath">The file path used for persistence.</param>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is blank.</exception>
    protected PersistedSingletonRepositoryBase(string filePath)
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
            var value = await LoadValueAsync(ct);
            _committedValue = value is null ? null : FromDto(value);
            _initialized = true;
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    /// <summary>
    /// Reads the current entity value visible within the supplied transaction.
    /// </summary>
    /// <param name="tx">The transaction to read through.</param>
    /// <returns>The committed or staged entity, or <see langword="null"/> when no value exists.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The repository has not been initialized.</exception>
    public TEntity? Read(IReadOnlyTx tx)
    {
        if (tx is null)
        {
            throw new ArgumentNullException(nameof(tx));
        }

        EnsureInitialized();

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
    /// <exception cref="InvalidOperationException">The repository has not been initialized, a value already exists, or the transaction is invalid.</exception>
    public void Create(IReadWriteTx tx, TEntity entity)
    {
        if (entity is null)
        {
            throw new ArgumentNullException(nameof(entity));
        }

        EnsureInitialized();
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
    /// <exception cref="InvalidOperationException">The repository has not been initialized, no value exists, or the transaction is invalid.</exception>
    public void Update(IReadWriteTx tx, TEntity entity)
    {
        if (entity is null)
        {
            throw new ArgumentNullException(nameof(entity));
        }

        EnsureInitialized();
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
    /// <exception cref="InvalidOperationException">The repository has not been initialized, no value exists, or the transaction is invalid.</exception>
    public void Delete(IReadWriteTx tx)
    {
        EnsureInitialized();
        var state = GetWriteState(tx);
        if (!state.HasValue)
        {
            throw new InvalidOperationException($"Delete failed for {GetType().Name}. A value does not exist.");
        }

        state.Value = null;
        state.HasValue = false;
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
    /// Loads the persisted DTO from storage.
    /// </summary>
    /// <param name="ct">A token that can cancel the load.</param>
    /// <returns>The stored DTO, or <see langword="null"/> when no value exists.</returns>
    protected abstract UniTask<TDto?> LoadValueAsync(CancellationToken ct);

    /// <summary>
    /// Saves the prepared DTO to storage during commit.
    /// </summary>
    /// <param name="value">The DTO to persist, or <see langword="null"/> to clear the value.</param>
    /// <param name="ct">A token that can cancel the save.</param>
    /// <returns>A task that completes when persistence finishes.</returns>
    protected abstract UniTask SaveValueAsync(TDto? value, CancellationToken ct);

    UniTask IRepositoryParticipant.PrepareCommitAsync(object transactionState, CancellationToken ct)
    {
        var state = (SingletonTransactionState)transactionState;
        state.PreparedValue = state.WriteState.HasValue ? state.WriteState.Value : null;
        state.PreparedDto = state.PreparedValue is null ? null : ToDto(state.PreparedValue);
        return SaveValueAsync(state.PreparedDto, ct);
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

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException($"{GetType().Name} has not been initialized. Call InitializeAsync before use.");
        }
    }

    private sealed class SingletonTransactionState
    {
        public SingletonTransactionState(RepositoryWriteState<TEntity> writeState)
        {
            WriteState = writeState;
        }

        public RepositoryWriteState<TEntity> WriteState { get; }

        public TEntity? PreparedValue { get; set; }

        public TDto? PreparedDto { get; set; }
    }
}
}
