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
        private TDto? _value;

        protected InMemoryRepository(Func<TEntity, TDto> toDto, Func<TDto, TEntity> fromDto, Func<TDto> createDefaultDto, TEntity? initialValue = null)
        {
            _toDto = toDto ?? throw new ArgumentNullException(nameof(toDto));
            _fromDto = fromDto ?? throw new ArgumentNullException(nameof(fromDto));
            _createDefaultDto = createDefaultDto ?? throw new ArgumentNullException(nameof(createDefaultDto));
            _value = initialValue is null ? null : _toDto(initialValue);
        }

        public UniTask<TEntity> LoadAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return UniTask.FromResult(_fromDto(_value ?? _createDefaultDto()));
        }

        public UniTask SaveAsync(TEntity entity, CancellationToken ct = default)
        {
            if (entity is null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            ct.ThrowIfCancellationRequested();
            _value = _toDto(entity);
            return UniTask.CompletedTask;
        }

        public UniTask<bool> DeleteAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var existed = _value is not null;
            _value = null;
            return UniTask.FromResult(existed);
        }

        public bool Exists()
        {
            return _value is not null;
        }
    }
}
