#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.Repository
{
    internal sealed class RepositoryWriteState<TState>
    {
        private TState _value;

        internal RepositoryWriteState(TState value)
        {
            _value = value;
        }

        internal bool HasChanges { get; private set; }

        internal TState Value
        {
            get => _value;
            set
            {
                _value = value;
                HasChanges = true;
            }
        }

        internal void AcceptChanges()
        {
            HasChanges = false;
        }
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

        internal RepositoryStateParticipant(
            TState state,
            Func<TState, CancellationToken, UniTask> persistAsync,
            Action<TState> applyCommittedState)
        {
            State = new RepositoryWriteState<TState>(state);
            _persistAsync = persistAsync;
            _applyCommittedState = applyCommittedState;
        }

        internal RepositoryWriteState<TState> State { get; }

        public async UniTask PrepareCommitAsync(CancellationToken cancellationToken)
        {
            if (!State.HasChanges)
            {
                return;
            }

            await _persistAsync(State.Value, cancellationToken);
        }

        public void ApplyCommit()
        {
            if (!State.HasChanges)
            {
                return;
            }

            _applyCommittedState(State.Value);
            State.AcceptChanges();
        }

        public UniTask RollbackAsync(CancellationToken cancellationToken)
        {
            State.AcceptChanges();
            return UniTask.CompletedTask;
        }
    }

    internal sealed class RepositoryOverlayState<TKey, TValue>
        where TKey : notnull
    {
        private readonly IReadOnlyDictionary<TKey, TValue> _committedState;
        private readonly Dictionary<TKey, TValue> _upserts;
        private readonly HashSet<TKey> _deletedKeys;

        internal RepositoryOverlayState(
            IReadOnlyDictionary<TKey, TValue> committedState,
            IEqualityComparer<TKey>? comparer)
        {
            _committedState = committedState ?? throw new ArgumentNullException(nameof(committedState));
            var resolvedComparer = ResolveComparer(committedState, comparer);
            _upserts = new Dictionary<TKey, TValue>(resolvedComparer);
            _deletedKeys = new HashSet<TKey>(resolvedComparer);
        }

        internal int Count
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

        internal bool HasChanges => _upserts.Count > 0 || _deletedKeys.Count > 0;

        internal bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
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

        internal void Upsert(TKey key, TValue value)
        {
            _deletedKeys.Remove(key);
            _upserts[key] = value;
        }

        internal bool Remove(TKey key)
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

        internal IEnumerable<TValue> EnumerateValues()
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

        internal Dictionary<TKey, TValue> Materialize()
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

    internal sealed class RepositoryOverlayParticipant<TKey, TValue> : ITransactionParticipant
        where TKey : notnull
    {
        private readonly Func<Dictionary<TKey, TValue>, CancellationToken, UniTask> _persistAsync;
        private readonly Action<Dictionary<TKey, TValue>> _applyCommittedState;
        private Dictionary<TKey, TValue>? _mergedState;

        internal RepositoryOverlayParticipant(
            IReadOnlyDictionary<TKey, TValue> committedState,
            Func<Dictionary<TKey, TValue>, CancellationToken, UniTask> persistAsync,
            Action<Dictionary<TKey, TValue>> applyCommittedState,
            IEqualityComparer<TKey>? comparer)
        {
            State = new RepositoryOverlayState<TKey, TValue>(committedState, comparer);
            _persistAsync = persistAsync;
            _applyCommittedState = applyCommittedState;
        }

        internal RepositoryOverlayState<TKey, TValue> State { get; }

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
        internal static ReferenceEqualityComparer Instance { get; } = new ReferenceEqualityComparer();

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
