using UnityEngine;
using UnityEngine.UI;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// ダイアログ背面の透過カバー（Backdrop）オブジェクトを動的生成・制御するためのユーティリティクラス。
    /// </summary>
    internal static class BackdropUtility
    {
        /// <summary>
        /// 指定された親オブジェクトの直下に、最背面を覆う Backdrop ゲームオブジェクトを動的に作成して初期セットアップを行います。
        /// </summary>
        /// <param name="parent">作成先となる親の <see cref="Transform"/>。</param>
        /// <returns>生成された Backdrop のゲームオブジェクト。</returns>
        public static GameObject Create(Transform parent)
        {
            var backdrop = new GameObject("Backdrop", typeof(RectTransform));
            backdrop.transform.SetParent(parent, false);
            backdrop.transform.SetAsFirstSibling(); // 最背面に配置

            var rect = backdrop.GetComponent<RectTransform>();
            SetFullStretch(rect);

            var image = backdrop.AddComponent<Image>();
            image.color = Backdrop.Color;
            image.raycastTarget = false; // 背景の画像自体ではクリック判定させず、OutsideButton 側で処理

            return backdrop;
        }

        /// <summary>
        /// 指定された RectTransform を親オブジェクトの全領域にフィットするようストレッチ設定を適用します。
        /// </summary>
        internal static void SetFullStretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
