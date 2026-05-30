using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// UI画面用の共通シーン "GameScreens" を Lazy に生成して管理するユーティリティクラス。
    /// </summary>
    public static class GameScreenSceneUtility
    {
        private const string SceneName = "GameScreens";
        private static Scene? _cachedScene;

        /// <summary>
        /// "GameScreens" シーンを取得、または必要に応じて動的に生成して返します。
        /// </summary>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>有効化された "GameScreens" シーン</returns>
        public static UniTask<Scene> GetOrCreateSceneAsync(CancellationToken cancellationToken)
        {
            if (
                _cachedScene.HasValue
                && _cachedScene.Value.IsValid()
                && _cachedScene.Value.isLoaded
            )
            {
                return UniTask.FromResult(_cachedScene.Value);
            }

            var scene = SceneManager.GetSceneByName(SceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                _cachedScene = scene;
                return UniTask.FromResult(scene);
            }

            // 物理シーンのロードは一切行わず、最初から動的生成を唯一の正常系とする
            scene = SceneManager.CreateScene(SceneName);
            _cachedScene = scene;
            return UniTask.FromResult(scene);
        }
    }
}
