using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Lilja.AssetManagement
{
    public sealed class ResourcesAssetLoader : IAssetLoader
    {
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
                var handle = new ResourcesHandle(asset);
                lifetime.Register(handle);
            }

            return asset;
        }

        private sealed class ResourcesHandle : IAssetHandle
        {
            private readonly Object _asset;

            public ResourcesHandle(Object asset)
            {
                _asset = asset;
            }

            public void Dispose()
            {
                if (_asset == null) return;

                // NOTE: Resources.UnloadAsset はGameObjectに対して使用不可
                if (_asset is not GameObject)
                {
                    Resources.UnloadAsset(_asset);
                }
            }
        }
    }
}
