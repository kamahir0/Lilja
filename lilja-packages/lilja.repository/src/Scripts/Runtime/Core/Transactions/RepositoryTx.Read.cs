#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Lilja.Repository
{
    internal static partial class RepositoryTx
    {
        internal static TState ReadState<TState>(
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

        internal static bool TryGetKeyedValue<TKey, TValue>(
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

        internal static int GetKeyedCount<TKey, TValue>(
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

        internal static IEnumerable<TValue> EnumerateKeyedValues<TKey, TValue>(
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
}
