using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.Repository.Internal
{
internal sealed class RepositoryTx : IReadWriteTx
{
    private readonly Dictionary<IRepositoryParticipant, object> _participantStates;

    public RepositoryTx(bool isReadWrite)
    {
        IsReadWrite = isReadWrite;
        _participantStates = new Dictionary<IRepositoryParticipant, object>();
    }

    public bool IsReadWrite { get; }

    public bool HasParticipants => _participantStates.Count > 0;

    public void Dispose()
    {
        IsDisposed = true;
    }

    public bool IsDisposed { get; private set; }

    public bool TryGetParticipantState(IRepositoryParticipant participant, out object transactionState)
    {
        EnsureNotDisposed();
        return _participantStates.TryGetValue(participant, out transactionState!);
    }

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

    public async UniTask PrepareCommitAsync(CancellationToken ct)
    {
        EnsureNotDisposed();

        foreach (var pair in _participantStates)
        {
            ct.ThrowIfCancellationRequested();
            await pair.Key.PrepareCommitAsync(pair.Value, ct);
        }
    }

    public void ApplyCommit()
    {
        EnsureNotDisposed();

        foreach (var pair in _participantStates)
        {
            pair.Key.ApplyCommit(pair.Value);
        }
    }

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
