using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.Repository.Internal
{
    /// <summary>
    /// 単一のトランザクションスコープに対するリポジトリ固有の状態を保持します。
    /// </summary>
    internal sealed class RepositoryTx : IReadWriteTx
    {
        private readonly Dictionary<IRepositoryParticipant, object> _participantStates;

        /// <summary>
        /// <see cref="RepositoryTx"/> クラスの新しいインスタンスを初期化します。
        /// </summary>
        /// <param name="isReadWrite">
        /// トランザクションが書き込みをサポートする場合は <see langword="true"/>、それ以外は <see langword="false"/>。
        /// </param>
        public RepositoryTx(bool isReadWrite)
        {
            IsReadWrite = isReadWrite;
            _participantStates = new Dictionary<IRepositoryParticipant, object>();
        }

        /// <summary>
        /// トランザクションが書き込みのステージングをサポートしているかどうかを示す値を取得します。
        /// </summary>
        public bool IsReadWrite { get; }

        /// <summary>
        /// このトランザクションに参加しているリポジトリが存在するかどうかを示す値を取得します。
        /// </summary>
        public bool HasParticipants => _participantStates.Count > 0;

        /// <summary>
        /// トランザクションを破棄済みとしてマークします。
        /// </summary>
        public void Dispose()
        {
            IsDisposed = true;
        }

        /// <summary>
        /// トランザクションが破棄済みかどうかを示す値を取得します。
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// すでにそのトランザクションへ関連付けられている、リポジトリ固有の状態の取得を試みます。
        /// </summary>
        /// <param name="participant">リポジトリ参加者。</param>
        /// <param name="transactionState">以前に登録された状態。</param>
        /// <returns>状態が存在する場合は <see langword="true"/>、それ以外は <see langword="false"/>。</returns>
        public bool TryGetParticipantState(IRepositoryParticipant participant, out object transactionState)
        {
            EnsureNotDisposed();
            return _participantStates.TryGetValue(participant, out transactionState!);
        }

        /// <summary>
        /// そのトランザクションに対する既存のリポジトリ状態を返し、初回アクセス時は新たに作成します。
        /// </summary>
        /// <typeparam name="TState">トランザクション状態の型。</typeparam>
        /// <param name="participant">リポジトリ参加者。</param>
        /// <param name="factory">状態がまだ存在しない場合に作成します。</param>
        /// <returns>既存または新規作成された状態オブジェクト。</returns>
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
        /// 参加しているすべてのリポジトリに対して <see cref="IRepositoryParticipant.PrepareCommitAsync"/> を呼び出します。
        /// </summary>
        /// <param name="ct">コミット準備を取り消せるトークン。</param>
        /// <returns>すべての参加者が状態の準備を終えたときに完了するタスク。</returns>
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
        /// 参加しているすべてのリポジトリに対して、準備済みの状態を反映します。
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
        /// そのトランザクションに対してステージングされた、すべてのリポジトリ状態を破棄します。
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
