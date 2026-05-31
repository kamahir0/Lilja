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
        private static readonly SemaphoreSlim _semaphore = new(1, 1);

        /// <summary>
        /// "GameScreens" シーンを取得、または必要に応じて動的に生成して返します。
        /// </summary>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>有効化された "GameScreens" シーン</returns>
        public static async UniTask<Scene> GetOrCreateSceneAsync(CancellationToken cancellationToken)
        {
            // すでにキャッシュが有効であれば、ロックを取得せずに即座に返す（ファストパス）
            if (
                _cachedScene.HasValue
                && _cachedScene.Value.IsValid()
                && _cachedScene.Value.isLoaded
            )
            {
                return _cachedScene.Value;
            }

            await _semaphore.WaitAsync(cancellationToken);

            try
            {
                // ロック獲得後に、他タスクによってすでにキャッシュが初期化されていないかダブルチェック（Double-checked locking pattern）
                if (
                    _cachedScene.HasValue
                    && _cachedScene.Value.IsValid()
                    && _cachedScene.Value.isLoaded
                )
                {
                    return _cachedScene.Value;
                }

                var scene = SceneManager.GetSceneByName(SceneName);
                if (scene.IsValid() && scene.isLoaded)
                {
                    _cachedScene = scene;
                    return scene;
                }

                // 物理シーンのロードは一切行わず、最初から動的生成を唯一の正常系とする
                scene = SceneManager.CreateScene(SceneName);
                _cachedScene = scene;
                return scene;
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
