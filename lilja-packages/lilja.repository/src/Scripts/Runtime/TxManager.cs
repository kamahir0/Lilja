using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Lilja.Repository.Internal;

namespace Lilja.Repository
{
public class TxManager
{
    private readonly object _readerSyncRoot = new object();
    private readonly SemaphoreSlim _writerGate = new SemaphoreSlim(1, 1);
    private int _activeReaders;
    private bool _readerAdmissionOpen = true;

    public TxManager()
    {
        RuntimeInstanceMonitor.TrackTxManager(this);
    }

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
