using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lilja.ScreenManagement
{

    public interface ITransition
    {

        UniTask OutAsync(CancellationToken cancellationToken);

        UniTask InAsync(CancellationToken cancellationToken);
    }

    public interface IPrefabProvider
    {

        UniTask<GameObject> LoadAsync(string key, CancellationToken cancellationToken);

        void Unload(string key);
    }

    public interface ISceneLoader
    {

        UniTask<Scene> LoadSceneAsync(string sceneName, CancellationToken cancellationToken);

        UniTask UnloadSceneAsync(Scene scene, CancellationToken cancellationToken);
    }

    public sealed class GameScreenOptions
    {
        private IPrefabProvider _prefabProvider;
        private ISceneLoader _sceneLoader;

        public ITransition Transition { get; set; }

        public IPrefabProvider PrefabProvider
        {
            get => _prefabProvider ??= new ResourcesPrefabProvider();
            set => _prefabProvider = value;
        }

        public ISceneLoader SceneLoader
        {
            get => _sceneLoader ??= new DefaultSceneLoader();
            set => _sceneLoader = value;
        }
    }

    internal sealed class DefaultSceneLoader : ISceneLoader
    {

        public async UniTask<Scene> LoadSceneAsync(
            string sceneName,
            CancellationToken cancellationToken
        )
        {
            await SceneManager
                .LoadSceneAsync(sceneName, LoadSceneMode.Additive)
                .WithCancellation(cancellationToken);
            return SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
        }

        public async UniTask UnloadSceneAsync(Scene scene, CancellationToken cancellationToken)
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                await SceneManager.UnloadSceneAsync(scene).WithCancellation(cancellationToken);
            }
        }
    }

    internal sealed class ResourcesPrefabProvider : IPrefabProvider
    {
        private readonly Dictionary<string, GameObject> _loadedCache = new();

        public async UniTask<GameObject> LoadAsync(string key, CancellationToken cancellationToken)
        {
            if (_loadedCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var request = Resources.LoadAsync<GameObject>(key);
            await request.WithCancellation(cancellationToken);
            var prefab = request.asset as GameObject;

            if (prefab == null)
            {
                throw new FileNotFoundException($"Prefab not found in Resources at key: '{key}'");
            }

            _loadedCache[key] = prefab;
            return prefab;
        }

        public void Unload(string key)
        {
            _loadedCache.Remove(key);
        }
    }
}
