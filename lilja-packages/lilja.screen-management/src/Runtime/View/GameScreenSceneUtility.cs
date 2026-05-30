using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// UI画面用の共通シーン "GameScreens" を Lazy に生成・加算ロードして管理するユーティリティクラス。
    /// </summary>
    public static class GameScreenSceneUtility
    {
        private const string SceneName = "GameScreens";
        private static Scene? _cachedScene;

        /// <summary>
        /// "GameScreens" シーンを取得、または必要に応じて加算ロードして返します。
        /// </summary>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>有効化された "GameScreens" シーン</returns>
        public static async UniTask<Scene> GetOrCreateSceneAsync(CancellationToken cancellationToken)
        {
            if (_cachedScene.HasValue && _cachedScene.Value.IsValid() && _cachedScene.Value.isLoaded)
            {
                return _cachedScene.Value;
            }

            var scene = SceneManager.GetSceneByName(SceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                _cachedScene = scene;
                return scene;
            }

            try
            {
                // ビルド設定に含まれている場合の加算ロードを試みる
                await SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive)
                    .WithCancellation(cancellationToken);

                scene = SceneManager.GetSceneByName(SceneName);
                if (scene.IsValid() && scene.isLoaded)
                {
                    _cachedScene = scene;
                    return scene;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // ビルド設定にない等の理由でロードできない場合はフォールバックへ移行
                Debug.LogWarning(
                    $"[Lilja.ScreenManagement] ゲーム画面専用シーン '{SceneName}' の加算ロードに失敗しました。動的シーン生成にフォールバックします。エラー: {ex.Message}"
                );
            }

            // 動的な空シーン生成フォールバック
            scene = SceneManager.CreateScene(SceneName);
            _cachedScene = scene;
            return scene;
        }
    }
}
