#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Lilja.Repository.Diagnostics;

namespace Lilja.Repository
{
    /// <summary>
    /// generated keyed in-memory repository の共通実装。
    /// </summary>
    public abstract class InMemoryKeyedRepositoryBase<TEntity, TKey>
        where TEntity : class
        where TKey : notnull
    {
        private Dictionary<TKey, TEntity> _storage = new Dictionary<TKey, TEntity>();

        protected InMemoryKeyedRepositoryBase()
        {
        }

        public UniTask InitializeAsync(CancellationToken ct = default)
        {
            return UniTask.CompletedTask;
        }

        public TEntity? Read(IReadOnlyTx tx, TKey key)
        {
            return RepositoryTx.TryGetKeyedValue(tx, this, (IReadOnlyDictionary<TKey, TEntity>)_storage, key, out var entityValue)
                ? entityValue
                : null;
        }

        public void Create(IReadWriteTx tx, TEntity entity)
        {
            RepositoryTx.UpsertKeyedValue(
                tx,
                this,
                (IReadOnlyDictionary<TKey, TEntity>)_storage,
                GetKey(entity),
                entity,
                PersistStateAsync,
                state => _storage = state,
                _storage.Comparer);
        }

        public void Update(IReadWriteTx tx, TEntity entity)
        {
            RepositoryTx.UpsertKeyedValue(
                tx,
                this,
                (IReadOnlyDictionary<TKey, TEntity>)_storage,
                GetKey(entity),
                entity,
                PersistStateAsync,
                state => _storage = state,
                _storage.Comparer);
        }

        public void Delete(IReadWriteTx tx, TKey key)
        {
            RepositoryTx.RemoveKeyedValue(
                tx,
                this,
                (IReadOnlyDictionary<TKey, TEntity>)_storage,
                key,
                PersistStateAsync,
                state => _storage = state,
                _storage.Comparer);
        }

        public IReadOnlyList<TEntity> All(IReadOnlyTx tx)
        {
            var list = new List<TEntity>(RepositoryTx.GetKeyedCount(tx, this, (IReadOnlyDictionary<TKey, TEntity>)_storage));
            foreach (var entity in RepositoryTx.EnumerateKeyedValues(tx, this, (IReadOnlyDictionary<TKey, TEntity>)_storage))
            {
                list.Add(entity);
            }

            return list;
        }

        protected abstract TKey GetKey(TEntity entity);

        protected virtual UniTask PersistStateAsync(Dictionary<TKey, TEntity> state, CancellationToken ct)
        {
            return UniTask.CompletedTask;
        }

#if UNITY_EDITOR
        protected void TrackRepository(RepositoryTracker.RepositoryType repositoryType)
        {
            RepositoryTracker.Track(this, repositoryType);
        }
#endif
    }

    /// <summary>
    /// generated singleton in-memory repository の共通実装。
    /// </summary>
    public abstract class InMemorySingletonRepositoryBase<TEntity>
        where TEntity : class
    {
        private TEntity? _entity;

        protected InMemorySingletonRepositoryBase()
        {
        }

        public UniTask InitializeAsync(CancellationToken ct = default)
        {
            return UniTask.CompletedTask;
        }

        public TEntity? Read(IReadOnlyTx tx)
        {
            return RepositoryTx.ReadState(tx, this, () => _entity);
        }

        public void Create(IReadWriteTx tx, TEntity entity)
        {
            GetWriteState(tx).Value = entity;
        }

        public void Update(IReadWriteTx tx, TEntity entity)
        {
            GetWriteState(tx).Value = entity;
        }

        public void Delete(IReadWriteTx tx)
        {
            GetWriteState(tx).Value = null;
        }

        private RepositoryWriteState<TEntity?> GetWriteState(IReadWriteTx tx)
        {
            return RepositoryTx.WriteState(
                tx,
                this,
                () => _entity,
                PersistStateAsync,
                state => _entity = state);
        }

        protected virtual UniTask PersistStateAsync(TEntity? state, CancellationToken ct)
        {
            return UniTask.CompletedTask;
        }

#if UNITY_EDITOR
        protected void TrackRepository(RepositoryTracker.RepositoryType repositoryType)
        {
            RepositoryTracker.Track(this, repositoryType);
        }
#endif
    }

    /// <summary>
    /// generated keyed persisted repository の共通実装。
    /// </summary>
    public abstract class PersistedKeyedRepositoryBase<TEntity, TKey, TDto>
        where TEntity : class
        where TKey : notnull
        where TDto : class
    {
        private readonly SemaphoreSlim _initializationGate = new SemaphoreSlim(1, 1);
        private readonly string _filePath;
        private Dictionary<TKey, TDto> _cache = new Dictionary<TKey, TDto>();
        private bool _initialized;

        protected PersistedKeyedRepositoryBase(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path must not be empty.", nameof(filePath));
            }

            _filePath = filePath;
            RuntimeInstanceMonitor.TrackPersistedRepository(this, _filePath);
        }

        protected string FilePath => _filePath;

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

