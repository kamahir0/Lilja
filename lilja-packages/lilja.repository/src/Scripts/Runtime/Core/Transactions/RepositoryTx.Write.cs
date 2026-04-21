#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.Repository
{
    internal static partial class RepositoryTx
    {
        internal static void CreateKeyedValue<TKey, TValue>(
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
            var overlayState = GetOverlayState(
                tx,
                repository,
                committedState,
                persistAsync,
                applyCommittedState,
                comparer);

            if (overlayState.TryGetValue(key, out _))
            {
                throw CreateStrictCrudException("Create", repository, key, "already exists.");
            }

            overlayState.Upsert(key, value);
        }

        internal static void UpdateKeyedValue<TKey, TValue>(
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
            var overlayState = GetOverlayState(
                tx,
                repository,
                committedState,
                persistAsync,
                applyCommittedState,
                comparer);

            if (!overlayState.TryGetValue(key, out _))
            {
                throw CreateStrictCrudException("Update", repository, key, "was not found.");
            }

            overlayState.Upsert(key, value);
        }

        internal static void DeleteKeyedValue<TKey, TValue>(
            IReadWriteTx tx,
            object repository,
            IReadOnlyDictionary<TKey, TValue> committedState,
            TKey key,
            Func<Dictionary<TKey, TValue>, CancellationToken, UniTask> persistAsync,
            Action<Dictionary<TKey, TValue>> applyCommittedState,
            IEqualityComparer<TKey>? comparer = null)
            where TKey : notnull
        {
            var overlayState = GetOverlayState(
                tx,
                repository,
                committedState,
                persistAsync,
                applyCommittedState,
                comparer);

            if (!overlayState.TryGetValue(key, out _))
            {
                throw CreateStrictCrudException("Delete", repository, key, "was not found.");
            }

            overlayState.Remove(key);
        }

        internal static void CreateReferenceStateValue<TState>(
            IReadWriteTx tx,
            object repository,
            Func<TState?> createStagedState,
            Func<TState?, CancellationToken, UniTask> persistAsync,
            Action<TState?> applyCommittedState,
            TState value)
            where TState : class
        {
            var writeState = GetOrAddWriteState(
                tx,
                repository,
                createStagedState,
                persistAsync,
                applyCommittedState);

            if (writeState.Value != null)
            {
                throw CreateStrictCrudException("Create", repository, "already exists.");
            }

            writeState.Value = value;
        }

        internal static void UpdateReferenceStateValue<TState>(
            IReadWriteTx tx,
            object repository,
            Func<TState?> createStagedState,
            Func<TState?, CancellationToken, UniTask> persistAsync,
            Action<TState?> applyCommittedState,
            TState value)
            where TState : class
        {
            var writeState = GetOrAddWriteState(
                tx,
                repository,
                createStagedState,
                persistAsync,
                applyCommittedState);

            if (writeState.Value == null)
            {
                throw CreateStrictCrudException("Update", repository, "was not found.");
            }

            writeState.Value = value;
        }

        internal static void DeleteReferenceStateValue<TState>(
            IReadWriteTx tx,
            object repository,
            Func<TState?> createStagedState,
            Func<TState?, CancellationToken, UniTask> persistAsync,
            Action<TState?> applyCommittedState)
            where TState : class
        {
            var writeState = GetOrAddWriteState(
                tx,
                repository,
                createStagedState,
                persistAsync,
                applyCommittedState);

            if (writeState.Value == null)
            {
                throw CreateStrictCrudException("Delete", repository, "was not found.");
            }

            writeState.Value = null;
        }

        private static RepositoryWriteState<TState> GetOrAddWriteState<TState>(
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

        private static InvalidOperationException CreateStrictCrudException<TKey>(
            string operation,
            object repository,
            TKey key,
            string reason)
        {
            return new InvalidOperationException(
                $"{operation} failed for repository '{repository.GetType().Name}' with key '{FormatIdentifier(key)}': entity {reason}");
        }

        private static InvalidOperationException CreateStrictCrudException(
            string operation,
            object repository,
            string reason)
        {
            return new InvalidOperationException(
                $"{operation} failed for repository '{repository.GetType().Name}': entity {reason}");
        }

        private static string FormatIdentifier<TValue>(TValue value)
        {
            return value?.ToString() ?? "<null>";
        }
    }
}
