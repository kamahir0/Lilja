using System;
using UnityEngine.UIElements;

namespace Lilja.DebugUI
{
    /// <summary>
    /// 画面全体に広がる共通ルートコンテナ
    /// </summary>
    [UxmlElement]
    internal partial class DebugMenuRoot : VisualElement
    {
        // クラス
        private const string UssClassName = "t-root";
        private const string ScreenLayoutUssClassName = "l-screen";
        private const string BgTransparentUssClassName = "u-bg-transparent";

        public DebugMenuRoot()
        {
            AddToClassList(UssClassName);
            AddToClassList(ScreenLayoutUssClassName);
            AddToClassList(BgTransparentUssClassName);
        }

        /// <summary>
        /// 矩形外タップ検知を設定する。ウィンドウ外をタップしたときに onOutsideTap を呼び出す。
        /// </summary>
        internal void SetupOutsideTapHandler(Func<VisualElement> getWindow, Action onOutsideTap)
        {
            RegisterCallback<PointerDownEvent>(evt =>
            {
                var window = getWindow();
                if (window != null && !window.worldBound.Contains(evt.position))
                {
                    onOutsideTap();
                }
            }, TrickleDown.TrickleDown);
        }
    }
}
