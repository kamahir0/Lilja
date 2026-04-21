#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.Repository
{
    internal sealed class RepositoryTransactionStateStore : IWriteTransactionStateAccess
    {
        private readonly Dictionary<object, ITransactionParticipant> _participants =
            new Dictionary<object, ITransactionParticipant>(ReferenceEqualityComparer.Instance);

        internal bool HasParticipants => _participants.Count > 0;

        public bool TryGetState<TState>(
            object repository,
            [MaybeNullWhen(false)] out TState state)
        {
            if (_participants.TryGetValue(repository, out var participant) &&
                participant is RepositoryStateParticipant<TState> typedParticipant)
            {
                state = typedParticipant.State.Value;
                return true;
            }

            state = default;
            return false;
        }

        public RepositoryWriteState<TState> GetOrAddState<TState>(
            object repository,
            Func<TState> createState,
            Func<TState, CancellationToken, UniTask> persistAsync,
            Action<TState> applyCommittedState)
        {
            if (_participants.TryGetValue(repository, out var existingParticipant))
            {
                if (existingParticipant is RepositoryStateParticipant<TState> typedParticipant)
                {
                    return typedParticipant.State;
                }

                throw new InvalidOperationException("Repository transaction state type mismatch was detected.");
            }

            var participant = new RepositoryStateParticipant<TState>(
                createState(),
                persistAsync,
                applyCommittedState);

            _participants.Add(repository, participant);
            return participant.State;
        }

        public bool TryGetOverlayState<TKey, TValue>(
            object repository,
            [MaybeNullWhen(false)] out RepositoryOverlayState<TKey, TValue> state)
            where TKey : notnull
        {
            if (_participants.TryGetValue(repository, out var participant) &&
                participant is RepositoryOverlayParticipant<TKey, TValue> typedParticipant)
            {
                state = typedParticipant.State;
                return true;
            }

            state = default;
            return false;
        }

        public RepositoryOverlayState<TKey, TValue> GetOrAddOverlayState<TKey, TValue>(
            object repository,
            IReadOnlyDictionary<TKey, TValue> committedState,
            Func<Dictionary<TKey, TValue>, CancellationToken, UniTask> persistAsync,
            Action<Dictionary<TKey, TValue>> applyCommittedState,
            IEqualityComparer<TKey>? comparer)
            where TKey : notnull
        {
            if (_participants.TryGetValue(repository, out var existingParticipant))
            {
                if (existingParticipant is RepositoryOverlayParticipant<TKey, TValue> typedParticipant)
                {
                    return typedParticipant.State;
                }

                throw new InvalidOperationException("Repository transaction state type mismatch was detected.");
            }

            var participant = new RepositoryOverlayParticipant<TKey, TValue>(
                committedState,
                persistAsync,
                applyCommittedState,
                comparer);

            _participants.Add(repository, participant);
            return participant.State;
        }

        internal async UniTask PrepareCommitAsync(CancellationToken cancellationToken)
        {
            foreach (var participant in _participants.Values)
            {
                await participant.PrepareCommitAsync(cancellationToken);
            }
        }

        internal void ApplyCommit()
        {
            foreach (var participant in _participants.Values)
            {
                participant.ApplyCommit();
            }
        }

        internal async UniTask RollbackAsync(CancellationToken cancellationToken)
        {
            foreach (var participant in _participants.Values)
            {
                await participant.RollbackAsync(cancellationToken);
            }
        }

        internal void Clear()
        {
            _participants.Clear();
        }
    }
}
