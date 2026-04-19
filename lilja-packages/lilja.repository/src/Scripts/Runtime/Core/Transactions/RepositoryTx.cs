#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.Repository
{
    /// <summary>
    /// generated Repository から利用される transaction state helper。
    /// </summary>
    public static class RepositoryTx
    {
        public static TState ReadState<TState>(
            IReadOnlyTx tx,
            object repository,
            Func<TState> getCommittedState)
        {
            ValidateReadArguments(tx, repository, getCommittedState);

            if (tx is IWriteTransactionStateAccess writeTx &&
                writeTx.TryGetState<TState>(repository, out var stagedState))
            {
                return stagedState!;
            }

            if (tx is IReadTransactionSnapshotAccess snapshotTx)
            {
                return snapshotTx.GetOrAddSnapshot(repository, getCommittedState);
            }

            return getCommittedState();
        }

        public static RepositoryWriteState<TState> WriteState<TState>(
            IReadWriteTx tx,
            object repository,
            Func<TState> createStagedState,
            Func<TState, CancellationToken, UniTask> persistAsync,
            Action<TState> applyCommittedState)
        {
            if (tx == null)
            {
                throw new ArgumentNullException(nameof(tx));
            }

            if (repository == null)
            {
                throw new ArgumentNullException(nameof(repository));
            }

            if (createStagedState == null)
            {
                throw new ArgumentNullException(nameof(createStagedState));
            }

            if (persistAsync == null)
            {
                throw new ArgumentNullException(nameof(persistAsync));
            }

            if (applyCommittedState == null)
            {
                throw new ArgumentNullException(nameof(applyCommittedState));
            }

            if (tx is not IWriteTransactionStateAccess writeTx)
            {
                throw new InvalidOperationException(
                    "The provided transaction does not support repository write staging.");
            }

            return writeTx.GetOrAddState(
                repository,
                createStagedState,
                persistAsync,
                applyCommittedState);
        }

        public static bool TryGetKeyedValue<TKey, TValue>(
            IReadOnlyTx tx,
            object repository,
            IReadOnlyDictionary<TKey, TValue> committedState,
            TKey key,
            [MaybeNullWhen(false)] out TValue value)
            where TKey : notnull
        {
            ValidateKeyedReadArguments(tx, repository, committedState);

            if (tx is IWriteTransactionStateAccess writeTx &&
                writeTx.TryGetOverlayState<TKey, TValue>(repository, out var overlayState))
            {
                return overlayState.TryGetValue(key, out value);
            }

            return GetKeyedReadState(tx, repository, committedState).TryGetValue(key, out value);
        }

        public static int GetKeyedCount<TKey, TValue>(
            IReadOnlyTx tx,
            object repository,
            IReadOnlyDictionary<TKey, TValue> committedState)
            where TKey : notnull
        {
            ValidateKeyedReadArguments(tx, repository, committedState);

            if (tx is IWriteTransactionStateAccess writeTx &&
                writeTx.TryGetOverlayState<TKey, TValue>(repository, out var overlayState))
            {
                return overlayState.Count;
            }

            return GetKeyedReadState(tx, repository, committedState).Count;
        }

        public static IEnumerable<TValue> EnumerateKeyedValues<TKey, TValue>(
            IReadOnlyTx tx,
            object repository,
            IReadOnlyDictionary<TKey, TValue> committedState)
            where TKey : notnull
        {
            ValidateKeyedReadArguments(tx, repository, committedState);

            if (tx is IWriteTransactionStateAccess writeTx &&
                writeTx.TryGetOverlayState<TKey, TValue>(repository, out var overlayState))
            {
                return overlayState.EnumerateValues();
            }

            return GetKeyedReadState(tx, repository, committedState).Values;
        }

        public static void UpsertKeyedValue<TKey, TValue>(
            IReadWriteTx tx,
            object repository,
            IReadOnlyDictionary<TKey, TValue> committedState,
            TKey key,
            TValue value,
            Func<Dictionary<TKey, TValue>, CancellationToken, UniTask> persistAsync,
            Action<Dictionary<TKey, TValue>> applyCommittedState,
            IEqualityComparer<TKey>? comparer = null)
            where TKey : notnull
        {
            GetOverlayState(
                tx,
                repository,
                committedState,
                persistAsync,
                applyCommittedState,
                comparer)
                .Upsert(key, value);
        }

        public static bool RemoveKeyedValue<TKey, TValue>(
            IReadWriteTx tx,
            object repository,
            IReadOnlyDictionary<TKey, TValue> committedState,
            TKey key,
            Func<Dictionary<TKey, TValue>, CancellationToken, UniTask> persistAsync,
            Action<Dictionary<TKey, TValue>> applyCommittedState,
            IEqualityComparer<TKey>? comparer = null)
            where TKey : notnull
        {
            return GetOverlayState(
                tx,
                repository,
                committedState,
                persistAsync,
                applyCommittedState,
                comparer)
                .Remove(key);
        }

        private static RepositoryOverlayState<TKey, TValue> GetOverlayState<TKey, TValue>(
            IReadWriteTx tx,
            object repository,
            IReadOnlyDictionary<TKey, TValue> committedState,
            Func<Dictionary<TKey, TValue>, CancellationToken, UniTask> persistAsync,
            Action<Dictionary<TKey, TValue>> applyCommittedState,
            IEqualityComparer<TKey>? comparer)
            where TKey : notnull
        {
            if (tx == null)
            {
                throw new ArgumentNullException(nameof(tx));
            }

            if (repository == null)
            {
                throw new ArgumentNullException(nameof(repository));
            }

            if (committedState == null)
            {
                throw new ArgumentNullException(nameof(committedState));
            }

            if (persistAsync == null)
            {
                throw new ArgumentNullException(nameof(persistAsync));
            }

            if (applyCommittedState == null)
            {
                throw new ArgumentNullException(nameof(applyCommittedState));
            }

            if (tx is not IWriteTransactionStateAccess writeTx)
            {
                throw new InvalidOperationException(
                    "The provided transaction does not support repository write staging.");
            }

            return writeTx.GetOrAddOverlayState(
                repository,
                committedState,
                persistAsync,
                applyCommittedState,
                comparer);
        }

        private static IReadOnlyDictionary<TKey, TValue> GetKeyedReadState<TKey, TValue>(
            IReadOnlyTx tx,
            object repository,
            IReadOnlyDictionary<TKey, TValue> committedState)
            where TKey : notnull
        {
            if (tx is IReadTransactionSnapshotAccess snapshotTx)
            {
                return snapshotTx.GetOrAddSnapshot(repository, () => committedState);
            }

            return committedState;
        }

        private static void ValidateReadArguments<TState>(
            IReadOnlyTx tx,
            object repository,
            Func<TState> getCommittedState)
        {
            if (tx == null)
            {
                throw new ArgumentNullException(nameof(tx));
            }

            if (repository == null)
            {
                throw new ArgumentNullException(nameof(repository));
            }

            if (getCommittedState == null)
            {
                throw new ArgumentNullException(nameof(getCommittedState));
            }
        }

        private static void ValidateKeyedReadArguments<TKey, TValue>(
            IReadOnlyTx tx,
            object repository,
            IReadOnlyDictionary<TKey, TValue> committedState)
            where TKey : notnull
        {
            if (tx == null)
            {
                throw new ArgumentNullException(nameof(tx));
            }

            if (repository == null)
            {
                throw new ArgumentNullException(nameof(repository));
            }

            if (committedState == null)
            {
                throw new ArgumentNullException(nameof(committedState));
            }
        }
    }

    public sealed class RepositoryWriteState<TState>
    {
        internal RepositoryWriteState(TState value)
        {
            Value = value;
        }

        public TState Value { get; set; }
    }

    internal interface IReadTransactionSnapshotAccess
    {
        TState GetOrAddSnapshot<TState>(
            object repository,
            Func<TState> getCommittedState);
    }

    internal interface IWriteTransactionStateAccess
    {
        bool TryGetState<TState>(
            object repository,
            [MaybeNullWhen(false)] out TState state);

        RepositoryWriteState<TState> GetOrAddState<TState>(
            object repository,
            Func<TState> createState,
            Func<TState, CancellationToken, UniTask> persistAsync,
            Action<TState> applyCommittedState);

        bool TryGetOverlayState<TKey, TValue>(
            object repository,
            [MaybeNullWhen(false)] out RepositoryOverlayState<TKey, TValue> state)
            where TKey : notnull;

        RepositoryOverlayState<TKey, TValue> GetOrAddOverlayState<TKey, TValue>(
            object repository,
            IReadOnlyDictionary<TKey, TValue> committedState,
            Func<Dictionary<TKey, TValue>, CancellationToken, UniTask> persistAsync,
            Action<Dictionary<TKey, TValue>> applyCommittedState,
            IEqualityComparer<TKey>? comparer)
            where TKey : notnull;
    }

    internal interface ITransactionParticipant
    {
        UniTask PrepareCommitAsync(CancellationToken cancellationToken);

        void ApplyCommit();

        UniTask RollbackAsync(CancellationToken cancellationToken);
    }

    internal sealed class RepositoryStateParticipant<TState> : ITransactionParticipant
    {
        private readonly Func<TState, CancellationToken, UniTask> _persistAsync;
        private readonly Action<TState> _applyCommittedState;

        public RepositoryStateParticipant(
            TState state,
            Func<TState, CancellationToken, UniTask> persistAsync,
            Action<TState> applyCommittedState)
        {
            State = new RepositoryWriteState<TState>(state);
            _persistAsync = persistAsync;
            _applyCommittedState = applyCommittedState;
        }

        public RepositoryWriteState<TState> State { get; }

        public async UniTask PrepareCommitAsync(CancellationToken cancellationToken)
        {
            await _persistAsync(State.Value, cancellationToken);
        }

        public void ApplyCommit()
        {
            _applyCommittedState(State.Value);
        }

        public UniTask RollbackAsync(CancellationToken cancellationToken)
        {
            return UniTask.CompletedTask;
        }
    }

    public sealed class RepositoryOverlayState<TKey, TValue>
        where TKey : notnull
    {
        private readonly IReadOnlyDictionary<TKey, TValue> _committedState;
        private readonly Dictionary<TKey, TValue> _upserts;
        private readonly HashSet<TKey> _deletedKeys;

        public RepositoryOverlayState(
            IReadOnlyDictionary<TKey, TValue> committedState,
            IEqualityComparer<TKey>? comparer)
        {
            _committedState = committedState ?? throw new ArgumentNullException(nameof(committedState));
            var resolvedComparer = ResolveComparer(committedState, comparer);
            _upserts = new Dictionary<TKey, TValue>(resolvedComparer);
            _deletedKeys = new HashSet<TKey>(resolvedComparer);
        }

        public int Count
        {
            get
            {
                var count = _committedState.Count;

                foreach (var deletedKey in _deletedKeys)
                {
                    if (_committedState.ContainsKey(deletedKey))
                    {
                        count--;
                    }
                }

                foreach (var key in _upserts.Keys)
                {
                    if (!_committedState.ContainsKey(key))
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public bool HasChanges => _upserts.Count > 0 || _deletedKeys.Count > 0;

        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            if (_upserts.TryGetValue(key, out value))
            {
                return true;
            }

            if (_deletedKeys.Contains(key))
            {
                value = default;
                return false;
            }

            return _committedState.TryGetValue(key, out value);
        }

        public void Upsert(TKey key, TValue value)
        {
            _deletedKeys.Remove(key);
            _upserts[key] = value;
        }

        public bool Remove(TKey key)
        {
            if (_upserts.Remove(key))
            {
                if (_committedState.ContainsKey(key))
                {
                    _deletedKeys.Add(key);
                }

                return true;
            }

            if (_deletedKeys.Contains(key))
            {
                return false;
            }

            if (_committedState.ContainsKey(key))
            {
                _deletedKeys.Add(key);
                return true;
            }

            return false;
        }

        public IEnumerable<TValue> EnumerateValues()
        {
            foreach (var pair in _committedState)
            {
                if (_deletedKeys.Contains(pair.Key))
                {
                    continue;
                }

                if (_upserts.TryGetValue(pair.Key, out var updatedValue))
                {
                    yield return updatedValue;
                    continue;
                }

                yield return pair.Value;
            }

            foreach (var pair in _upserts)
            {
                if (_committedState.ContainsKey(pair.Key))
                {
                    continue;
                }

                yield return pair.Value;
            }
        }

        public Dictionary<TKey, TValue> Materialize()
        {
            var mergedState = new Dictionary<TKey, TValue>(Count, _upserts.Comparer);

            foreach (var pair in _committedState)
            {
                if (_deletedKeys.Contains(pair.Key))
                {
                    continue;
                }

                if (_upserts.TryGetValue(pair.Key, out var updatedValue))
                {
                    mergedState[pair.Key] = updatedValue;
                    continue;
                }

                mergedState[pair.Key] = pair.Value;
            }

            foreach (var pair in _upserts)
            {
                if (_committedState.ContainsKey(pair.Key))
                {
                    continue;
                }

                mergedState[pair.Key] = pair.Value;
            }

            return mergedState;
        }

        private static IEqualityComparer<TKey> ResolveComparer(
            IReadOnlyDictionary<TKey, TValue> committedState,
            IEqualityComparer<TKey>? comparer)
        {
            if (comparer != null)
            {
                return comparer;
            }

            if (committedState is Dictionary<TKey, TValue> dictionary)
            {
                return dictionary.Comparer;
            }

            return EqualityComparer<TKey>.Default;
        }
    }

    internal interface IOverlayStateParticipant<TKey, TValue> : ITransactionParticipant
        where TKey : notnull
    {
        RepositoryOverlayState<TKey, TValue> State { get; }
    }

    internal sealed class RepositoryOverlayParticipant<TKey, TValue> : IOverlayStateParticipant<TKey, TValue>
        where TKey : notnull
    {
        private readonly Func<Dictionary<TKey, TValue>, CancellationToken, UniTask> _persistAsync;
        private readonly Action<Dictionary<TKey, TValue>> _applyCommittedState;
        private Dictionary<TKey, TValue>? _mergedState;

        public RepositoryOverlayParticipant(
            IReadOnlyDictionary<TKey, TValue> committedState,
            Func<Dictionary<TKey, TValue>, CancellationToken, UniTask> persistAsync,
            Action<Dictionary<TKey, TValue>> applyCommittedState,
            IEqualityComparer<TKey>? comparer)
        {
            State = new RepositoryOverlayState<TKey, TValue>(committedState, comparer);
            _persistAsync = persistAsync;
            _applyCommittedState = applyCommittedState;
        }

        public RepositoryOverlayState<TKey, TValue> State { get; }

        public async UniTask PrepareCommitAsync(CancellationToken cancellationToken)
        {
            if (!State.HasChanges)
            {
                _mergedState = null;
                return;
            }

            _mergedState = State.Materialize();
            await _persistAsync(_mergedState, cancellationToken);
        }

        public void ApplyCommit()
        {
            if (_mergedState == null)
            {
                return;
            }

            _applyCommittedState(_mergedState);
            _mergedState = null;
        }

        public UniTask RollbackAsync(CancellationToken cancellationToken)
        {
            _mergedState = null;
            return UniTask.CompletedTask;
        }
    }

    internal sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static ReferenceEqualityComparer Instance { get; } = new ReferenceEqualityComparer();

        private ReferenceEqualityComparer()
        {
        }

        public new bool Equals(object? x, object? y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(object obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}
