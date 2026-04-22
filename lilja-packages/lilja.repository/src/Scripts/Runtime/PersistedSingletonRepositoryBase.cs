#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Lilja.Repository.Internal;

namespace Lilja.Repository
{
public abstract class PersistedSingletonRepositoryBase<TEntity, TDto> : IRepositoryParticipant
    where TEntity : class
    where TDto : class
{
    private readonly SemaphoreSlim _initializationGate = new SemaphoreSlim(1, 1);
    private TEntity? _committedValue;
    private bool _initialized;

    protected PersistedSingletonRepositoryBase(string filePath)
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
            var value = await LoadValueAsync(ct);
            _committedValue = value is null ? null : FromDto(value);
            _initialized = true;
        }
        finally
        {
            _initializationGate.Release();
        }
    }

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

    protected abstract TDto ToDto(TEntity entity);

    protected abstract TEntity FromDto(TDto dto);

    protected abstract UniTask<TDto?> LoadValueAsync(CancellationToken ct);

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
