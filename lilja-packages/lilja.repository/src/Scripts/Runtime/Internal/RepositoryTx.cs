using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.Repository.Internal
{
    /// <summary>
    /// Stores repository-specific state for a single transaction scope.
    /// </summary>
    internal sealed class RepositoryTx : IReadWriteTx
    {
        private readonly Dictionary<IRepositoryParticipant, object> _participantStates;

        /// <summary>
        /// Initializes a new instance of the <see cref="RepositoryTx"/> class.
        /// </summary>
        /// <param name="isReadWrite">
        /// <see langword="true"/> when the transaction supports writes; otherwise <see langword="false"/>.
        /// </param>
        public RepositoryTx(bool isReadWrite)
        {
            IsReadWrite = isReadWrite;
            _participantStates = new Dictionary<IRepositoryParticipant, object>();
        }

        /// <summary>
        /// Gets a value indicating whether the transaction supports write staging.
        /// </summary>
        public bool IsReadWrite { get; }

        /// <summary>
        /// Gets a value indicating whether any repositories have joined this transaction.
        /// </summary>
        public bool HasParticipants => _participantStates.Count > 0;

        /// <summary>
        /// Marks the transaction as disposed.
        /// </summary>
        public void Dispose()
        {
            IsDisposed = true;
        }

        /// <summary>
        /// Gets a value indicating whether the transaction has been disposed.
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// Attempts to retrieve repository-specific state that has already been associated with the transaction.
        /// </summary>
        /// <param name="participant">The repository participant.</param>
        /// <param name="transactionState">The previously registered state.</param>
        /// <returns><see langword="true"/> when state exists; otherwise <see langword="false"/>.</returns>
        public bool TryGetParticipantState(IRepositoryParticipant participant, out object transactionState)
        {
            EnsureNotDisposed();
            return _participantStates.TryGetValue(participant, out transactionState!);
        }

        /// <summary>
        /// Returns existing repository state for the transaction, or creates it on first access.
        /// </summary>
        /// <typeparam name="TState">The type of transaction state.</typeparam>
        /// <param name="participant">The repository participant.</param>
        /// <param name="factory">Creates the state when it does not already exist.</param>
        /// <returns>The existing or newly created state object.</returns>
        public TState GetOrCreateParticipantState<TState>(IRepositoryParticipant participant, Func<TState> factory)
            where TState : class
        {
            EnsureNotDisposed();

            if (!IsReadWrite)
            {
                throw new InvalidOperationException("This transaction does not support writes.");
            }

            if (_participantStates.TryGetValue(participant, out var existing))
            {
                return (TState)existing;
            }

            var created = factory();
            _participantStates.Add(participant, created);
            return created;
        }

        /// <summary>
        /// Invokes <see cref="IRepositoryParticipant.PrepareCommitAsync"/> for every participating repository.
        /// </summary>
        /// <param name="ct">A token that can cancel commit preparation.</param>
        /// <returns>A task that completes when every participant has prepared its state.</returns>
        public async UniTask PrepareCommitAsync(CancellationToken ct)
        {
            EnsureNotDisposed();

            foreach (var pair in _participantStates)
            {
                ct.ThrowIfCancellationRequested();
                await pair.Key.PrepareCommitAsync(pair.Value, ct);
            }
        }

        /// <summary>
        /// Applies the prepared state for every participating repository.
        /// </summary>
        public void ApplyCommit()
        {
            EnsureNotDisposed();

            foreach (var pair in _participantStates)
            {
                pair.Key.ApplyCommit(pair.Value);
            }
        }

        /// <summary>
        /// Discards all staged repository state for the transaction.
        /// </summary>
        public void Rollback()
        {
            EnsureNotDisposed();
            _participantStates.Clear();
        }

        private void EnsureNotDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(RepositoryTx));
            }
        }
    }
}
