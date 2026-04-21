#nullable enable
using System;
using System.Collections.Generic;

namespace Lilja.Repository
{
    internal sealed class ReadTransactionSnapshotStore
    {
        private readonly Dictionary<object, object?> _snapshots =
            new Dictionary<object, object?>(ReferenceEqualityComparer.Instance);

        internal TState GetOrAddSnapshot<TState>(
            object repository,
            Func<TState> getCommittedState)
        {
            if (repository == null)
            {
                throw new ArgumentNullException(nameof(repository));
            }

            if (getCommittedState == null)
            {
                throw new ArgumentNullException(nameof(getCommittedState));
            }

            if (_snapshots.TryGetValue(repository, out var existingSnapshot))
            {
                return existingSnapshot is null ? default! : (TState)existingSnapshot;
            }

            var snapshot = getCommittedState();
            _snapshots.Add(repository, snapshot);
            return snapshot;
        }

        internal void Clear()
        {
            _snapshots.Clear();
        }
    }
}
