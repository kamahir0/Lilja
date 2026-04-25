using UnityEngine.UIElements;

namespace Lilja.DebugUI
{
    /// <summary>
    /// DebugMenu のパッケージ内部向けAPI。
    /// </summary>
    public static partial class DebugMenu
    {
        /// <summary>DebugPage.AddDebugUI などから PageCache へのアクセスに使用する。</summary>
        internal static DebugPageCache CurrentCache => DebugMenuCore.Shared?.PageCache;

        /// <summary>
        /// Show/Hide アニメーションをキャンセルしてウィンドウを即時非表示にする。
        /// エディタが所有権を奪ったとき（ランタイム表示中だった場合）に呼ぶ。
        /// </summary>
        internal static void CancelAndHide()
        {
            ++_animVersion;
            if (_menuRoot != null) _menuRoot.pickingMode = PickingMode.Ignore;
            _window?.SetHidden();
        }

        /// <summary>
        /// エディタホストを HostRegistry に登録する。
        /// null を渡すと登録解除する。
        /// </summary>
        internal static void RegisterEditorHost(IPageHost host)
            => DebugMenuCore.Shared?.HostRegistry.RegisterEditorHost(host);

        /// <summary>
        /// 所有権をリクエストする。エディタが Release する際に呼ぶ。
        /// </summary>
        internal static void RequestOwnership(HostKind kind)
            => DebugMenuCore.Shared?.HostRegistry.RequestOwnership(kind);
    }
}
