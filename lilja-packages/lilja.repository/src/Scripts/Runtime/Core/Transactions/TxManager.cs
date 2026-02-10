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
        /// 読み書きトランザクションを開始する（同期Lambda）。
        /// Lambda正常完了でコミット、例外発生でロールバックされる。
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
                    tx.ExecuteCommit();
                }
                catch
                {
                    tx.ExecuteRollback();
                    throw;
                }
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <summary>
        /// 読み書きトランザクションを開始する（非同期Lambda）。
        /// Lambda正常完了でコミット、例外発生でロールバックされる。
        /// </summary>
        /// <param name="action">トランザクション内で実行する非同期アクション。</param>
        /// <param name="ct">キャンセルトークン。</param>
        public async UniTask BeginRWTransactionAsync(Func<IReadWriteTx, UniTask> action, CancellationToken ct = default)
        {
            await _writeLock.WaitAsync(ct);
            try
            {
                using var tx = new ReadWriteTxImpl();
                try
                {
                    await action(tx);
                    tx.ExecuteCommit();
                }
                catch
                {
                    tx.ExecuteRollback();
                    throw;
                }
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <summary>
        /// 読み取りトランザクションを開始する。
        /// </summary>
        /// <typeparam name="T">戻り値の型。</typeparam>
        /// <param name="func">トランザクション内で実行する関数。</param>
        /// <returns>関数の戻り値。</returns>
        public T BeginROTransaction<T>(Func<IReadOnlyTx, T> func)
        {
            using var tx = new ReadOnlyTxImpl();
            return func(tx);
        }

        /// <summary>
        /// 読み取りトランザクションを開始する（戻り値なし）。
        /// </summary>
        /// <param name="action">トランザクション内で実行するアクション。</param>
        public void BeginROTransaction(Action<IReadOnlyTx> action)
        {
            using var tx = new ReadOnlyTxImpl();
            action(tx);
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
            private readonly List<Action> _commitActions = new List<Action>();
            private readonly List<Action> _rollbackActions = new List<Action>();
            private bool _disposed;

            /// <inheritdoc />
            public void OnCommit(Action action)
            {
                _commitActions.Add(action);
            }

            /// <inheritdoc />
            public void OnRollback(Action action)
            {
                _rollbackActions.Add(action);
            }

            /// <summary>
            /// 登録されたコミットアクションを順次実行する。
            /// </summary>
            internal void ExecuteCommit()
            {
                foreach (var action in _commitActions)
                {
                    action();
                }
            }

            /// <summary>
            /// 登録されたロールバックアクションを順次実行する。
            /// </summary>
            internal void ExecuteRollback()
            {
                foreach (var action in _rollbackActions)
                {
                    action();
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
