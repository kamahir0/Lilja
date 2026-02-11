using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.Repository
{
    /// <summary>
    /// トランザクションマネージャ。
    /// SemaphoreSlimによる書き込み直列化とLambdaベースのトランザクションAPIを提供する。
    /// </summary>
    public class TxManager
    {
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// 読み取りトランザクションを開始する。
        /// </summary>
        /// <param name="action">トランザクション内で実行するアクション。</param>
        public void BeginROTransaction(Action<IReadOnlyTx> action)
        {
            using var tx = new ReadOnlyTxImpl();
            action(tx);
        }

        /// <summary>
        /// 読み書きトランザクションを開始する（同期Lambda）。
        /// Lambda正常完了でコミット、例外発生でロールバックされる。
        /// コミット/ロールバック時の非同期アクション（ファイルIO等）はawaitされる。
        /// </summary>
        /// <param name="action">トランザクション内で実行するアクション。</param>
        /// <param name="ct">キャンセルトークン。</param>
        public async UniTask BeginRWTransactionAsync(Action<IReadWriteTx> action, CancellationToken ct = default)
        {
            await _writeLock.WaitAsync(ct);
            try
            {
                using var tx = new ReadWriteTxImpl();
                try
                {
                    action(tx);
                    await tx.ExecuteCommitAsync();
                }
                catch
                {
                    await tx.ExecuteRollbackAsync();
                    throw;
                }
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <summary>
        /// 読み取り専用トランザクションの内部実装。
        /// </summary>
        private sealed class ReadOnlyTxImpl : IReadOnlyTx
        {
            private bool _disposed;

            /// <inheritdoc />
            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// 読み書き可能トランザクションの内部実装。
        /// </summary>
        private sealed class ReadWriteTxImpl : IReadWriteTx
        {
            private readonly List<Func<UniTask>> _commitActions = new List<Func<UniTask>>();
            private readonly List<Func<UniTask>> _rollbackActions = new List<Func<UniTask>>();
            private bool _disposed;

            /// <inheritdoc />
            public void OnCommit(Func<UniTask> asyncAction)
            {
                _commitActions.Add(asyncAction);
            }

            /// <inheritdoc />
            public void OnRollback(Func<UniTask> asyncAction)
            {
                _rollbackActions.Add(asyncAction);
            }

            /// <summary>
            /// 登録されたコミットアクションを順次実行する。
            /// </summary>
            internal async UniTask ExecuteCommitAsync()
            {
                foreach (var action in _commitActions)
                {
                    await action();
                }
            }

            /// <summary>
            /// 登録されたロールバックアクションを順次実行する。
            /// </summary>
            internal async UniTask ExecuteRollbackAsync()
            {
                foreach (var action in _rollbackActions)
                {
                    await action();
                }
            }

            /// <inheritdoc />
            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _commitActions.Clear();
                _rollbackActions.Clear();
            }
        }
    }
}
