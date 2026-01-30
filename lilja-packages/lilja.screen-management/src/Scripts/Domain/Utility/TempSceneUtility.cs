using System;
using UnityEngine.SceneManagement;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// シーン0状態を防ぐための一時シーンのユーティリティ
    /// </summary>
    public static class TempSceneUtility
    {
        private const string SceneName = "TempScene";

        /// <summary> 一時シーンを作成します </summary>
        public static Scene Create()
        {
            return SceneManager.CreateScene(SceneName);
        }

        /// <summary> 一時シーンが存在するかどうかを返します </summary>
        public static bool Exists()
        {
            return SceneManager.GetSceneByName(SceneName).isLoaded;
        }

        /// <summary> 一時シーンを破棄します </summary>
        public static void Destroy()
        {
            SceneManager.UnloadSceneAsync(SceneName);
        }

        /// <summary> 一時シーンスコープを作成します </summary>
        public static TempSceneScope CreateTempSceneScope()
        {
            return new TempSceneScope(Create());
        }

        /// <summary>
        /// 一時シーンの自動破棄スコープ
        /// </summary>
        public readonly struct TempSceneScope : IDisposable
        {
            private readonly Scene _scene;

            public TempSceneScope(Scene scene)
            {
                _scene = scene;
            }

            public void Dispose()
            {
                SceneManager.UnloadSceneAsync(_scene);
            }
        }
    }
}
