#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Lilja.Persistence
{
    public abstract class JsonKeyedRepository<TKey, TData, TDto> : KeyedRepository<TKey, TData>
        where TData : class, IKeyed<TKey>
        where TDto : class
    {
        public override UniTask<TData> LoadAsync(TKey key, CancellationToken ct = default)
        {
            var path = GetFilePath(key);
            return UniTask.RunOnThreadPool(() =>
            {
                ct.ThrowIfCancellationRequested();
                if (!File.Exists(path))
                {
                    return CreateDefault(key);
                }

                var raw = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return CreateDefault(key);
                }

                var dto = JsonUtility.FromJson<TDto>(raw);
                return dto is null ? CreateDefault(key) : FromDto(dto);
            }, cancellationToken: ct);
        }

        public override UniTask<IReadOnlyList<TData>> LoadAllAsync(CancellationToken ct = default)
        {
            var directoryPath = GetDirectoryPath();
            return UniTask.RunOnThreadPool<IReadOnlyList<TData>>(() =>
            {
                ct.ThrowIfCancellationRequested();
                if (!Directory.Exists(directoryPath))
                {
                    return Array.Empty<TData>();
                }

                var filePaths = Directory.GetFiles(directoryPath, "*." + FileExtension);
                var dataList = new List<TData>(filePaths.Length);
                foreach (var filePath in filePaths)
                {
                    ct.ThrowIfCancellationRequested();
                    var raw = File.ReadAllText(filePath);
                    if (string.IsNullOrWhiteSpace(raw))
                    {
                        throw new InvalidDataException($"Persistence file is empty: {filePath}");
                    }

                    var dto = JsonUtility.FromJson<TDto>(raw);
                    if (dto is null)
                    {
                        throw new InvalidDataException($"Persistence file could not be deserialized: {filePath}");
                    }

                    dataList.Add(FromDto(dto));
                }

                return dataList;
            }, cancellationToken: ct);
        }

        public override UniTask SaveAsync(TData data, CancellationToken ct = default)
        {
            if (data is null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var path = GetFilePath(data.Key);
            return UniTask.RunOnThreadPool(() =>
            {
                ct.ThrowIfCancellationRequested();
                var json = JsonUtility.ToJson(ToDto(data), false);
                AtomicFileWriter.WriteAllText(path, json);
            }, cancellationToken: ct);
        }

        public override bool Exists(TKey key)
        {
            return File.Exists(GetFilePath(key));
        }

        protected abstract string FileExtension { get; }

        protected abstract string GetDirectoryPath();

        protected abstract string GetFilePath(TKey key);

        protected abstract TData CreateDefault(TKey key);

        protected abstract TData FromDto(TDto dto);

        protected abstract TDto ToDto(TData data);
    }
}
