#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Lilja.Repository.Internal;

namespace Lilja.Repository
{
    /// <summary>
    /// 完全にメモリ内に保持されるキー付きリポジトリ向けの、トランザクション対応 CRUD 振る舞いを提供します。
    /// </summary>
    /// <typeparam name="TEntity">リポジトリが管理するエンティティ型。</typeparam>
    /// <typeparam name="TKey">エンティティの識別に使うキー型。</typeparam>
    public abstract class InMemoryKeyedRepositoryBase<TEntity, TKey> : IRepositoryParticipant
        where TEntity : class
        where TKey : notnull
    {
        private Dictionary<TKey, TEntity> _committedState = new Dictionary<TKey, TEntity>();

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
        /// 指定されたトランザクション内で可視なエンティティを読み取ります。
        /// </summary>
        /// <param name="tx">読み取りに使用するトランザクション。</param>
        /// <param name="key">エンティティキー。</param>
        /// <returns>確定済みまたはステージング済みのエンティティ。存在しない場合は <see langword="null"/>。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> が <see langword="null"/> です。</exception>
        public TEntity? Read(IReadOnlyTx tx, TKey key)
        {
            if (tx is null)
            {
                throw new ArgumentNullException(nameof(tx));
            }

            if (TryGetOverlay(tx, out var overlay) && overlay.TryGetValue(key, out var stagedEntity))
            {
                return stagedEntity;
            }

            return _committedState.TryGetValue(key, out var entity) ? entity : null;
        }

        /// <summary>
        /// 読み書きトランザクション内でエンティティを作成します。
        /// </summary>
        /// <param name="tx">変更をステージングするトランザクション。</param>
        /// <param name="entity">作成するエンティティ。</param>
        /// <exception cref="ArgumentNullException"><paramref name="entity"/> が <see langword="null"/> です。</exception>
        /// <exception cref="InvalidOperationException">同じキーを持つエンティティがすでに存在するか、トランザクションが無効です。</exception>
        public void Create(IReadWriteTx tx, TEntity entity)
        {
            if (entity is null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            var overlay = GetWriteOverlay(tx);
            var key = GetKey(entity);
            if (overlay.ContainsKey(key))
            {
                throw new InvalidOperationException($"Create failed for {GetType().Name}. Entity with key '{key}' already exists.");
            }

            overlay.Upsert(key, entity);
        }

        /// <summary>
        /// 読み書きトランザクション内でエンティティを更新します。
        /// </summary>
        /// <param name="tx">変更をステージングするトランザクション。</param>
        /// <param name="entity">置き換え後のエンティティ。</param>
        /// <exception cref="ArgumentNullException"><paramref name="entity"/> が <see langword="null"/> です。</exception>
        /// <exception cref="InvalidOperationException">エンティティが存在しないか、トランザクションが無効です。</exception>
        public void Update(IReadWriteTx tx, TEntity entity)
        {
            if (entity is null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            var overlay = GetWriteOverlay(tx);
            var key = GetKey(entity);
            if (!overlay.ContainsKey(key))
            {
                throw new InvalidOperationException($"Update failed for {GetType().Name}. Entity with key '{key}' does not exist.");
            }

            overlay.Upsert(key, entity);
        }

        /// <summary>
        /// 読み書きトランザクション内でエンティティを削除します。
        /// </summary>
        /// <param name="tx">変更をステージングするトランザクション。</param>
        /// <param name="key">削除するエンティティのキー。</param>
        /// <exception cref="InvalidOperationException">エンティティが存在しないか、トランザクションが無効です。</exception>
        public void Delete(IReadWriteTx tx, TKey key)
        {
            var overlay = GetWriteOverlay(tx);
            if (!overlay.ContainsKey(key))
            {
                throw new InvalidOperationException($"Delete failed for {GetType().Name}. Entity with key '{key}' does not exist.");
            }

            overlay.Delete(key);
        }

        /// <summary>
        /// 指定されたトランザクション内で可視な全エンティティのスナップショットを返します。
        /// </summary>
        /// <param name="tx">読み取りに使用するトランザクション。</param>
        /// <returns>実体化されたエンティティ一覧。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> が <see langword="null"/> です。</exception>
        public IReadOnlyList<TEntity> All(IReadOnlyTx tx)
        {
            if (tx is null)
            {
                throw new ArgumentNullException(nameof(tx));
            }

            if (TryGetOverlay(tx, out var overlay))
            {
                return new List<TEntity>(overlay.Materialize().Values);
            }

            return new List<TEntity>(_committedState.Values);
        }

        /// <summary>
        /// エンティティインスタンスからリポジトリキーを取り出します。
        /// </summary>
        /// <param name="entity">キーを返す対象のエンティティ。</param>
        /// <returns>エンティティキー。</returns>
        protected abstract TKey GetKey(TEntity entity);

        /// <summary>
        /// 準備済みの状態が新しい確定済みスナップショットになる前に永続化します。
        /// </summary>
        /// <param name="state">これから読み取り側に見えるようになる辞書。</param>
        /// <param name="ct">永続化を取り消せるトークン。</param>
        /// <returns>永続化が完了したときに完了するタスク。</returns>
        protected virtual UniTask PersistStateAsync(Dictionary<TKey, TEntity> state, CancellationToken ct)
        {
            return UniTask.CompletedTask;
        }

        UniTask IRepositoryParticipant.PrepareCommitAsync(object transactionState, CancellationToken ct)
        {
            var state = (KeyedTransactionState)transactionState;
            state.PreparedState = state.Overlay.Materialize();
            return PersistStateAsync(state.PreparedState, ct);
        }

        void IRepositoryParticipant.ApplyCommit(object transactionState)
        {
            var state = (KeyedTransactionState)transactionState;
            _committedState = state.PreparedState ?? state.Overlay.Materialize();
        }

        private RepositoryOverlayState<TKey, TEntity> GetWriteOverlay(IReadWriteTx tx)
        {
            if (tx is null)
            {
                throw new ArgumentNullException(nameof(tx));
            }

            if (tx is not RepositoryTx repositoryTx || !repositoryTx.IsReadWrite)
            {
                throw new InvalidOperationException("Writes require a transaction created by TxManager.");
            }

            return repositoryTx.GetOrCreateParticipantState(this, () => new KeyedTransactionState(new RepositoryOverlayState<TKey, TEntity>(_committedState))).Overlay;
        }

        private bool TryGetOverlay(IReadOnlyTx tx, out RepositoryOverlayState<TKey, TEntity> overlay)
        {
            overlay = default!;

            if (tx is RepositoryTx repositoryTx &&
                repositoryTx.IsReadWrite &&
                repositoryTx.TryGetParticipantState(this, out var transactionState))
            {
                overlay = ((KeyedTransactionState)transactionState).Overlay;
                return true;
            }

            return false;
        }

        private sealed class KeyedTransactionState
        {
            public KeyedTransactionState(RepositoryOverlayState<TKey, TEntity> overlay)
            {
                Overlay = overlay;
            }

            public RepositoryOverlayState<TKey, TEntity> Overlay { get; }

            public Dictionary<TKey, TEntity>? PreparedState { get; set; }
        }
    }
}
