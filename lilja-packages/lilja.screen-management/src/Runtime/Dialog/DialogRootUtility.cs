using UnityEngine;
using UnityEngine.UI;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// システムダイアログが描画される物理キャンバスやスケーラーなどのルートオブジェクトを動的生成するためのユーティリティクラス。
    /// </summary>
    public static class DialogRootUtility
    {
        /// <summary>
        /// ScreenSpaceOverlay 描画モードの UI Canvas、および標準的な画面スケーラーを持ったダイアログ専用ルートオブジェクトを生成します。
        /// </summary>
        /// <returns>生成されたルートのゲームオブジェクト。</returns>
        public static GameObject Create()
        {
            var root = new GameObject("Dialog");

            // Canvas の自動アタッチと設定
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // 基準解像度 1920x1080 に基づくマルチ解像度対応スケーラーの設定
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // UI入力を可能にするための GraphicRaycaster 設定
            root.AddComponent<GraphicRaycaster>();

            return root;
        }
    }
}
