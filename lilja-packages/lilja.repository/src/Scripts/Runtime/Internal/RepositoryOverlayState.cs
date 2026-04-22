#nullable enable
using System.Collections.Generic;

namespace Lilja.Repository.Internal
{
    /// <summary>
    /// 確定済みのキー付き状態スナップショットの上に、ステージングされた追加・更新・削除を追跡します。
    /// </summary>
    /// <typeparam name="TKey">項目の識別に使うキー型。</typeparam>
    /// <typeparam name="TValue">リポジトリに格納される値の型。</typeparam>
    internal sealed class RepositoryOverlayState<TKey, TValue>
        where TKey : notnull
    {
        private readonly Dictionary<TKey, TValue> _committedState;
        private readonly Dictionary<TKey, TValue> _upserts;
        private readonly HashSet<TKey> _deletedKeys;

        /// <summary>
        /// <see cref="RepositoryOverlayState{TKey, TValue}"/> クラスの新しいインスタンスを初期化します。
        /// </summary>
        /// <param name="committedState">ステージングされた変更を重ね合わせる確定済み状態。</param>
        public RepositoryOverlayState(Dictionary<TKey, TValue> committedState)
        {
            _committedState = committedState;
            _upserts = new Dictionary<TKey, TValue>();
            _deletedKeys = new HashSet<TKey>();
        }

        /// <summary>
        /// オーバーレイが現在、指定されたキーに対する値を公開しているかどうかを判定します。
        /// </summary>
        /// <param name="key">確認するキー。</param>
        /// <returns>値が可視であれば <see langword="true"/>、それ以外は <see langword="false"/>。</returns>
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
        /// ステージングされた変更を考慮して値の読み取りを試みます。
        /// </summary>
        /// <param name="key">確認するキー。</param>
        /// <param name="value">存在する場合の可視な値。</param>
        /// <returns>値が可視であれば <see langword="true"/>、それ以外は <see langword="false"/>。</returns>
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
        /// 指定されたキーに対する追加または更新をステージングします。
        /// </summary>
        /// <param name="key">書き込むキー。</param>
        /// <param name="value">ステージングする値。</param>
        public void Upsert(TKey key, TValue value)
        {
            _deletedKeys.Remove(key);
            _upserts[key] = value;
        }

        /// <summary>
        /// 指定されたキーの削除をステージングします。
        /// </summary>
        /// <param name="key">削除するキー。</param>
        public void Delete(TKey key)
        {
            _upserts.Remove(key);
            _deletedKeys.Add(key);
        }

        /// <summary>
        /// ステージングされた変更を適用したあとの可視な値の件数を計算します。
        /// </summary>
        /// <returns>可視な項目数。</returns>
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
        /// 確定済み状態とすべてのステージング変更を結合した新しい辞書を作成します。
        /// </summary>
        /// <returns>現在のオーバーレイを実体化したスナップショット。</returns>
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
