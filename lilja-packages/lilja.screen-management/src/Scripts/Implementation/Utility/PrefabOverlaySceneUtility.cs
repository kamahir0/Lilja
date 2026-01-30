using UnityEngine.SceneManagement;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// PrefabOverlay シーンの作成と取得を行うユーティリティ
    /// </summary>
    public static class PrefabOverlaySceneUtility
    {
        private const string SceneName = "PrefabOverlay";

        /// <summary>
        /// PrefabOverlayシーンを取得または作成します
        /// </summary>
        public static Scene GetOrCreate()
        {
            var scene = SceneManager.GetSceneByName(SceneName);
            if (!scene.IsValid()) scene = SceneManager.CreateScene(SceneName);
            return scene;
        }
    }
}
