using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// Unity の Scene 加算ロードを物理的なビューの実体として管理する、ビューハンドルクラス。
    /// </summary>
    public sealed class SceneViewHandle : IViewHandle
    {
        #region Public / Protected Members

        // --- Fields ---
        // (No public or protected fields)

        // --- Properties ---

        /// <summary>
        /// シーン名自動解決に対応した、空のデフォルトハンドルインスタンスを取得します。
        /// </summary>
        public static SceneViewHandle Default => new(null);

        // --- Constructors ---

        /// <summary>
        /// 新しい <see cref="SceneViewHandle"/> インスタンスを初期化します。
        /// </summary>
        /// <param name="sceneName">ロード対象のシーンアセット名</param>
        /// <param name="unloadsAncestors">このビューロード時に先祖画面のアンロードを要求するか</param>
        public SceneViewHandle(string sceneName, bool unloadsAncestors = true)
        {
            _specifiedSceneName = sceneName;
            _unloadsAncestors = unloadsAncestors;
        }

        // --- Methods ---
        // (No public or protected methods)

        #endregion

        #region Internal / Private Members

        // --- Fields ---
        private readonly string _specifiedSceneName;
        private readonly bool _unloadsAncestors;
        private string _resolvedSceneName;
        private Scene _loadedScene;
        private ISceneLoader _cachedLoader;
        private GameObject[] _rootObjects = Array.Empty<GameObject>();

        // --- Properties ---
        // (No internal or private properties)

        // --- Methods ---

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

        #endregion

        #region IViewHandle

        // --- Properties ---

        /// <inheritdoc />
        public GameObject[] RootObjects => _rootObjects;

        /// <inheritdoc />
        public bool IsLoaded => _loadedScene.IsValid() && _loadedScene.isLoaded;

        /// <inheritdoc />
        public bool IsUnloadedTemporarily { get; set; }

        /// <inheritdoc />
        public bool UnloadsAncestors => _unloadsAncestors;

        // --- Methods ---

        /// <inheritdoc />
        public void Initialize(Type ownerType)
        {
            if (_resolvedSceneName != null)
            {
                return;
            }

            _resolvedSceneName = !string.IsNullOrEmpty(_specifiedSceneName)
                ? _specifiedSceneName
                : ResolveSceneNameFromType(ownerType);
        }

        /// <inheritdoc />
        public UniTask PreloadAsync(GameScreenContext context, CancellationToken cancellationToken)
        {
            return UniTask.CompletedTask;
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
                    "[Lilja.ScreenManagement] SceneViewHandle が型コンテキストで初期化されていません。"
                );
            }

            _cachedLoader = context.SceneLoader;
            _loadedScene = await _cachedLoader.LoadSceneAsync(
                _resolvedSceneName,
                cancellationToken
            );

            if (!_loadedScene.IsValid())
            {
                throw new InvalidOperationException(
                    $"[Lilja.ScreenManagement] シーン '{_resolvedSceneName}' のロードに失敗しました。Build Settings にシーンが追加されているか確認してください。"
                );
            }

            _rootObjects = _loadedScene.GetRootGameObjects();

            // ロードしたシーンをアクティブシーンに設定して、ブートシーン等のアンロードを可能にする
            SceneManager.SetActiveScene(_loadedScene);
        }

        /// <inheritdoc />
        public async UniTask UnloadAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (_cachedLoader != null && _loadedScene.IsValid() && _loadedScene.isLoaded)
                {
                    // アンロード中のキャンセルによるシーン破損・メモリリークを防ぐため、
                    // CancellationToken.None を指定して処理を最後まで安全に完了させます
                    await _cachedLoader.UnloadSceneAsync(_loadedScene, CancellationToken.None);
                }
            }
            finally
            {
                _loadedScene = default;
                _cachedLoader = null;
                _rootObjects = Array.Empty<GameObject>();
            }
        }

        #endregion
    }
}
