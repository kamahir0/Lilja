using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lilja.AssetManagement
{
    /// <summary>
    /// AssetLifetimeの拡張メソッド定義クラス
    /// </summary>
    public static class AssetLifetimeExtensions
    {
        /// <summary>
        /// GameObjectが破棄されたタイミングでAssetLifetimeをDisposeします
        /// </summary>
        /// <param name="self">AssetLifetime</param>
        /// <param name="gameObject">紐づけるGameObject</param>
        public static void LinkTo(this AssetLifetime self, GameObject gameObject)
        {
            var token = gameObject.GetCancellationTokenOnDestroy();
            var registration = token.Register(self.Dispose);
            self.Register(new CancellationTokenRegistrationHandle(registration));
        }

        /// <summary>
        /// SceneがアンロードされたタイミングでAssetLifetimeをDisposeします
        /// </summary>
        /// <param name="self">AssetLifetime</param>
        /// <param name="scene">紐づけるScene</param>
        public static void LinkTo(this AssetLifetime self, Scene scene)
        {
            var handle = new SceneUnloadHandle(scene, self);
            self.Register(handle);
        }

        private sealed class CancellationTokenRegistrationHandle : IAssetHandle
        {
            private CancellationTokenRegistration _registration;

            public CancellationTokenRegistrationHandle(CancellationTokenRegistration registration)
            {
                _registration = registration;
            }

            public void Dispose()
            {
                _registration.Dispose();
            }
        }

        private sealed class SceneUnloadHandle : IAssetHandle
        {
            private readonly Scene _scene;
            private readonly AssetLifetime _lifetime;
            private bool _disposed;

            public SceneUnloadHandle(Scene scene, AssetLifetime lifetime)
            {
                _scene = scene;
                _lifetime = lifetime;
                SceneManager.sceneUnloaded += OnSceneUnloaded;
            }

            private void OnSceneUnloaded(Scene scene)
            {
                if (_scene == scene)
                {
                    _lifetime.Dispose();
                }
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                SceneManager.sceneUnloaded -= OnSceneUnloaded;
            }
        }
    }
}
