#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Lilja.Repository.Diagnostics;

namespace Lilja.Repository
{
    /// <summary>
    /// 単一 writer と snapshot reader を提供する transaction manager。
    /// </summary>
    public class TxManager
    {
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);
        private readonly object _readerSync = new object();
        private bool _readerAdmissionClosed;
        private int _activeReaders;
        private TaskCompletionSource<bool>? _readerAdmissionOpenedSignal;
        private TaskCompletionSource<bool>? _readersDrainedSignal;

        public TxManager()
        {
            RuntimeInstanceMonitor.TrackTxManager(this);
        }

        public void BeginROTransaction(Action<IReadOnlyTx> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            EnterReader();
            using var tx = new ReadOnlyTxImpl(ReleaseReader);
            action(tx);
        }

        public async UniTask BeginROTransactionAsync(Func<IReadOnlyTx, UniTask> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            await EnterReaderAsync();
            using var tx = new ReadOnlyTxImpl(ReleaseReader);
            await action(tx);
        }

        public async UniTask BeginRWTransactionAsync(Action<IReadWriteTx> action, CancellationToken ct = default)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            await BeginRWTransactionAsync(
                tx =>
                {
                    action(tx);
                    return UniTask.CompletedTask;
                },
                ct);
        }

        public async UniTask BeginRWTransactionAsync(Func<IReadWriteTx, UniTask> action, CancellationToken ct = default)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            await _writeLock.WaitAsync(ct);
            try
            {
                using var tx = new ReadWriteTxImpl(this);
                try
                {
                    await action(tx);
                    await tx.CommitAsync(ct);
                }
                catch
                {
                    await tx.RollbackAsync(ct);
                    throw;
                }
            }
            finally
            {
                _writeLock.Release();
            }
        }

        private void EnterReader()
        {
            while (true)
            {
                if (TryEnterReader(out var waitTask))
                {
                    return;
                }

                waitTask!.GetAwaiter().GetResult();
            }
        }

        private async UniTask EnterReaderAsync()
        {
            while (true)
            {
                if (TryEnterReader(out var waitTask))
                {
                    return;
                }

                await waitTask!;
            }
        }

        private bool TryEnterReader(out Task? waitTask)
        {
            lock (_readerSync)
            {
                if (!_readerAdmissionClosed)
                {
                    _activeReaders++;
                    waitTask = null;
                    return true;
                }

                _readerAdmissionOpenedSignal ??= CreateSignal();
                waitTask = _readerAdmissionOpenedSignal.Task;
                return false;
            }
        }

        private async UniTask CloseReaderAdmissionAndWaitForReadersAsync()
        {
            Task? waitTask = null;
            lock (_readerSync)
            {
                _readerAdmissionClosed = true;
                _readerAdmissionOpenedSignal ??= CreateSignal();

                if (_activeReaders > 0)
                {
                    _readersDrainedSignal ??= CreateSignal();
                    waitTask = _readersDrainedSignal.Task;
                }
            }

            if (waitTask == null)
            {
                return;
            }

            await waitTask;
        }

        private void OpenReaderAdmission()
        {
            TaskCompletionSource<bool>? admissionOpenedSignal;
            lock (_readerSync)
            {
                _readerAdmissionClosed = false;
                _readersDrainedSignal = null;
                admissionOpenedSignal = _readerAdmissionOpenedSignal;
                _readerAdmissionOpenedSignal = null;
            }

            admissionOpenedSignal?.TrySetResult(true);
        }

        private void ReleaseReader()
        {
            TaskCompletionSource<bool>? readersDrainedSignal = null;
            lock (_readerSync)
            {
                if (_activeReaders <= 0)
                {
                    return;
                }

                _activeReaders--;
                if (_activeReaders == 0 && _readerAdmissionClosed)
                {
                    readersDrainedSignal = _readersDrainedSignal;
                    _readersDrainedSignal = null;
                }
            }

            readersDrainedSignal?.TrySetResult(true);
        }

        private static TaskCompletionSource<bool> CreateSignal()
        {
            return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private sealed class ReadOnlyTxImpl : IReadOnlyTx, IReadTransactionSnapshotAccess
        {
            private readonly Dictionary<object, object?> _snapshots =
                new Dictionary<object, object?>(ReferenceEqualityComparer.Instance);
            private readonly Action _disposeAction;
            private bool _disposed;

            public ReadOnlyTxImpl(Action disposeAction)
            {
                _disposeAction = disposeAction ?? throw new ArgumentNullException(nameof(disposeAction));
            }

            public TState GetOrAddSnapshot<TState>(
                object repository,
                Func<TState> getCommittedState)
            {
                EnsureNotDisposed();

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

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _snapshots.Clear();
                _disposeAction();
            }

            private void EnsureNotDisposed()
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(ReadOnlyTxImpl));
                }
            }
        }

        private sealed class ReadWriteTxImpl : IReadWriteTx, IWriteTransactionStateAccess
        {
            private readonly TxManager _owner;
            private readonly Dictionary<object, ITransactionParticipant> _participants =
                new Dictionary<object, ITransactionParticipant>(ReferenceEqualityComparer.Instance);
            private bool _disposed;

            public ReadWriteTxImpl(TxManager owner)
            {
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            }

            public bool TryGetState<TState>(
                object repository,
                [MaybeNullWhen(false)] out TState state)
            {
                EnsureNotDisposed();

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
                EnsureNotDisposed();

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
                EnsureNotDisposed();

                if (_participants.TryGetValue(repository, out var participant) &&
                    participant is IOverlayStateParticipant<TKey, TValue> typedParticipant)
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
                EnsureNotDisposed();

                if (_participants.TryGetValue(repository, out var existingParticipant))
                {
                    if (existingParticipant is IOverlayStateParticipant<TKey, TValue> typedParticipant)
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

            internal async UniTask CommitAsync(CancellationToken cancellationToken)
            {
                EnsureNotDisposed();

                if (_participants.Count == 0)
                {
                    return;
                }

                foreach (var participant in _participants.Values)
                {
                    await participant.PrepareCommitAsync(cancellationToken);
                }

                await _owner.CloseReaderAdmissionAndWaitForReadersAsync();
                try
                {
                    foreach (var participant in _participants.Values)
                    {
                        participant.ApplyCommit();
                    }
                }
                finally
                {
                    _owner.OpenReaderAdmission();
                }
            }

            internal async UniTask RollbackAsync(CancellationToken cancellationToken)
            {
                EnsureNotDisposed();

                foreach (var participant in _participants.Values)
                {
                    await participant.RollbackAsync(cancellationToken);
                }
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _participants.Clear();
            }

            private void EnsureNotDisposed()
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(ReadWriteTxImpl));
                }
            }
        }
    }
}
