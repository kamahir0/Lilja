#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Lilja.Repository.Internal;

namespace Lilja.Repository
{
    /// <summary>
    /// 完全にメモリ内に保持されるシングルトンリポジトリ向けの、トランザクション対応 CRUD 振る舞いを提供します。
    /// </summary>
    /// <typeparam name="TEntity">リポジトリが管理するエンティティ型。</typeparam>
    public abstract class InMemorySingletonRepositoryBase<TEntity> : IRepositoryParticipant
        where TEntity : class
    {
        private TEntity? _committedValue;

        /// <summary>
        /// 初回使用前にリポジトリを初期化します。
        /// </summary>
        /// <param name="ct">初期化を取り消せるトークン。</param>
        /// <returns>インメモリ実装用の完了済みタスク。</returns>
        public UniTask InitializeAsync(CancellationToken ct = default)
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 指定されたトランザクション内で可視な現在のエンティティ値を読み取ります。
        /// </summary>
        /// <param name="tx">読み取りに使用するトランザクション。</param>
        /// <returns>確定済みまたはステージング済みのエンティティ値。存在しない場合は <see langword="null"/>。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> が <see langword="null"/> です。</exception>
        public TEntity? Read(IReadOnlyTx tx)
        {
            if (tx is null)
            {
                throw new ArgumentNullException(nameof(tx));
            }

            if (TryGetWriteState(tx, out var writeState))
            {
                return writeState.HasValue ? writeState.Value : null;
            }

            return _committedValue;
        }

        /// <summary>
        /// 読み書きトランザクション内でシングルトン値を作成します。
        /// </summary>
        /// <param name="tx">変更をステージングするトランザクション。</param>
        /// <param name="entity">作成するエンティティ。</param>
        /// <exception cref="ArgumentNullException"><paramref name="entity"/> が <see langword="null"/> です。</exception>
        /// <exception cref="InvalidOperationException">値がすでに存在するか、トランザクションが無効です。</exception>
        public void Create(IReadWriteTx tx, TEntity entity)
        {
            if (entity is null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            var state = GetWriteState(tx);
            if (state.HasValue)
            {
                throw new InvalidOperationException($"Create failed for {GetType().Name}. A value already exists.");
            }

            state.Value = entity;
            state.HasValue = true;
        }

        /// <summary>
        /// 読み書きトランザクション内でシングルトン値を置き換えます。
        /// </summary>
        /// <param name="tx">変更をステージングするトランザクション。</param>
        /// <param name="entity">置き換え後のエンティティ。</param>
        /// <exception cref="ArgumentNullException"><paramref name="entity"/> が <see langword="null"/> です。</exception>
        /// <exception cref="InvalidOperationException">値が存在しないか、トランザクションが無効です。</exception>
        public void Update(IReadWriteTx tx, TEntity entity)
        {
            if (entity is null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            var state = GetWriteState(tx);
            if (!state.HasValue)
            {
                throw new InvalidOperationException($"Update failed for {GetType().Name}. A value does not exist.");
            }

            state.Value = entity;
            state.HasValue = true;
        }

        /// <summary>
        /// 読み書きトランザクション内でシングルトン値を削除します。
        /// </summary>
        /// <param name="tx">変更をステージングするトランザクション。</param>
        /// <exception cref="InvalidOperationException">値が存在しないか、トランザクションが無効です。</exception>
        public void Delete(IReadWriteTx tx)
        {
            var state = GetWriteState(tx);
            if (!state.HasValue)
            {
                throw new InvalidOperationException($"Delete failed for {GetType().Name}. A value does not exist.");
            }

            state.Value = null;
            state.HasValue = false;
        }

        /// <summary>
        /// 準備済みの状態が新しい確定済み値になる前に永続化します。
        /// </summary>
        /// <param name="state">これから読み取り側に見えるようになる値。</param>
        /// <param name="ct">永続化を取り消せるトークン。</param>
        /// <returns>永続化が完了したときに完了するタスク。</returns>
        protected virtual UniTask PersistStateAsync(TEntity? state, CancellationToken ct)
        {
            return UniTask.CompletedTask;
        }

        UniTask IRepositoryParticipant.PrepareCommitAsync(object transactionState, CancellationToken ct)
        {
            var state = (SingletonTransactionState)transactionState;
            state.PreparedValue = state.WriteState.HasValue ? state.WriteState.Value : null;
            return PersistStateAsync(state.PreparedValue, ct);
        }

        void IRepositoryParticipant.ApplyCommit(object transactionState)
        {
            var state = (SingletonTransactionState)transactionState;
            _committedValue = state.PreparedValue;
        }

        private RepositoryWriteState<TEntity> GetWriteState(IReadWriteTx tx)
        {
            if (tx is null)
            {
                throw new ArgumentNullException(nameof(tx));
            }

            if (tx is not RepositoryTx repositoryTx || !repositoryTx.IsReadWrite)
            {
                throw new InvalidOperationException("Writes require a transaction created by TxManager.");
            }

            return repositoryTx
                .GetOrCreateParticipantState(this, () => new SingletonTransactionState(new RepositoryWriteState<TEntity>(_committedValue, _committedValue is not null)))
                .WriteState;
        }

        private bool TryGetWriteState(IReadOnlyTx tx, out RepositoryWriteState<TEntity> writeState)
        {
            writeState = default!;

            if (tx is RepositoryTx repositoryTx &&
                repositoryTx.IsReadWrite &&
                repositoryTx.TryGetParticipantState(this, out var transactionState))
            {
                writeState = ((SingletonTransactionState)transactionState).WriteState;
                return true;
            }

            return false;
        }

        private sealed class SingletonTransactionState
        {
            public SingletonTransactionState(RepositoryWriteState<TEntity> writeState)
            {
                WriteState = writeState;
            }

            public RepositoryWriteState<TEntity> WriteState { get; }

            public TEntity? PreparedValue { get; set; }
        }
    }
}
