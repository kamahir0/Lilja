using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// 全てのシーンがアンロードされてシーン数が 0 になるのを防ぐため、一時シーン（TempScene）を管理するユーティリティ。
    /// </summary>
    internal static class TempSceneUtility
    {
        private const string SceneName = "TempScene";

        /// <summary>
        /// 一時シーンが存在し、ロードされているかどうかを判定します。
        /// </summary>
        /// <returns>存在する場合は true、それ以外は false</returns>
        internal static bool Exists()
        {
            var scene = SceneManager.GetSceneByName(SceneName);
            return scene.IsValid() && scene.isLoaded;
        }

        /// <summary>
        /// 一時シーンを作成します。すでに存在する場合は既存のシーンを返します。
        /// </summary>
        /// <returns>作成または取得した一時シーン</returns>
        internal static Scene Create()
        {
            var scene = SceneManager.GetSceneByName(SceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                return scene;
            }
            return SceneManager.CreateScene(SceneName);
        }

        /// <summary>
        /// 一時シーンを非同期的にアンロードして破棄します。
        /// </summary>
        internal static void Destroy()
        {
            var scene = SceneManager.GetSceneByName(SceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.UnloadSceneAsync(scene);
            }
        }

        /// <summary>
        /// 一時シーンを作成し、破棄（Dispose）時に自動的にアンロードする使い捨てのスコープオブジェクトを生成します。
        /// </summary>
        /// <returns>自動破棄用の一時シーンスコープ</returns>
        internal static TempSceneScope CreateTempSceneScope()
        {
            return new TempSceneScope(Create());
        }

        /// <summary>
        /// 一時シーンの自動アンロードを保証するための Disposable な構造体。
        /// </summary>
        internal struct TempSceneScope : IDisposable
        {
            private Scene _scene;

            /// <summary>
            /// 新しい <see cref="TempSceneScope"/> 構造体を初期化します。
            /// </summary>
            /// <param name="scene">管理対象の一時シーン</param>
            internal TempSceneScope(Scene scene)
            {
                _scene = scene;
            }

            /// <inheritdoc />
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
