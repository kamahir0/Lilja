#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.Repository
{
    public abstract class InMemoryKeyedRepository<TKey, TEntity, TDto>
        where TKey : notnull
        where TEntity : class
        where TDto : class
    {
        private readonly Func<TEntity, TDto> _toDto;
        private readonly Func<TDto, TEntity> _fromDto;
        private readonly Func<TEntity, TKey> _getKeyFromEntity;
        private readonly Func<TDto, TKey> _getKeyFromDto;
        private readonly Func<TKey, TDto> _createDefaultDto;
        private readonly Dictionary<TKey, TDto> _values = new Dictionary<TKey, TDto>();
#if UNITY_EDITOR
        private readonly global::Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryState _repositoryState;
#endif

        protected InMemoryKeyedRepository(
            Func<TEntity, TDto> toDto,
            Func<TDto, TEntity> fromDto,
            Func<TEntity, TKey> getKeyFromEntity,
            Func<TDto, TKey> getKeyFromDto,
            Func<TKey, TDto> createDefaultDto,
            IReadOnlyList<TEntity>? initialValues = null)
        {
            _toDto = toDto ?? throw new ArgumentNullException(nameof(toDto));
            _fromDto = fromDto ?? throw new ArgumentNullException(nameof(fromDto));
            _getKeyFromEntity = getKeyFromEntity ?? throw new ArgumentNullException(nameof(getKeyFromEntity));
            _getKeyFromDto = getKeyFromDto ?? throw new ArgumentNullException(nameof(getKeyFromDto));
            _createDefaultDto = createDefaultDto ?? throw new ArgumentNullException(nameof(createDefaultDto));
#if UNITY_EDITOR
            _repositoryState = global::Lilja.Repository.Diagnostics.RepositoryTracker.Track(
                this,
                global::Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType.InMemory,
                typeof(TEntity).FullName ?? typeof(TEntity).Name,
                typeof(TEntity).Name + "Repository",
                true);
#endif

            if (initialValues is null)
            {
                return;
            }

            foreach (var entity in initialValues)
            {
                if (entity is null)
                {
                    continue;
                }

                var key = _getKeyFromEntity(entity);
                var dto = _toDto(entity);
                _values[key] = dto;
#if UNITY_EDITOR
                _repositoryState.SetRecord(key, dto);
#endif
            }
        }

        public UniTask LoadAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return UniTask.CompletedTask;
        }

        public UniTask SaveAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return UniTask.CompletedTask;
        }

        public TEntity Get(TKey key)
        {
            if (!_values.TryGetValue(key, out var dto))
            {
                throw new KeyNotFoundException($"Repository record was not found. Key: {key}");
            }

            return _fromDto(dto);
        }

        public bool TryGet(TKey key, out TEntity entity)
        {
            if (!_values.TryGetValue(key, out var dto))
            {
                entity = null!;
                return false;
            }

            entity = _fromDto(dto);
            return true;
        }

        public IReadOnlyList<TEntity> All()
        {
            var values = new List<TEntity>(_values.Count);
            foreach (var dto in _values.Values)
            {
                values.Add(_fromDto(dto));
            }

            return values;
        }

        public void Update(TEntity entity)
        {
            if (entity is null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            var key = _getKeyFromEntity(entity);
            var dto = _toDto(entity);
            _values[key] = dto;
#if UNITY_EDITOR
            _repositoryState.SetRecord(key, dto);
#endif
        }

        public bool Delete(TKey key)
        {
            var removed = _values.Remove(key);
#if UNITY_EDITOR
            if (removed)
            {
                _repositoryState.RemoveRecord(key);
            }
#endif
            return removed;
        }

        public bool Exists(TKey key)
        {
            return _values.ContainsKey(key);
        }

        public void Clear()
        {
            _values.Clear();
#if UNITY_EDITOR
            _repositoryState.Clear();
#endif
        }
    }
}
