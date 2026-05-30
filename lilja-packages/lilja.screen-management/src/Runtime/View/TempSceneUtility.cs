using System;
using UnityEngine.SceneManagement;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// Unity でロード済みシーン数が 0 になることを防ぐために、一時的な空シーン（防衛用）を生成・破棄するユーティリティクラス。
    /// </summary>
    internal static class TempSceneUtility
    {
        private const string SceneName = "TempScene";

        /// <summary>
        /// Using スコープによる一時シーンの自動生存期間管理（アンロード保証）を行うためのスコープを生成します。
        /// </summary>
        internal static TempSceneScope CreateTempSceneScope()
        {
            return new TempSceneScope(Create());
        }

        /// <summary>
        /// 防衛用の一時シーンを新規作成、または既存のものを取得してロードし、アクティブシーンに設定します。
        /// </summary>
        private static Scene Create()
        {
            var scene = SceneManager.GetSceneByName(SceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.SetActiveScene(scene);
                return scene;
            }
            var createdScene = SceneManager.CreateScene(SceneName);
            SceneManager.SetActiveScene(createdScene);
            return createdScene;
        }

        /// <summary>
        /// 一時シーンの自動アンロード破棄を Using ブロックで実現するための構造体スコープ。
        /// </summary>
        internal struct TempSceneScope : IDisposable
        {
            private Scene _scene;

            internal TempSceneScope(Scene scene)
            {
                _scene = scene;
            }

            #region IDisposable

            /// <inheritdoc />
            public void Dispose()
            {
                if (_scene.IsValid() && _scene.isLoaded)
                {
                    SceneManager.UnloadSceneAsync(_scene);
                }
            }

            #endregion
        }
    }
}
