using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// Unityの加算シーンロードを非同期で実行し、シーン内のルートオブジェクトを抽出して管理するビューハンドル。
    /// </summary>
    public sealed class SceneViewHandle : IViewHandle
    {
        /// <summary>
        /// シーン名指定を省略し、画面のクラス名から自動的にシーン名を推論してロードするデフォルトインスタンスを取得します。
        /// </summary>
        public static SceneViewHandle Default => new SceneViewHandle(null);

        private readonly string _specifiedSceneName;
        private string _resolvedSceneName;
        private Scene _loadedScene;
        private ISceneLoader _cachedLoader;
        private GameObject[] _rootObjects = Array.Empty<GameObject>();

        /// <inheritdoc />
        public GameObject[] RootObjects => _rootObjects;

        /// <summary>
        /// 加算ロードするシーン名を明示的に指定して、新しい <see cref="SceneViewHandle"/> インスタンスを初期化します。
        /// </summary>
        /// <param name="sceneName">シーン名（null の場合はクラス名から自動推論されます）</param>
        public SceneViewHandle(string sceneName)
        {
            _specifiedSceneName = sceneName;
        }

        /// <inheritdoc />
        public void Initialize(Type ownerType)
        {
            if (_resolvedSceneName != null)
            {
                return;
            }

            // 指定されたシーン名があればそれを使い、なければ型名から自動解決する (遅延解決)
            _resolvedSceneName = !string.IsNullOrEmpty(_specifiedSceneName)
                ? _specifiedSceneName
                : ResolveSceneNameFromType(ownerType);
        }

        /// <inheritdoc />
        public async UniTask LoadAsync(
            GameScreenContext context,
            CancellationToken cancellationToken
        )
        {
            if (_loadedScene.IsValid() && _loadedScene.isLoaded)
            {
                return;
            }

            if (_resolvedSceneName == null)
            {
                throw new InvalidOperationException(
                    "SceneViewHandle has not been initialized with a type context."
                );
            }

            // DIされた SceneLoader をキャッシュして実行 (static シングルトンから完全脱却)
            _cachedLoader = context.Options.SceneLoader;
            _loadedScene = await _cachedLoader.LoadSceneAsync(
                _resolvedSceneName,
                cancellationToken
            );

            if (!_loadedScene.IsValid())
            {
                throw new InvalidOperationException(
                    $"Failed to load scene: '{_resolvedSceneName}'"
                );
            }

            // 加算ロードされたシーンのルートオブジェクト群を抽出して格納
            _rootObjects = _loadedScene.GetRootGameObjects();
        }

        /// <inheritdoc />
        public void Unload()
        {
            if (_cachedLoader != null && _loadedScene.IsValid() && _loadedScene.isLoaded)
            {
                // アンロード演出をバックグラウンドで開始
                _cachedLoader.UnloadSceneAsync(_loadedScene, CancellationToken.None).Forget();
            }

            _loadedScene = default;
            _cachedLoader = null;
            _rootObjects = Array.Empty<GameObject>();
        }

        private static string ResolveSceneNameFromType(Type ownerType)
        {
            var typeName = ownerType.Name;
            const string suffix = "Screen";

            if (typeName.EndsWith(suffix))
            {
                typeName = typeName[..^suffix.Length];
            }

            return typeName;
        }
    }
}
