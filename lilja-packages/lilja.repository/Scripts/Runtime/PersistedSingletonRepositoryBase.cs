#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Lilja.Repository.Internal;

namespace Lilja.Repository
{
    /// <summary>
    /// 永続化された DTO ペイロードをバックエンドに持つシングルトンリポジトリ向けの、トランザクション対応 CRUD 振る舞いを提供します。
    /// </summary>
    /// <typeparam name="TEntity">リポジトリが管理するエンティティ型。</typeparam>
    /// <typeparam name="TDto">保存先への書き込みと読み込みに使う DTO 型。</typeparam>
    public abstract class PersistedSingletonRepositoryBase<TEntity, TDto> : IRepositoryParticipant
        where TEntity : class
        where TDto : class
    {
        private readonly SemaphoreSlim _initializationGate = new SemaphoreSlim(1, 1);
        private TEntity? _committedValue;
        private bool _initialized;

        /// <summary>
        /// <see cref="PersistedSingletonRepositoryBase{TEntity, TDto}"/> クラスの新しいインスタンスを初期化します。
        /// </summary>
        /// <param name="filePath">永続化に使うファイルパス。</param>
        /// <exception cref="ArgumentException"><paramref name="filePath"/> が空白です。</exception>
        protected PersistedSingletonRepositoryBase(string filePath)
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
                var value = await LoadValueAsync(ct);
                _committedValue = value is null ? null : FromDto(value);
                _initialized = true;
            }
            finally
            {
                _initializationGate.Release();
            }
        }

        /// <summary>
        /// 指定されたトランザクション内で可視な現在のエンティティ値を読み取ります。
        /// </summary>
        /// <param name="tx">読み取りに使用するトランザクション。</param>
        /// <returns>確定済みまたはステージング済みのエンティティ値。存在しない場合は <see langword="null"/>。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> が <see langword="null"/> です。</exception>
        /// <exception cref="InvalidOperationException">リポジトリが初期化されていません。</exception>
        public TEntity? Read(IReadOnlyTx tx)
        {
            if (tx is null)
            {
                throw new ArgumentNullException(nameof(tx));
            }

            EnsureInitialized();

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
        /// <exception cref="InvalidOperationException">リポジトリが初期化されていないか、値がすでに存在するか、トランザクションが無効です。</exception>
        public void Create(IReadWriteTx tx, TEntity entity)
        {
            if (entity is null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            EnsureInitialized();
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
        /// <exception cref="InvalidOperationException">リポジトリが初期化されていないか、値が存在しないか、トランザクションが無効です。</exception>
        public void Update(IReadWriteTx tx, TEntity entity)
        {
            if (entity is null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            EnsureInitialized();
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
        /// <exception cref="InvalidOperationException">リポジトリが初期化されていないか、値が存在しないか、トランザクションが無効です。</exception>
        public void Delete(IReadWriteTx tx)
        {
            EnsureInitialized();
            var state = GetWriteState(tx);
            if (!state.HasValue)
            {
                throw new InvalidOperationException($"Delete failed for {GetType().Name}. A value does not exist.");
            }

            state.Value = null;
            state.HasValue = false;
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
        /// 保存先から永続化された DTO を読み込みます。
        /// </summary>
        /// <param name="ct">読み込みを取り消せるトークン。</param>
        /// <returns>保存された DTO。値が存在しない場合は <see langword="null"/>。</returns>
        protected abstract UniTask<TDto?> LoadValueAsync(CancellationToken ct);

        /// <summary>
        /// コミット中に準備済み DTO を保存先へ保存します。
        /// </summary>
        /// <param name="value">永続化する DTO。値を消去する場合は <see langword="null"/>。</param>
        /// <param name="ct">保存を取り消せるトークン。</param>
        /// <returns>永続化が完了したときに完了するタスク。</returns>
        protected abstract UniTask SaveValueAsync(TDto? value, CancellationToken ct);

        UniTask IRepositoryParticipant.PrepareCommitAsync(object transactionState, CancellationToken ct)
        {
            var state = (SingletonTransactionState)transactionState;
            state.PreparedValue = state.WriteState.HasValue ? state.WriteState.Value : null;
            state.PreparedDto = state.PreparedValue is null ? null : ToDto(state.PreparedValue);
            return SaveValueAsync(state.PreparedDto, ct);
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

        private void EnsureInitialized()
        {
            if (!_initialized)
            {
                throw new InvalidOperationException($"{GetType().Name} has not been initialized. Call InitializeAsync before use.");
            }
        }

        private sealed class SingletonTransactionState
        {
            public SingletonTransactionState(RepositoryWriteState<TEntity> writeState)
            {
                WriteState = writeState;
            }

            public RepositoryWriteState<TEntity> WriteState { get; }

            public TEntity? PreparedValue { get; set; }

            public TDto? PreparedDto { get; set; }
        }
    }
}
