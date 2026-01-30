using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// Scene をビューとする Screen のための IViewHandle
    /// </summary>
    public class SceneViewHandle : IViewHandle
    {
        private readonly string _sceneName;
        private Scene _scene;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public SceneViewHandle(string sceneName)
        {
            _sceneName = sceneName;
        }

        /// <inheritdoc/>
        public GameObject[] RootObjects => _scene.GetRootGameObjects();

        /// <inheritdoc/>
        public async UniTask LoadAsync(CancellationToken cancellationToken)
        {
            await SceneManager.LoadSceneAsync(_sceneName, LoadSceneMode.Additive);
            _scene = SceneManager.GetSceneByName(_sceneName);
            SceneManager.SetActiveScene(_scene);
        }

        /// <inheritdoc/>
        public void Unload()
        {
            SceneManager.UnloadSceneAsync(_scene);
        }
    }
}
