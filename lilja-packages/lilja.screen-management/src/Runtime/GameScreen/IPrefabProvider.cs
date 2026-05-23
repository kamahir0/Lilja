using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
#if LILJA_SCREEN_MANAGEMENT_ADDRESSABLES_SUPPORT
using UnityEngine.AddressableAssets;
#endif

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// プレハブアセットのロード・アンロードを抽象化するインターフェース。
    /// </summary>
    public interface IPrefabProvider
    {
        /// <summary>
        /// 指定されたキーのプレハブを非同期でロードします。
        /// </summary>
        /// <param name="key">アセットのキー</param>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>ロードされたプレハブオブジェクト</returns>
        UniTask<GameObject> LoadAsync(string key, CancellationToken cancellationToken);

        /// <summary>
        /// 指定されたキーのプレハブアセットをアンロードします。
        /// </summary>
        /// <param name="key">対象のキー名</param>
        void Unload(string key);
    }

    /// <summary>
    /// Unity標準の Resources API を使用してプレハブをロード・アンロードするデフォルトのプロバイダー。
    /// </summary>
    internal sealed class ResourcesPrefabProvider : IPrefabProvider
    {
        private readonly Dictionary<string, GameObject> _loadedCache = new();

        #region IPrefabProvider

        /// <inheritdoc />
        public async UniTask<GameObject> LoadAsync(string key, CancellationToken cancellationToken)
        {
            if (_loadedCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var request = Resources.LoadAsync<GameObject>(key);
            await request.WithCancellation(cancellationToken);
            var prefab = request.asset as GameObject;

            if (prefab == null)
            {
                throw new FileNotFoundException($"Prefab not found in Resources at key: '{key}'");
            }

            _loadedCache[key] = prefab;
            return prefab;
        }

        /// <inheritdoc />
        public void Unload(string key)
        {
            _loadedCache.Remove(key);
        }

        #endregion
    }

#if LILJA_SCREEN_MANAGEMENT_ADDRESSABLES_SUPPORT
    /// <summary>
    /// Addressables アセットシステムを使用してプレハブをロード・アンロードするプロバイダー。
    /// </summary>
    public sealed class AddressablePrefabProvider : IPrefabProvider
    {
        private readonly Dictionary<string, GameObject> _loadedCache = new();

        #region IPrefabProvider

        /// <inheritdoc />
        public async UniTask<GameObject> LoadAsync(string key, CancellationToken cancellationToken)
        {
            if (_loadedCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var op = Addressables.LoadAssetAsync<GameObject>(key);
            var prefab = await op.WithCancellation(cancellationToken);

            if (prefab == null)
            {
                throw new FileNotFoundException($"Prefab not found in Addressables at key: '{key}'");
            }

            _loadedCache[key] = prefab;
            return prefab;
        }

        /// <inheritdoc />
        public void Unload(string key)
        {
            if (_loadedCache.TryGetValue(key, out var prefab))
            {
                _loadedCache.Remove(key);
                Addressables.Release(prefab);
            }
        }

        #endregion
    }
#endif
}
