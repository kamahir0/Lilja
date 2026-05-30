using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// Prefab アセットを物理的なビューの実体としてロード・インスタンス化する、デフォルトのビューハンドルクラス。
    /// </summary>
    public sealed class PrefabViewHandle : IViewHandle
    {
        /// <summary>
        /// プレハブキー自動解決に対応した、空のデフォルトハンドルインスタンスを取得します。
        /// </summary>
        public static PrefabViewHandle Default => new(null);

        private readonly string _specifiedKey;
        private readonly bool _unloadsAncestors;
        private string _resolvedKey;
        private GameObject _instance;
        private GameObject[] _rootObjects = Array.Empty<GameObject>();

        /// <summary>
        /// 新しい <see cref="PrefabViewHandle"/> インスタンスを初期化します。
        /// </summary>
        /// <param name="prefabKey">プレハブアセットを特定するキー名</param>
        /// <param name="unloadsAncestors">このビューロード時に先祖画面のアンロードを要求するか</param>
        public PrefabViewHandle(string prefabKey, bool unloadsAncestors = false)
        {
            _specifiedKey = prefabKey;
            _unloadsAncestors = unloadsAncestors;
        }

        private static string ResolveKeyFromType(Type ownerType)
        {
            var typeName = ownerType.Name;
            const string suffix = "Screen";

            if (typeName.EndsWith(suffix))
            {
                typeName = typeName[..^suffix.Length];
            }

            return $"Screens/{typeName}";
        }

        #region IViewHandle

        /// <inheritdoc />
        public GameObject[] RootObjects => _rootObjects;

        /// <inheritdoc />
        public bool IsLoaded => _instance != null;

        /// <inheritdoc />
        public bool IsUnloadedTemporarily { get; set; }

        /// <inheritdoc />
        public bool UnloadsAncestors => _unloadsAncestors;

        /// <inheritdoc />
        public void Initialize(Type ownerType)
        {
            if (_resolvedKey != null)
            {
                return;
            }

            _resolvedKey = !string.IsNullOrEmpty(_specifiedKey)
                ? _specifiedKey
                : ResolveKeyFromType(ownerType);
        }

        /// <inheritdoc />
        public async UniTask PreloadAsync(
            GameScreenContext context,
            CancellationToken cancellationToken
        )
        {
            if (_resolvedKey == null)
            {
                throw new InvalidOperationException(
                    "[Lilja.ScreenManagement] PrefabViewHandle が型コンテキストで初期化されていません。"
                );
            }
            await context.PrefabProvider.LoadAsync(_resolvedKey, cancellationToken);
        }

        /// <inheritdoc />
        public async UniTask LoadAsync(
            GameScreenContext context,
            CancellationToken cancellationToken
        )
        {
            if (_instance != null)
            {
                return;
            }

            if (_resolvedKey == null)
            {
                throw new InvalidOperationException(
                    "[Lilja.ScreenManagement] PrefabViewHandle が型コンテキストで初期化されていません。"
                );
            }

            var provider = context.PrefabProvider;
            var prefab = await provider.LoadAsync(_resolvedKey, cancellationToken);

            if (prefab == null)
            {
                throw new FileNotFoundException(
                    $"[Lilja.ScreenManagement] キー '{_resolvedKey}' のプレハブアセットをロードできませんでした。アセットが Resources または Addressables に存在することを確認してください。"
                );
            }

            _instance = UnityEngine.Object.Instantiate(prefab);
            _rootObjects = new[] { _instance };

            var targetScene = await GameScreenSceneUtility.GetOrCreateSceneAsync(cancellationToken);
            if (targetScene.IsValid() && targetScene.isLoaded && _instance != null)
            {
                SceneManager.MoveGameObjectToScene(_instance, targetScene);
            }
        }

        /// <inheritdoc />
        public UniTask UnloadAsync(CancellationToken cancellationToken)
        {
            if (_instance != null)
            {
                UnityEngine.Object.Destroy(_instance);
                _instance = null;
            }
            _rootObjects = Array.Empty<GameObject>();
            return UniTask.CompletedTask;
        }

        #endregion
    }
}
