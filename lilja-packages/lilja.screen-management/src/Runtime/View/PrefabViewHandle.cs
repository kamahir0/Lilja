using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

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
                    "PrefabViewHandle has not been initialized with a type context."
                );
            }
            await context.Options.PrefabProvider.LoadAsync(_resolvedKey, cancellationToken);
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
                    "PrefabViewHandle has not been initialized with a type context."
                );
            }

            var provider = context.Options.PrefabProvider;
            var prefab = await provider.LoadAsync(_resolvedKey, cancellationToken);

            if (prefab == null)
            {
                throw new FileNotFoundException(
                    $"Prefab asset could not be loaded at key: '{_resolvedKey}'"
                );
            }

            _instance = UnityEngine.Object.Instantiate(prefab);
            _rootObjects = new[] { _instance };
        }

        /// <inheritdoc />
        public void Unload()
        {
            if (_instance != null)
            {
                UnityEngine.Object.Destroy(_instance);
                _instance = null;
            }
            _rootObjects = Array.Empty<GameObject>();
        }

        #endregion
    }
}
