#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Lilja.Repository.Internal;

namespace Lilja.Repository
{
    /// <summary>
    /// 永続化された DTO ペイロードをバックエンドに持つ、キー付きリポジトリ向けのトランザクション対応 CRUD 振る舞いを提供します。
    /// </summary>
    /// <typeparam name="TEntity">リポジトリが管理するエンティティ型。</typeparam>
    /// <typeparam name="TKey">エンティティの識別に使うキー型。</typeparam>
    /// <typeparam name="TDto">保存先への書き込みと読み込みに使う DTO 型。</typeparam>
    public abstract class PersistedKeyedRepositoryBase<TEntity, TKey, TDto> : IRepositoryParticipant
        where TEntity : class
        where TKey : notnull
        where TDto : class
    {
        private readonly SemaphoreSlim _initializationGate = new SemaphoreSlim(1, 1);
        private Dictionary<TKey, TEntity> _committedState = new Dictionary<TKey, TEntity>();
        private bool _initialized;

        /// <summary>
        /// <see cref="PersistedKeyedRepositoryBase{TEntity, TKey, TDto}"/> クラスの新しいインスタンスを初期化します。
        /// </summary>
        /// <param name="filePath">永続化に使うファイルパス。</param>
        /// <exception cref="ArgumentException"><paramref name="filePath"/> が空白です。</exception>
        protected PersistedKeyedRepositoryBase(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path must not be null, empty, or whitespace.", nameof(filePath));
            }

            FilePath = filePath;
            RuntimeInstanceMonitor.TrackPersistedRepository(GetType(), filePath, this);
        }

        /// <summary>
        /// リポジトリバックエンドで使用するファイルパスを取得します。
        /// </summary>
        protected string FilePath { get; }

        /// <summary>
        /// リポジトリを使用する前に、永続化された状態をメモリへ読み込みます。
        /// </summary>
        /// <param name="ct">初期化を取り消せるトークン。</param>
        /// <returns>初回読み込みが完了したときに完了するタスク。</returns>
        public async UniTask InitializeAsync(CancellationToken ct = default)
        {
            if (_initialized)
            {
                return;
            }

            await _initializationGate.WaitAsync(ct);
            try
            {
                if (_initialized)
                {
                    return;
                }

                ct.ThrowIfCancellationRequested();
                var items = await LoadItemsAsync(ct);
                var loaded = new Dictionary<TKey, TEntity>();

                if (items is not null)
                {
                    foreach (var dto in items)
                    {
                        if (dto is null)
                        {
                            continue;
                        }

                        loaded[GetKeyFromDto(dto)] = FromDto(dto);
                    }
                }

                _committedState = loaded;
                _initialized = true;
            }
            finally
            {
                _initializationGate.Release();
            }
        }

