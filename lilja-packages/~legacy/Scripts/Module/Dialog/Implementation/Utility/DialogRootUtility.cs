using UnityEngine;
using UnityEngine.UI;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// Dialog のルート GameObject の生成を行う Utility
    /// </summary>
    public static class DialogRootUtility
    {
        /// <summary>
        /// システムダイアログのルート（Canvas など）
        /// を生成します </summary>
        /// <returns>生成されたルート GameObject</returns>
        public static GameObject Create()
        {
            var root = new GameObject("Dialog");

            // Canvas 追加
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            root.AddComponent<GraphicRaycaster>();

            return root;
        }
    }
}
