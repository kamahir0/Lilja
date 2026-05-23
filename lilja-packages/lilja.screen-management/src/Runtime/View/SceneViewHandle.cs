using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lilja.ScreenManagement
{

    public sealed class SceneViewHandle : IViewHandle
    {

        public static SceneViewHandle Default => new(null);

        private readonly string _specifiedSceneName;
        private readonly bool _unloadsAncestors;
        private string _resolvedSceneName;
        private Scene _loadedScene;
        private ISceneLoader _cachedLoader;
        private GameObject[] _rootObjects = Array.Empty<GameObject>();

        public GameObject[] RootObjects => _rootObjects;

        public bool IsLoaded => _loadedScene.IsValid() && _loadedScene.isLoaded;

        public bool IsUnloadedTemporarily { get; set; }

        public bool UnloadsAncestors => _unloadsAncestors;

        public SceneViewHandle(string sceneName, bool unloadsAncestors = true)
        {
            _specifiedSceneName = sceneName;
            _unloadsAncestors = unloadsAncestors;
        }

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

            _rootObjects = _loadedScene.GetRootGameObjects();
        }

        public void Unload()
        {
            if (_cachedLoader != null && _loadedScene.IsValid() && _loadedScene.isLoaded)
            {

                _cachedLoader.UnloadSceneAsync(_loadedScene, CancellationToken.None).Forget();
            }

            _loadedScene = default;
            _cachedLoader = null;
            _rootObjects = Array.Empty<GameObject>();
        }

        public UniTask PreloadAsync(GameScreenContext context, CancellationToken cancellationToken)
        {

            return UniTask.CompletedTask;
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