        /// <summary>
        /// 指定されたトランザクション内で可視なエンティティを読み取ります。
        /// </summary>
        /// <param name="tx">読み取りに使用するトランザクション。</param>
        /// <param name="key">エンティティキー。</param>
        /// <returns>確定済みまたはステージング済みのエンティティ。存在しない場合は <see langword="null"/>。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> が <see langword="null"/> です。</exception>
        /// <exception cref="InvalidOperationException">リポジトリが初期化されていません。</exception>
        public TEntity? Read(IReadOnlyTx tx, TKey key)
        {
            if (tx is null)
            {
                throw new ArgumentNullException(nameof(tx));
            }

            EnsureInitialized();

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
        /// <exception cref="InvalidOperationException">リポジトリが初期化されていないか、同じキーを持つエンティティがすでに存在するか、トランザクションが無効です。</exception>
        public void Create(IReadWriteTx tx, TEntity entity)
        {
            if (entity is null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            EnsureInitialized();
            var overlay = GetWriteOverlay(tx);
            var key = GetKeyFromEntity(entity);
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
        /// <exception cref="InvalidOperationException">リポジトリが初期化されていないか、エンティティが存在しないか、トランザクションが無効です。</exception>
        public void Update(IReadWriteTx tx, TEntity entity)
        {
            if (entity is null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            EnsureInitialized();
            var overlay = GetWriteOverlay(tx);
            var key = GetKeyFromEntity(entity);
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
        /// <exception cref="InvalidOperationException">リポジトリが初期化されていないか、エンティティが存在しないか、トランザクションが無効です。</exception>
        public void Delete(IReadWriteTx tx, TKey key)
        {
            EnsureInitialized();
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
        /// <exception cref="InvalidOperationException">リポジトリが初期化されていません。</exception>
        public IReadOnlyList<TEntity> All(IReadOnlyTx tx)
        {
            if (tx is null)
            {
                throw new ArgumentNullException(nameof(tx));
            }

            EnsureInitialized();

            if (TryGetOverlay(tx, out var overlay))
            {
                return new List<TEntity>(overlay.Materialize().Values);
            }

            return new List<TEntity>(_committedState.Values);
        }

        /// <summary>
        /// エンティティインスタンスを、このリポジトリが永続化する DTO へ変換します。
        /// </summary>
        /// <param name="entity">変換するエンティティ。</param>
        /// <returns>DTO 表現。</returns>
        protected abstract TDto ToDto(TEntity entity);

        /// <summary>
        /// 永続化された DTO 表現からエンティティインスタンスを再構築します。
        /// </summary>
        /// <param name="dto">変換する DTO。</param>
        /// <returns>再構築されたエンティティ。</returns>
        protected abstract TEntity FromDto(TDto dto);

        /// <summary>
        /// 永続化された DTO からリポジトリキーを取り出します。
        /// </summary>
        /// <param name="dto">キーを返す対象の DTO。</param>
        /// <returns>エンティティキー。</returns>
        protected abstract TKey GetKeyFromDto(TDto dto);

        /// <summary>
        /// 保存先から、永続化されたすべての DTO を読み込みます。
        /// </summary>
        /// <param name="ct">読み込みを取り消せるトークン。</param>
        /// <returns>保存された DTO 一覧。状態が存在しない場合は <see langword="null"/>。</returns>
        protected abstract UniTask<IReadOnlyList<TDto>?> LoadItemsAsync(CancellationToken ct);

        /// <summary>
        /// コミット中に準備済み DTO スナップショットを保存先へ保存します。
        /// </summary>
        /// <param name="items">永続化する DTO 一覧。</param>
        /// <param name="ct">保存を取り消せるトークン。</param>
        /// <returns>永続化が完了したときに完了するタスク。</returns>
        protected abstract UniTask SaveItemsAsync(IReadOnlyList<TDto> items, CancellationToken ct);

        UniTask IRepositoryParticipant.PrepareCommitAsync(object transactionState, CancellationToken ct)
        {
            var state = (KeyedTransactionState)transactionState;
            state.PreparedState = state.Overlay.Materialize();
            state.PreparedItems = ToDtoList(state.PreparedState);
            return SaveItemsAsync(state.PreparedItems, ct);
        }

        void IRepositoryParticipant.ApplyCommit(object transactionState)
        {
            var state = (KeyedTransactionState)transactionState;
            _committedState = state.PreparedState ?? state.Overlay.Materialize();
        }

        private TKey GetKeyFromEntity(TEntity entity)
        {
            return GetKeyFromDto(ToDto(entity));
        }

        private List<TDto> ToDtoList(Dictionary<TKey, TEntity> state)
        {
            var items = new List<TDto>(state.Count);
            foreach (var value in state.Values)
            {
                items.Add(ToDto(value));
            }

            return items;
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

        private void EnsureInitialized()
        {
            if (!_initialized)
            {
                throw new InvalidOperationException($"{GetType().Name} has not been initialized. Call InitializeAsync before use.");
            }
        }

        private sealed class KeyedTransactionState
        {
            public KeyedTransactionState(RepositoryOverlayState<TKey, TEntity> overlay)
            {
                Overlay = overlay;
            }

            public RepositoryOverlayState<TKey, TEntity> Overlay { get; }

            public Dictionary<TKey, TEntity>? PreparedState { get; set; }

            public IReadOnlyList<TDto>? PreparedItems { get; set; }
        }
    }
}
