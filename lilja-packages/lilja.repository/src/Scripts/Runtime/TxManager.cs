using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Lilja.Repository.Internal;

namespace Lilja.Repository
{
/// <summary>
/// Coordinates repository transaction lifetimes for concurrent readers and serialized writers.
/// </summary>
public class TxManager
{
    private readonly object _readerSyncRoot = new object();
    private readonly SemaphoreSlim _writerGate = new SemaphoreSlim(1, 1);
    private int _activeReaders;
    private bool _readerAdmissionOpen = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="TxManager"/> class.
    /// </summary>
    public TxManager()
    {
        RuntimeInstanceMonitor.TrackTxManager(this);
    }

    /// <summary>
    /// Executes a synchronous read-only transaction.
    /// </summary>
    /// <param name="action">The callback that performs read operations.</param>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    public void BeginROTransaction(Action<IReadOnlyTx> action)
    {
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        EnterReader();
        try
        {
            using var tx = new RepositoryTx(false);
            action(tx);
        }
        finally
        {
            ExitReader();
        }
    }

    /// <summary>
    /// Executes an asynchronous read-only transaction.
    /// </summary>
    /// <param name="action">The callback that performs read operations.</param>
    /// <returns>A task that completes when the callback finishes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    public async UniTask BeginROTransactionAsync(Func<IReadOnlyTx, UniTask> action)
    {
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        EnterReader();
        try
        {
            using var tx = new RepositoryTx(false);
            await action(tx);
        }
        finally
        {
            ExitReader();
        }
    }

    /// <summary>
    /// Executes a read-write transaction using a synchronous callback.
    /// </summary>
    /// <param name="action">The callback that stages writes.</param>
    /// <param name="ct">A token that cancels the transaction before commit completes.</param>
    /// <returns>A task that completes after the transaction commits.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    public UniTask BeginRWTransactionAsync(Action<IReadWriteTx> action, CancellationToken ct = default)
    {
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        return BeginRWTransactionAsync(
            tx =>
            {
                action(tx);
                return UniTask.CompletedTask;
            },
            ct);
    }

    /// <summary>
    /// Executes a read-write transaction using an asynchronous callback.
    /// </summary>
    /// <param name="action">The callback that stages writes.</param>
    /// <param name="ct">A token that cancels the transaction before commit completes.</param>
    /// <returns>A task that completes after the transaction commits.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    public async UniTask BeginRWTransactionAsync(Func<IReadWriteTx, UniTask> action, CancellationToken ct = default)
    {
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        await _writerGate.WaitAsync(ct);
        try
        {
            using var tx = new RepositoryTx(true);
            try
            {
                await action(tx);

                if (!tx.HasParticipants)
                {
                    return;
                }

                await tx.PrepareCommitAsync(ct);
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            CloseReaderAdmission();
            try
            {
                WaitForReadersToDrain();
                tx.ApplyCommit();
            }
            finally
            {
                ReopenReaderAdmission();
            }
        }
        finally
        {
            _writerGate.Release();
        }
    }

    private void EnterReader()
    {
        lock (_readerSyncRoot)
        {
            while (!_readerAdmissionOpen)
            {
                Monitor.Wait(_readerSyncRoot);
            }

            _activeReaders++;
        }
    }

    private void ExitReader()
    {
        lock (_readerSyncRoot)
        {
            _activeReaders--;
            if (_activeReaders == 0)
            {
                Monitor.PulseAll(_readerSyncRoot);
            }
        }
    }

    private void CloseReaderAdmission()
    {
        lock (_readerSyncRoot)
        {
            _readerAdmissionOpen = false;
        }
    }

    private void WaitForReadersToDrain()
    {
        lock (_readerSyncRoot)
        {
            while (_activeReaders > 0)
            {
                Monitor.Wait(_readerSyncRoot);
            }
        }
    }

    private void ReopenReaderAdmission()
    {
        lock (_readerSyncRoot)
        {
            _readerAdmissionOpen = true;
            Monitor.PulseAll(_readerSyncRoot);
        }
    }
}
}
