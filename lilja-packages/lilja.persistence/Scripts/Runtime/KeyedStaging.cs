#nullable enable
using System;
using System.Collections.Generic;

namespace Lilja.Persistence
{
    public abstract class KeyedStaging<TEntity, TKey>
        where TEntity : class, IKeyed<TKey>
        where TKey : notnull
    {
        public abstract TEntity? GetOrDefault(TKey key);

        public abstract bool TryGet(TKey key, out TEntity? entity);

        public abstract bool Contains(TKey key);

        public abstract IReadOnlyList<TEntity> All();

        public abstract void Update(TEntity entity);

        public abstract bool Delete(TKey key);
    }

    public abstract class KeyedStaging<TEntity, TKey, TDto> : KeyedStaging<TEntity, TKey>, IStagingSnapshot<TDto>
        where TEntity : class, IKeyed<TKey>
        where TKey : notnull
        where TDto : class
    {
        private readonly Dictionary<TKey, TDto> _dtos = new();

        public override TEntity? GetOrDefault(TKey key)
        {
            return TryGetDto(key, out var dto) ? ToEntity(dto) : null;
        }

        public override bool TryGet(TKey key, out TEntity? entity)
        {
            if (TryGetDto(key, out var dto))
            {
                entity = ToEntity(dto);
                return true;
            }

            entity = null;
            return false;
        }

        public override bool Contains(TKey key)
        {
            return _dtos.ContainsKey(key);
        }

        public override IReadOnlyList<TEntity> All()
        {
            var entities = new List<TEntity>(_dtos.Count);
            foreach (var dto in _dtos.Values)
            {
                entities.Add(ToEntity(dto));
            }

            return entities;
        }

        public override void Update(TEntity entity)
        {
            if (entity is null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            _dtos[entity.Key] = ToDto(entity);
        }

        public override bool Delete(TKey key)
        {
            return _dtos.Remove(key);
        }

        public IReadOnlyList<TDto> ExportDtos()
        {
            return new List<TDto>(_dtos.Values);
        }

        public void ImportDtos(IEnumerable<TDto>? dtos)
        {
            _dtos.Clear();
            if (dtos is null)
            {
                return;
            }

            foreach (var dto in dtos)
            {
                if (dto is null)
                {
                    continue;
                }

                _dtos[GetKey(dto)] = dto;
            }
        }

        protected abstract TEntity ToEntity(TDto dto);

        protected abstract TDto ToDto(TEntity entity);

        protected abstract TKey GetKey(TDto dto);

        private bool TryGetDto(TKey key, out TDto dto)
        {
            return _dtos.TryGetValue(key, out dto!);
        }
    }
}