                _cache = await LoadStateAsync(ct);
                _initialized = true;
            }
            finally
            {
                _initializationGate.Release();
            }
        }

        public TEntity? Read(IReadOnlyTx tx, TKey key)
        {
            EnsureInitialized();
            return RepositoryTx.TryGetKeyedValue(tx, this, (IReadOnlyDictionary<TKey, TDto>)_cache, key, out var dto)
                ? FromDto(dto)
                : null;
        }

        public void Create(IReadWriteTx tx, TEntity entity)
        {
            EnsureInitialized();
            var dto = ToDto(entity);
            RepositoryTx.UpsertKeyedValue(
                tx,
                this,
                (IReadOnlyDictionary<TKey, TDto>)_cache,
                GetKeyFromDto(dto),
                dto,
                PersistStateAsync,
                state => _cache = state,
                _cache.Comparer);
        }

        public void Update(IReadWriteTx tx, TEntity entity)
        {
            EnsureInitialized();
            var dto = ToDto(entity);
            RepositoryTx.UpsertKeyedValue(
                tx,
                this,
                (IReadOnlyDictionary<TKey, TDto>)_cache,
                GetKeyFromDto(dto),
                dto,
                PersistStateAsync,
                state => _cache = state,
                _cache.Comparer);
        }

        public void Delete(IReadWriteTx tx, TKey key)
        {
            EnsureInitialized();
            RepositoryTx.RemoveKeyedValue(
                tx,
                this,
                (IReadOnlyDictionary<TKey, TDto>)_cache,
                key,
                PersistStateAsync,
                state => _cache = state,
                _cache.Comparer);
        }

        public IReadOnlyList<TEntity> All(IReadOnlyTx tx)
        {
            EnsureInitialized();
            var list = new List<TEntity>(RepositoryTx.GetKeyedCount(tx, this, (IReadOnlyDictionary<TKey, TDto>)_cache));
            foreach (var dto in RepositoryTx.EnumerateKeyedValues(tx, this, (IReadOnlyDictionary<TKey, TDto>)_cache))
            {
                list.Add(FromDto(dto));
            }

            return list;
        }

        protected abstract TDto ToDto(TEntity entity);

        protected abstract TEntity FromDto(TDto dto);

        protected abstract TKey GetKeyFromDto(TDto dto);

        protected abstract UniTask<IReadOnlyList<TDto>?> LoadItemsAsync(CancellationToken ct);

        protected abstract UniTask SaveItemsAsync(IReadOnlyList<TDto> items, CancellationToken ct);

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            throw new InvalidOperationException($"{GetType().Name} must be initialized by calling InitializeAsync before use.");
        }

        private async UniTask<Dictionary<TKey, TDto>> LoadStateAsync(CancellationToken ct)
        {
            var items = await LoadItemsAsync(ct);
            var cache = items == null
                ? new Dictionary<TKey, TDto>()
                : new Dictionary<TKey, TDto>(items.Count);

            if (items == null)
            {
                return cache;
            }

            foreach (var dto in items)
            {
                cache[GetKeyFromDto(dto)] = dto;
            }

            return cache;
        }

        private UniTask PersistStateAsync(Dictionary<TKey, TDto> state, CancellationToken ct)
        {
            var items = new List<TDto>(state.Count);
            items.AddRange(state.Values);
            return SaveItemsAsync(items, ct);
        }

#if UNITY_EDITOR
        protected void TrackRepository(RepositoryTracker.RepositoryType repositoryType)
        {
            RepositoryTracker.Track(this, repositoryType);
        }
#endif
    }

    /// <summary>
    /// generated singleton persisted repository の共通実装。
    /// </summary>
    public abstract class PersistedSingletonRepositoryBase<TEntity, TDto>
        where TEntity : class
        where TDto : class
    {
        private readonly SemaphoreSlim _initializationGate = new SemaphoreSlim(1, 1);
        private readonly string _filePath;
        private TDto? _cache;
        private bool _initialized;

        protected PersistedSingletonRepositoryBase(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path must not be empty.", nameof(filePath));
            }

            _filePath = filePath;
            RuntimeInstanceMonitor.TrackPersistedRepository(this, _filePath);
        }

        protected string FilePath => _filePath;

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

                _cache = await LoadValueAsync(ct);
                _initialized = true;
            }
            finally
            {
                _initializationGate.Release();
            }
        }

        public TEntity? Read(IReadOnlyTx tx)
        {
            EnsureInitialized();
            var dto = RepositoryTx.ReadState(tx, this, () => _cache);
            return dto is null ? null : FromDto(dto);
        }

        public void Create(IReadWriteTx tx, TEntity entity)
        {
            EnsureInitialized();
            GetWriteState(tx).Value = ToDto(entity);
        }

        public void Update(IReadWriteTx tx, TEntity entity)
        {
            EnsureInitialized();
            GetWriteState(tx).Value = ToDto(entity);
        }

        public void Delete(IReadWriteTx tx)
        {
            EnsureInitialized();
            GetWriteState(tx).Value = null;
        }

        protected abstract TDto ToDto(TEntity entity);

        protected abstract TEntity FromDto(TDto dto);

        protected abstract UniTask<TDto?> LoadValueAsync(CancellationToken ct);

        protected abstract UniTask SaveValueAsync(TDto? value, CancellationToken ct);

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            throw new InvalidOperationException($"{GetType().Name} must be initialized by calling InitializeAsync before use.");
        }

        private RepositoryWriteState<TDto?> GetWriteState(IReadWriteTx tx)
        {
            return RepositoryTx.WriteState(
                tx,
                this,
                () => _cache,
                PersistStateAsync,
                state => _cache = state);
        }

        private UniTask PersistStateAsync(TDto? state, CancellationToken ct)
        {
            return SaveValueAsync(state, ct);
        }

#if UNITY_EDITOR
        protected void TrackRepository(RepositoryTracker.RepositoryType repositoryType)
        {
            RepositoryTracker.Track(this, repositoryType);
        }
#endif
    }
}
