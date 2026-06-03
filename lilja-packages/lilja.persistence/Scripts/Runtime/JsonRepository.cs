#nullable enable
using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Lilja.Persistence
{
    public abstract class JsonRepository<TData, TDto> : Repository<TData>
        where TData : class
        where TDto : class
    {
        protected JsonRepository(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path must not be null, empty, or whitespace.", nameof(filePath));
            }

            FilePath = filePath;
        }

        protected string FilePath { get; }

        public override UniTask<TData> LoadAsync(CancellationToken ct = default)
        {
            return UniTask.RunOnThreadPool(() =>
            {
                ct.ThrowIfCancellationRequested();
                if (!File.Exists(FilePath))
                {
                    return CreateDefault();
                }

                var raw = File.ReadAllText(FilePath);
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return CreateDefault();
                }

                var dto = JsonUtility.FromJson<TDto>(raw);
                return dto is null ? CreateDefault() : FromDto(dto);
            }, cancellationToken: ct);
        }

        public override UniTask SaveAsync(TData data, CancellationToken ct = default)
        {
            if (data is null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            return UniTask.RunOnThreadPool(() =>
            {
                ct.ThrowIfCancellationRequested();
                var json = JsonUtility.ToJson(ToDto(data), false);
                AtomicFileWriter.WriteAllText(FilePath, json);
            }, cancellationToken: ct);
        }

        protected abstract TData CreateDefault();

        protected abstract TData FromDto(TDto dto);

        protected abstract TDto ToDto(TData data);
    }
}
