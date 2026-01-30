using UnityEngine;
using UnityEngine.UI;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// Backdrop の生成を行う Utility
    /// </summary>
    internal static class BackdropUtility
    {
        /// <summary>
        /// Backdrop 用の GameObject を生成します（RaycastTarget 無効）
        /// </summary>
        public static GameObject Create(Transform parent)
        {
            var backdrop = new GameObject("Backdrop", typeof(RectTransform));
            backdrop.transform.SetParent(parent, false);
            backdrop.transform.SetAsFirstSibling(); // 最背面に配置

            var rect = backdrop.GetComponent<RectTransform>();
            SetFullStretch(rect);

            var image = backdrop.AddComponent<Image>();
            image.color = Repository.BackdropColor;
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
}
