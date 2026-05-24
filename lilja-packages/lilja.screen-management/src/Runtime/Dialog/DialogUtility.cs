using UnityEngine;
using UnityEngine.UI;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// Backdrop（半透明背景）の設定を行うクラス。
    /// </summary>
    public static class Backdrop
    {
        /// <summary>
        /// Backdrop の色を取得または設定します。
        /// </summary>
        public static Color Color { get; set; } = new(0f, 0f, 0f, 0.5f);
    }

    /// <summary>
    /// 枠外クリックイベント判定用のボタン。
    /// </summary>
    internal sealed class OutsideButton : Button { }

    /// <summary>
    /// Backdrop の生成を行うユーティリティクラス。
    /// </summary>
    internal static class BackdropUtility
    {
        /// <summary>
        /// Backdrop 用の GameObject を生成します。
        /// </summary>
        /// <param name="parent">親となる Transform</param>
        /// <returns>生成された GameObject</returns>
        public static GameObject Create(Transform parent)
        {
            var backdrop = new GameObject("Backdrop", typeof(RectTransform));
            backdrop.transform.SetParent(parent, false);
            backdrop.transform.SetAsFirstSibling(); // 最背面に配置

            var rect = backdrop.GetComponent<RectTransform>();
            SetFullStretch(rect);

            var image = backdrop.AddComponent<Image>();
            image.color = Backdrop.Color;
            image.raycastTarget = false; // クリック判定しない

            return backdrop;
        }

        private static void SetFullStretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }

    /// <summary>
    /// Dialog のルート GameObject の生成を行うユーティリティクラス。
    /// </summary>
    public static class DialogRootUtility
    {
        /// <summary>
        /// システムダイアログのルート（Canvas など）を生成します。
        /// </summary>
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

    /// <summary>
    /// 枠外ボタンの生成を行うユーティリティクラス。
    /// </summary>
    internal static class OutsideButtonUtility
    {
        /// <summary>
        /// 枠外ボタンを生成します。
        /// </summary>
        /// <param name="parent">親となる Transform</param>
        /// <param name="hasBackdrop">Backdrop が存在するかどうか</param>
        /// <returns>生成された OutsideButton コンポーネント</returns>
        public static OutsideButton Create(Transform parent, bool hasBackdrop)
        {
            var outside = new GameObject("Outside", typeof(RectTransform));
            outside.transform.SetParent(parent, false);

            // Backdrop の後ろ、Frame の手前に配置
            // Backdrop があれば index 1、なければ index 0
            var siblingIndex = hasBackdrop ? 1 : 0;
            outside.transform.SetSiblingIndex(siblingIndex);

            var rect = outside.GetComponent<RectTransform>();
            SetFullStretch(rect);

            // InvisibleGraphic（描画なし、クリック判定あり）
            var graphic = outside.AddComponent<InvisibleGraphic>();
            graphic.raycastTarget = true;

            // Button でクリックを検出
            var button = outside.AddComponent<OutsideButton>();
            button.transition = Selectable.Transition.None;

            return button;
        }

        private static void SetFullStretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
