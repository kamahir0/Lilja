using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Lilja.AssetManagement
{
    /// <summary>
    /// Resourcesからアセットを読み込むローダーの実装
    /// </summary>
    public sealed class ResourcesAssetLoader : IAssetLoader
    {
        // アセットごとの参照カウントを保持する辞書
        private static readonly Dictionary<Object, int> RefCounts = new();

        /// <inheritdoc />
        public async UniTask<T> LoadAsync<T>(string key, AssetLifetime lifetime, CancellationToken ct = default)
            where T : Object
        {
            var request = Resources.LoadAsync<T>(key);

            await request.ToUniTask(cancellationToken: ct);

            if (request.asset == null)
            {
                return null;
            }

            var asset = request.asset as T;
            if (asset != null)
            {
                // 参照カウントのトランザクション管理
                // usingスコープを抜けるまでに Commit() されなければ自動的にロールバック（デクリメント）される
                using var transaction = new RefCountTransaction(asset);

                var handle = new ResourcesHandle(asset);
                lifetime.Register(handle);

                transaction.Commit();
            }

            return asset;
        }

        private static void IncrementRefCount(Object asset)
        {
            if (asset is GameObject) return;
            lock (RefCounts)
            {
                if (!RefCounts.ContainsKey(asset))
                {
                    RefCounts[asset] = 0;
                }

                RefCounts[asset]++;
            }
        }

        private static void DecrementRefCount(Object asset)
        {
            if (asset is GameObject) return;
            lock (RefCounts)
            {
                if (RefCounts.ContainsKey(asset))
                {
                    RefCounts[asset]--;

                    if (RefCounts[asset] <= 0)
                    {
                        RefCounts.Remove(asset);
                        Resources.UnloadAsset(asset);
                    }
                }
            }
        }

        /// <summary>
        /// 参照カウントの増加と、失敗時のロールバックを管理する構造体
        /// </summary>
        private struct RefCountTransaction : IDisposable
        {
            private readonly Object _asset;
            private bool _committed;

            public RefCountTransaction(Object asset)
            {
                _asset = asset;
                _committed = false;
                IncrementRefCount(_asset);
            }

            public void Commit()
            {
                _committed = true;
            }

            public void Dispose()
            {
                if (!_committed)
                {
                    DecrementRefCount(_asset);
                }
            }
        }

        private sealed class ResourcesHandle : IAssetHandle
        {
            private readonly Object _asset;
            private bool _isDisposed;

            public ResourcesHandle(Object asset)
            {
                _asset = asset;
            }

            public void Dispose()
            {
                if (_isDisposed) return;
                if (_asset == null) return;

                _isDisposed = true;
                DecrementRefCount(_asset);
            }
        }
    }
}
