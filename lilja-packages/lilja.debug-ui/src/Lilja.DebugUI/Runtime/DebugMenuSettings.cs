namespace Lilja.DebugUI
{
    /// <summary>
    /// デバッグメニュー全体で共有するアニメーション・操作定数
    /// </summary>
    internal static class DebugMenuSettings
    {
        // ── ウィンドウ Show / Hide アニメーション ──────────────────────
        internal const float ShowDuration = 0.2f;
        internal const float HideDuration = 0.15f;
        internal const float HideScale = 0.9f;

        // ── ページスライドアニメーション ────────────────────────────────
        internal const float PageSlideDuration = 0.4f;

        // ── ドラッグ操作 ────────────────────────────────────────────────
        /// <summary>ドラッグと見なす最小移動距離（ピクセル）</summary>
        internal const float DragThreshold = 5f;
    }
}
