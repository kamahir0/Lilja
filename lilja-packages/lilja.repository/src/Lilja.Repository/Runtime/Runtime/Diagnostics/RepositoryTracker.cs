#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Lilja.Repository.Diagnostics
{
    public static class RepositoryTracker
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<RepositoryType, List<WeakReference>> Repositories = new Dictionary<RepositoryType, List<WeakReference>>
        {
            { RepositoryType.InMemory, new List<WeakReference>() },
            { RepositoryType.Json, new List<WeakReference>() },
            { RepositoryType.MessagePack, new List<WeakReference>() },
        };
        private static readonly Dictionary<RepositoryType, List<RepositoryState>> States = new Dictionary<RepositoryType, List<RepositoryState>>
        {
            { RepositoryType.InMemory, new List<RepositoryState>() },
            { RepositoryType.Json, new List<RepositoryState>() },
            { RepositoryType.MessagePack, new List<RepositoryState>() },
        };
        private static readonly Dictionary<RepositoryType, long> Versions = new Dictionary<RepositoryType, long>
        {
            { RepositoryType.InMemory, 0L },
            { RepositoryType.Json, 0L },
            { RepositoryType.MessagePack, 0L },
        };

        public enum RepositoryType
        {
            InMemory,
            Json,
            MessagePack,
        }

        public static void Track(object repository, RepositoryType type)
        {
            if (repository is null)
            {
                throw new ArgumentNullException(nameof(repository));
            }

            lock (SyncRoot)
            {
                var references = Repositories[type];
                Cleanup(references);
                references.Add(new WeakReference(repository));
            }
        }

        public static RepositoryState Track(
            object repository,
            RepositoryType type,
            string storageIdentifier,
            string displayName,
            bool isKeyed)
        {
            Track(repository, type);
            var state = new RepositoryState(repository, type, storageIdentifier, displayName, isKeyed);
            lock (SyncRoot)
            {
                Cleanup(Repositories[type]);
                CleanupStates(States[type]);
                States[type].Add(state);
                IncrementVersionLocked(type);
            }

            return state;
        }

        public static IEnumerable<object> GetAll(RepositoryType type)
        {
            lock (SyncRoot)
            {
                var liveObjects = new List<object>();
                foreach (var reference in Repositories[type])
                {
                    if (reference.Target is object repository)
                    {
                        liveObjects.Add(repository);
                    }
                }

                return liveObjects;
            }
        }

        public static IReadOnlyList<RepositoryStateSnapshot> GetSnapshots(RepositoryType type)
        {
            lock (SyncRoot)
            {
                Cleanup(Repositories[type]);
                CleanupStates(States[type]);
                return States[type].Select(static state => state.ToSnapshot()).ToList();
            }
        }

        public static long GetVersion(RepositoryType type)
        {
            lock (SyncRoot)
            {
                Cleanup(Repositories[type]);
                CleanupStates(States[type]);
                return Versions[type];
            }
        }

        private static void Cleanup(List<WeakReference> references)
        {
            for (var index = references.Count - 1; index >= 0; index--)
            {
                if (!references[index].IsAlive)
                {
                    references.RemoveAt(index);
                }
            }
        }

        private static void CleanupStates(List<RepositoryState> states)
        {
            for (var index = states.Count - 1; index >= 0; index--)
            {
                if (!states[index].IsAlive)
                {
                    states.RemoveAt(index);
                }
            }
        }

        private static void IncrementVersionLocked(RepositoryType type)
        {
            unchecked
            {
                Versions[type]++;
            }
        }

        public sealed class RepositoryState
        {
            private readonly WeakReference _repository;
            private readonly Dictionary<string, object?> _records = new Dictionary<string, object?>();
            private object? _singleValue;

            internal RepositoryState(object repository, RepositoryType type, string storageIdentifier, string displayName, bool isKeyed)
            {
                _repository = new WeakReference(repository);
                Type = type;
                StorageIdentifier = storageIdentifier;
                DisplayName = displayName;
                IsKeyed = isKeyed;
                StableId = $"{type}:{storageIdentifier}:{RuntimeHelpers.GetHashCode(repository)}";
            }

            public RepositoryType Type { get; }

            public string StorageIdentifier { get; }

            public string DisplayName { get; }

            public bool IsKeyed { get; }

            internal bool IsAlive => _repository.IsAlive;

            internal string StableId { get; }

            public void SetValue(object? value)
            {
                lock (SyncRoot)
                {
                    _singleValue = value;
                    IncrementVersionLocked(Type);
                }
            }

            public void SetRecord(object key, object? value)
            {
                lock (SyncRoot)
                {
                    _records[NormalizeKey(key)] = value;
                    IncrementVersionLocked(Type);
                }
            }

            public void RemoveRecord(object key)
            {
                lock (SyncRoot)
                {
                    if (_records.Remove(NormalizeKey(key)))
                    {
                        IncrementVersionLocked(Type);
                    }
                }
            }

            public void Clear()
            {
                lock (SyncRoot)
                {
                    var hadValue = _singleValue is not null;
                    _singleValue = null;
                    if (hadValue || _records.Count > 0)
                    {
                        _records.Clear();
                        IncrementVersionLocked(Type);
                    }
                    else
                    {
                        _records.Clear();
                    }
                }
            }

            internal RepositoryStateSnapshot ToSnapshot()
            {
                lock (SyncRoot)
                {
                    var records = _records
                        .OrderBy(static item => item.Key, StringComparer.Ordinal)
                        .Select(static item => new RepositoryRecordSnapshot(item.Key, item.Value))
                        .ToList();
                    return new RepositoryStateSnapshot(StableId, Type, StorageIdentifier, DisplayName, IsKeyed, _singleValue, records);
                }
            }

            private static string NormalizeKey(object? key)
            {
                return key?.ToString() ?? "null";
            }
        }

        public sealed class RepositoryStateSnapshot
        {
            internal RepositoryStateSnapshot(
                string stableId,
                RepositoryType type,
                string storageIdentifier,
                string displayName,
                bool isKeyed,
                object? value,
                IReadOnlyList<RepositoryRecordSnapshot> records)
            {
                StableId = stableId;
                Type = type;
                StorageIdentifier = storageIdentifier;
                DisplayName = displayName;
                IsKeyed = isKeyed;
                Value = value;
                Records = records;
            }

            public string StableId { get; }

            public RepositoryType Type { get; }

            public string StorageIdentifier { get; }

            public string DisplayName { get; }

            public bool IsKeyed { get; }

            public object? Value { get; }

            public IReadOnlyList<RepositoryRecordSnapshot> Records { get; }
        }

        public sealed class RepositoryRecordSnapshot
        {
            internal RepositoryRecordSnapshot(string key, object? value)
            {
                Key = key;
                Value = value;
            }

            public string Key { get; }

            public object? Value { get; }
        }
    }
}
