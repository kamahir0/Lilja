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
        private bool _hasValue;
        private TDto? _value;
#if UNITY_EDITOR
        private readonly global::Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryState _repositoryState;
#endif

        protected JsonRepository(string filePath, Func<TEntity, TDto> toDto, Func<TDto, TEntity> fromDto, Func<TDto> createDefaultDto)
        {
            FilePath = string.IsNullOrWhiteSpace(filePath) ? throw new ArgumentException("File path must not be empty.", nameof(filePath)) : filePath;
            _toDto = toDto ?? throw new ArgumentNullException(nameof(toDto));
            _fromDto = fromDto ?? throw new ArgumentNullException(nameof(fromDto));
            _createDefaultDto = createDefaultDto ?? throw new ArgumentNullException(nameof(createDefaultDto));
#if UNITY_EDITOR
            var storageIdentifier = Path.GetFileNameWithoutExtension(FilePath);
            _repositoryState = global::Lilja.Repository.Diagnostics.RepositoryTracker.Track(
                this,
                global::Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType.Json,
                storageIdentifier,
                storageIdentifier + "Repository",
                false);
#endif
        }

        protected string FilePath { get; }

        public async UniTask LoadAsync(CancellationToken ct = default)
        {
            var path = FilePath;
            var dto = await UniTask.RunOnThreadPool(() =>
            {
                ct.ThrowIfCancellationRequested();
                if (!File.Exists(path))
                {
                    return null;
                }

                var raw = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return null;
                }

                return JsonUtility.FromJson<TDto>(raw);
            }, cancellationToken: ct);

            _hasValue = dto is not null;
            _value = dto;
#if UNITY_EDITOR
            if (_hasValue)
            {
                _repositoryState.SetValue(_value);
            }
            else
            {
                _repositoryState.Clear();
            }
#endif
        }

        public UniTask SaveAsync(CancellationToken ct = default)
        {
            var path = FilePath;
            var hasValue = _hasValue;
            var value = _value;
            return UniTask.RunOnThreadPool(() =>
            {
                ct.ThrowIfCancellationRequested();
                if (!hasValue || value is null)
                {
                    AtomicFileWriter.DeleteIfExists(path);
                    return;
                }

                AtomicFileWriter.WriteAllText(path, JsonUtility.ToJson(value, false));
            }, cancellationToken: ct);
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
