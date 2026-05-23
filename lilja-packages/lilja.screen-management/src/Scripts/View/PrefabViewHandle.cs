using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// プレハブアセットをロードし、GameObject をインスタンス化して管理するビューハンドル。
    /// </summary>
    public sealed class PrefabViewHandle : IViewHandle
    {
        /// <summary>
        /// パス指定を省略し、画面のクラス名から自動的にプレハブパスを推論してロードするデフォルトインスタンスを取得します。
        /// </summary>
        public static PrefabViewHandle Default => new(null);

        private readonly string _specifiedKey;
        private string _resolvedKey;
        private GameObject _instance;
        private GameObject[] _rootObjects = Array.Empty<GameObject>();

        /// <inheritdoc />
        public GameObject[] RootObjects => _rootObjects;

        /// <summary>
        /// プレハブのキー（Resourcesパス等）を指定して、新しい <see cref="PrefabViewHandle"/> インスタンスを初期化します。
        /// </summary>
        /// <param name="prefabKey">プレハブのキー名（null の場合はクラス名から自動推論されます）</param>
        public PrefabViewHandle(string prefabKey)
        {
            _specifiedKey = prefabKey;
        }

        /// <inheritdoc />
        public void Initialize(Type ownerType)
        {
            if (_resolvedKey != null)
            {
                return;
            }

            // 指定キーがあればそれを使い、なければ型名から自動解決する (遅延解決)
            _resolvedKey = !string.IsNullOrEmpty(_specifiedKey)
                ? _specifiedKey
                : ResolveKeyFromType(ownerType);
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

            // 1. DIされた PrefabProvider を用いてアセットをロード (static シングルトンから完全脱却)
            var provider = context.Options.PrefabProvider;
            var prefab = await provider.LoadAsync(_resolvedKey, cancellationToken);

            if (prefab == null)
            {
                throw new FileNotFoundException(
                    $"Prefab asset could not be loaded at key: '{_resolvedKey}'"
                );
            }

            // 2. インスタンス化
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

        /// <summary>
        /// プレハブアセットを事前に非同期ロードしてキャッシュします。
        /// </summary>
        /// <param name="context">画面コンテキスト</param>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>非同期タスク</returns>
        public UniTask PreloadAsync(GameScreenContext context, CancellationToken cancellationToken)
        {
            if (_resolvedKey == null)
            {
                throw new InvalidOperationException(
                    "PrefabViewHandle has not been initialized with a type context."
                );
            }
            return context.Options.PrefabProvider.LoadAsync(_resolvedKey, cancellationToken);
        }

        private static string ResolveKeyFromType(Type ownerType)
        {
            var typeName = ownerType.Name;
            const string suffix = "Screen";

            if (typeName.EndsWith(suffix))
            {
                typeName = typeName[..^suffix.Length];
            }

            // 標準の Resources プレハブパス「Screens/{Name}」を自動構築
            return $"Screens/{typeName}";
        }
    }
}
