using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lilja.ScreenManagement
{

    internal static class TempSceneUtility
    {
        private const string SceneName = "TempScene";

        internal static bool Exists()
        {
            var scene = SceneManager.GetSceneByName(SceneName);
            return scene.IsValid() && scene.isLoaded;
        }

        internal static Scene Create()
        {
            var scene = SceneManager.GetSceneByName(SceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                return scene;
            }
            return SceneManager.CreateScene(SceneName);
        }

        internal static void Destroy()
        {
            var scene = SceneManager.GetSceneByName(SceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.UnloadSceneAsync(scene);
            }
        }

        internal static TempSceneScope CreateTempSceneScope()
        {
            return new TempSceneScope(Create());
        }

        internal struct TempSceneScope : IDisposable
        {
            private Scene _scene;

            internal TempSceneScope(Scene scene)
            {
                _scene = scene;
            }

            public void Dispose()
            {
                if (_scene.IsValid() && _scene.isLoaded)
                {
                    SceneManager.UnloadSceneAsync(_scene);
                }
            }
        }
    }
}
