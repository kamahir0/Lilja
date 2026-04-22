#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Lilja.Repository.Internal;

namespace Lilja.Repository
{
    /// <summary>
    /// Provides transactional CRUD behavior for a keyed repository stored entirely in memory.
    /// </summary>
    /// <typeparam name="TEntity">The entity type managed by the repository.</typeparam>
    /// <typeparam name="TKey">The key used to identify entities.</typeparam>
    public abstract class InMemoryKeyedRepositoryBase<TEntity, TKey> : IRepositoryParticipant
        where TEntity : class
        where TKey : notnull
    {
        private Dictionary<TKey, TEntity> _committedState = new Dictionary<TKey, TEntity>();

        /// <summary>
        /// Initializes the repository before first use.
        /// </summary>
        /// <param name="ct">A token that can cancel initialization.</param>
        /// <returns>A completed task for the in-memory implementation.</returns>
        public UniTask InitializeAsync(CancellationToken ct = default)
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Reads an entity visible within the supplied transaction.
        /// </summary>
        /// <param name="tx">The transaction to read through.</param>
        /// <param name="key">The entity key.</param>
        /// <returns>The committed or staged entity, or <see langword="null"/> when no entity exists.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>.</exception>
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
        /// Creates an entity within a read-write transaction.
        /// </summary>
        /// <param name="tx">The transaction that stages the change.</param>
        /// <param name="entity">The entity to create.</param>
        /// <exception cref="ArgumentNullException"><paramref name="entity"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">An entity with the same key already exists or the transaction is invalid.</exception>
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
        /// Updates an entity within a read-write transaction.
        /// </summary>
        /// <param name="tx">The transaction that stages the change.</param>
        /// <param name="entity">The replacement entity.</param>
        /// <exception cref="ArgumentNullException"><paramref name="entity"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">The entity does not exist or the transaction is invalid.</exception>
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
        /// Deletes an entity within a read-write transaction.
        /// </summary>
        /// <param name="tx">The transaction that stages the change.</param>
        /// <param name="key">The key of the entity to delete.</param>
        /// <exception cref="InvalidOperationException">The entity does not exist or the transaction is invalid.</exception>
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
        /// Returns a snapshot of all entities visible within the supplied transaction.
        /// </summary>
        /// <param name="tx">The transaction to read through.</param>
        /// <returns>A materialized list of entities.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>.</exception>
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
        /// Extracts the repository key from an entity instance.
        /// </summary>
        /// <param name="entity">The entity whose key should be returned.</param>
        /// <returns>The entity key.</returns>
        protected abstract TKey GetKey(TEntity entity);

        /// <summary>
        /// Persists the prepared state before it becomes the new committed snapshot.
        /// </summary>
        /// <param name="state">The dictionary that is about to become visible to readers.</param>
        /// <param name="ct">A token that can cancel persistence.</param>
        /// <returns>A task that completes when persistence finishes.</returns>
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
