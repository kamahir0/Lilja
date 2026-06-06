#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.Repository
{
    public abstract class InMemoryRepository<TEntity, TDto>
        where TEntity : class
        where TDto : class
    {
        private readonly Func<TEntity, TDto> _toDto;
        private readonly Func<TDto, TEntity> _fromDto;
        private readonly Func<TDto> _createDefaultDto;
        private bool _hasValue;
        private TDto? _value;
#if UNITY_EDITOR
        private readonly global::Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryState _repositoryState;
#endif

        protected InMemoryRepository(Func<TEntity, TDto> toDto, Func<TDto, TEntity> fromDto, Func<TDto> createDefaultDto, TEntity? initialValue = null)
        {
            _toDto = toDto ?? throw new ArgumentNullException(nameof(toDto));
            _fromDto = fromDto ?? throw new ArgumentNullException(nameof(fromDto));
            _createDefaultDto = createDefaultDto ?? throw new ArgumentNullException(nameof(createDefaultDto));
            _hasValue = initialValue is not null;
            _value = initialValue is null ? null : _toDto(initialValue);
#if UNITY_EDITOR
            _repositoryState = global::Lilja.Repository.Diagnostics.RepositoryTracker.Track(
                this,
                global::Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType.InMemory,
                typeof(TEntity).FullName ?? typeof(TEntity).Name,
                typeof(TEntity).Name + "Repository",
                false);
            _repositoryState.SetValue(_value);
#endif
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

        public TEntity Get()
        {
            if (!_hasValue || _value is null)
            {
                throw new InvalidOperationException("Repository has no value.");
            }

            return _fromDto(_value);
        }

        public bool TryGet(out TEntity entity)
        {
            if (!_hasValue || _value is null)
            {
                entity = null!;
                return false;
            }

            entity = _fromDto(_value);
            return true;
        }

        public void Update(TEntity entity)
        {
            if (entity is null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            _value = _toDto(entity);
            _hasValue = true;
#if UNITY_EDITOR
            _repositoryState.SetValue(_value);
#endif
        }

        public bool Delete()
        {
            var existed = _hasValue;
            _value = null;
            _hasValue = false;
#if UNITY_EDITOR
            _repositoryState.Clear();
#endif
            return existed;
        }

        public bool Exists()
        {
            return _hasValue;
        }

        public void Clear()
        {
            _value = null;
            _hasValue = false;
#if UNITY_EDITOR
            _repositoryState.Clear();
#endif
        }
    }
}
