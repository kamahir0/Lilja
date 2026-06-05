#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Lilja.Repository
{
    public abstract class JsonKeyedRepository<TKey, TEntity, TDto>
        where TKey : notnull
        where TEntity : class
        where TDto : class
    {
        private readonly Func<TEntity, TDto> _toDto;
        private readonly Func<TDto, TEntity> _fromDto;
        private readonly Func<TEntity, TKey> _getKeyFromEntity;
        private readonly Func<TKey, TDto> _createDefaultDto;

        protected JsonKeyedRepository(
            string directoryPath,
            Func<TEntity, TDto> toDto,
            Func<TDto, TEntity> fromDto,
            Func<TEntity, TKey> getKeyFromEntity,
            Func<TKey, TDto> createDefaultDto)
        {
            DirectoryPath = string.IsNullOrWhiteSpace(directoryPath) ? throw new ArgumentException("Directory path must not be empty.", nameof(directoryPath)) : directoryPath;
            _toDto = toDto ?? throw new ArgumentNullException(nameof(toDto));
            _fromDto = fromDto ?? throw new ArgumentNullException(nameof(fromDto));
            _getKeyFromEntity = getKeyFromEntity ?? throw new ArgumentNullException(nameof(getKeyFromEntity));
            _createDefaultDto = createDefaultDto ?? throw new ArgumentNullException(nameof(createDefaultDto));
        }

        protected string DirectoryPath { get; }

        public UniTask<TEntity> LoadAsync(TKey key, CancellationToken ct = default)
        {
            var path = GetFilePath(key);
            return UniTask.RunOnThreadPool(() =>
            {
                ct.ThrowIfCancellationRequested();
                if (!File.Exists(path))
                {
                    return _fromDto(_createDefaultDto(key));
                }

                var raw = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return _fromDto(_createDefaultDto(key));
                }

                var dto = JsonUtility.FromJson<TDto>(raw);
                return _fromDto(dto ?? _createDefaultDto(key));
            }, cancellationToken: ct);
        }

        public UniTask<IReadOnlyList<TEntity>> LoadAllAsync(CancellationToken ct = default)
        {
            var directory = DirectoryPath;
            return UniTask.RunOnThreadPool<IReadOnlyList<TEntity>>(() =>
            {
                ct.ThrowIfCancellationRequested();
                if (!Directory.Exists(directory))
                {
                    return Array.Empty<TEntity>();
                }

                var files = Directory.GetFiles(directory, "*.json");
                var values = new List<TEntity>(files.Length);
                foreach (var file in files)
                {
                    ct.ThrowIfCancellationRequested();
                    var raw = File.ReadAllText(file);
                    if (string.IsNullOrWhiteSpace(raw))
                    {
                        continue;
                    }

                    var dto = JsonUtility.FromJson<TDto>(raw);
                    if (dto is not null)
                    {
                        values.Add(_fromDto(dto));
                    }
                }

                return values;
            }, cancellationToken: ct);
        }

        public UniTask SaveAsync(TEntity entity, CancellationToken ct = default)
        {
            if (entity is null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            var path = GetFilePath(_getKeyFromEntity(entity));
            var dto = _toDto(entity);
            return UniTask.RunOnThreadPool(() =>
            {
                ct.ThrowIfCancellationRequested();
                AtomicFileWriter.WriteAllText(path, JsonUtility.ToJson(dto, false));
            }, cancellationToken: ct);
        }

        public UniTask<bool> DeleteAsync(TKey key, CancellationToken ct = default)
        {
            var path = GetFilePath(key);
            return UniTask.RunOnThreadPool(() =>
            {
                ct.ThrowIfCancellationRequested();
                return AtomicFileWriter.DeleteIfExists(path);
            }, cancellationToken: ct);
        }

        public bool Exists(TKey key)
        {
            return File.Exists(GetFilePath(key));
        }

        protected string GetFilePath(TKey key)
        {
            return Path.Combine(DirectoryPath, RepositoryFileName.Encode(key) + ".json");
        }
    }
}
