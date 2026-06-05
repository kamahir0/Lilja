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

                _values[_getKeyFromEntity(entity)] = _toDto(entity);
            }
        }

        public UniTask<TEntity> LoadAsync(TKey key, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return UniTask.FromResult(_fromDto(_values.TryGetValue(key, out var dto) ? dto : _createDefaultDto(key)));
        }

        public UniTask<IReadOnlyList<TEntity>> LoadAllAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var values = new List<TEntity>(_values.Count);
            foreach (var dto in _values.Values)
            {
                values.Add(_fromDto(dto));
            }

            return UniTask.FromResult((IReadOnlyList<TEntity>)values);
        }

        public UniTask SaveAsync(TEntity entity, CancellationToken ct = default)
        {
            if (entity is null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            ct.ThrowIfCancellationRequested();
            _values[_getKeyFromEntity(entity)] = _toDto(entity);
            return UniTask.CompletedTask;
        }

        public UniTask<bool> DeleteAsync(TKey key, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return UniTask.FromResult(_values.Remove(key));
        }

        public bool Exists(TKey key)
        {
            return _values.ContainsKey(key);
        }
    }
}
