using UnityEngine;
using UnityEngine.UI;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// 枠外ボタンの生成を行う Utility
    /// </summary>
    internal static class OutsideButtonUtility
    {
        /// <summary>
        /// 枠外ボタンを生成します
        /// </summary>
        public static OutsideButton Create(Transform parent, bool hasBackdrop)
        {
            var outside = new GameObject("Outside", typeof(RectTransform));
            outside.transform.SetParent(parent, false);

            // Backdrop の後ろ、Frame の手前に配置
            // BackdropImage があれば index 1、なければ index 0
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
