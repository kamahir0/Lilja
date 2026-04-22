#nullable enable
using System.Collections.Generic;

namespace Lilja.Repository.Internal
{
    /// <summary>
    /// Tracks staged inserts, updates, and deletes on top of a committed keyed state snapshot.
    /// </summary>
    /// <typeparam name="TKey">The key type used to identify entries.</typeparam>
    /// <typeparam name="TValue">The value type stored in the repository.</typeparam>
    internal sealed class RepositoryOverlayState<TKey, TValue>
        where TKey : notnull
    {
        private readonly Dictionary<TKey, TValue> _committedState;
        private readonly Dictionary<TKey, TValue> _upserts;
        private readonly HashSet<TKey> _deletedKeys;

        /// <summary>
        /// Initializes a new instance of the <see cref="RepositoryOverlayState{TKey, TValue}"/> class.
        /// </summary>
        /// <param name="committedState">The committed state that staged changes overlay.</param>
        public RepositoryOverlayState(Dictionary<TKey, TValue> committedState)
        {
            _committedState = committedState;
            _upserts = new Dictionary<TKey, TValue>();
            _deletedKeys = new HashSet<TKey>();
        }

        /// <summary>
        /// Determines whether the overlay currently exposes a value for the supplied key.
        /// </summary>
        /// <param name="key">The key to inspect.</param>
        /// <returns><see langword="true"/> when a value is visible; otherwise <see langword="false"/>.</returns>
        public bool ContainsKey(TKey key)
        {
            if (_upserts.ContainsKey(key))
            {
                return true;
            }

            if (_deletedKeys.Contains(key))
            {
                return false;
            }

            return _committedState.ContainsKey(key);
        }

        /// <summary>
        /// Attempts to read a value taking staged changes into account.
        /// </summary>
        /// <param name="key">The key to inspect.</param>
        /// <param name="value">The visible value when one exists.</param>
        /// <returns><see langword="true"/> when a value is visible; otherwise <see langword="false"/>.</returns>
        public bool TryGetValue(TKey key, out TValue value)
        {
            if (_upserts.TryGetValue(key, out value!))
            {
                return true;
            }

            if (_deletedKeys.Contains(key))
            {
                value = default!;
                return false;
            }

            return _committedState.TryGetValue(key, out value!);
        }

        /// <summary>
        /// Stages an insert or update for the supplied key.
        /// </summary>
        /// <param name="key">The key to write.</param>
        /// <param name="value">The value to stage.</param>
        public void Upsert(TKey key, TValue value)
        {
            _deletedKeys.Remove(key);
            _upserts[key] = value;
        }

        /// <summary>
        /// Stages deletion of the supplied key.
        /// </summary>
        /// <param name="key">The key to delete.</param>
        public void Delete(TKey key)
        {
            _upserts.Remove(key);
            _deletedKeys.Add(key);
        }

        /// <summary>
        /// Calculates how many values are visible after applying staged changes.
        /// </summary>
        /// <returns>The visible item count.</returns>
        public int Count()
        {
            var count = _committedState.Count;
            foreach (var deletedKey in _deletedKeys)
            {
                if (_committedState.ContainsKey(deletedKey))
                {
                    count--;
                }
            }

            foreach (var pair in _upserts)
            {
                if (!_committedState.ContainsKey(pair.Key))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Creates a new dictionary that combines committed state with all staged changes.
        /// </summary>
        /// <returns>A materialized snapshot of the current overlay.</returns>
        public Dictionary<TKey, TValue> Materialize()
        {
            var materialized = new Dictionary<TKey, TValue>(_committedState);

            foreach (var deletedKey in _deletedKeys)
            {
                materialized.Remove(deletedKey);
            }

            foreach (var pair in _upserts)
            {
                materialized[pair.Key] = pair.Value;
            }

            return materialized;
        }
    }
}
