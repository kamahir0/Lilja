#nullable enable
using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Lilja.Repository
{
    public abstract class JsonRepository<TEntity, TDto>
        where TEntity : class
        where TDto : class
    {
        private readonly Func<TEntity, TDto> _toDto;
        private readonly Func<TDto, TEntity> _fromDto;
        private readonly Func<TDto> _createDefaultDto;

        protected JsonRepository(string filePath, Func<TEntity, TDto> toDto, Func<TDto, TEntity> fromDto, Func<TDto> createDefaultDto)
        {
            FilePath = string.IsNullOrWhiteSpace(filePath) ? throw new ArgumentException("File path must not be empty.", nameof(filePath)) : filePath;
            _toDto = toDto ?? throw new ArgumentNullException(nameof(toDto));
            _fromDto = fromDto ?? throw new ArgumentNullException(nameof(fromDto));
            _createDefaultDto = createDefaultDto ?? throw new ArgumentNullException(nameof(createDefaultDto));
        }

        protected string FilePath { get; }

        public UniTask<TEntity> LoadAsync(CancellationToken ct = default)
        {
            var path = FilePath;
            return UniTask.RunOnThreadPool(() =>
            {
                ct.ThrowIfCancellationRequested();
                if (!File.Exists(path))
                {
                    return _fromDto(_createDefaultDto());
                }

                var raw = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return _fromDto(_createDefaultDto());
                }

                var dto = JsonUtility.FromJson<TDto>(raw);
                return _fromDto(dto ?? _createDefaultDto());
            }, cancellationToken: ct);
        }

        public UniTask SaveAsync(TEntity entity, CancellationToken ct = default)
        {
            if (entity is null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            var path = FilePath;
            var dto = _toDto(entity);
            return UniTask.RunOnThreadPool(() =>
            {
                ct.ThrowIfCancellationRequested();
                AtomicFileWriter.WriteAllText(path, JsonUtility.ToJson(dto, false));
            }, cancellationToken: ct);
        }

        public UniTask<bool> DeleteAsync(CancellationToken ct = default)
        {
            var path = FilePath;
            return UniTask.RunOnThreadPool(() =>
            {
                ct.ThrowIfCancellationRequested();
                return AtomicFileWriter.DeleteIfExists(path);
            }, cancellationToken: ct);
        }

        public bool Exists()
        {
            return File.Exists(FilePath);
        }
    }
}
