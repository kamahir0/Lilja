using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// Prefab をビューとする Screen のための IViewHandle
    /// </summary>
    public class PrefabViewHandle : IViewHandle
    {
        private readonly IPrefabHandle _prefabHandle;
        private GameObject _instance;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public PrefabViewHandle(IPrefabHandle prefabHandle)
        {
            _prefabHandle = prefabHandle;
        }

        /// <summary>
        /// Prefab アセットを事前ロードします
        /// </summary>
        public async UniTask PreloadAsync(CancellationToken cancellationToken)
        {
            await _prefabHandle.LoadAsync(cancellationToken);
        }

        #region IViewHandle

        /// <inheritdoc/>
        public GameObject[] RootObjects => new[] { _instance };

        /// <inheritdoc/>
        public async UniTask LoadAsync(CancellationToken cancellationToken)
        {
            // すでにインスタンス化されている場合は何もしない
            if (_instance != null) return;

            var prefab = await _prefabHandle.LoadAsync(cancellationToken);
            if (prefab == null)
            {
                throw new Exception($"Prefab not found at address: {_prefabHandle}");
            }

            _instance = Object.Instantiate(prefab);

            var prefabOverlayScene = PrefabOverlaySceneUtility.GetOrCreate();
            SceneManager.MoveGameObjectToScene(_instance, prefabOverlayScene);
        }

        /// <inheritdoc/>
        public void Unload()
        {
            Object.Destroy(_instance);
            _instance = null;
            _prefabHandle.Release();
        }

        #endregion
    }
}
