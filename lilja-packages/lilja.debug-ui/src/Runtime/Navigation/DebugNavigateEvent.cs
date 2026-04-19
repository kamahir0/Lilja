using UnityEngine.UIElements;

namespace Lilja.DebugUI
{
    /// <summary>
    /// NavigationButton がクリックされたときに発火するカスタムイベント。
    /// VisualElement 階層を伝播（バブル）し、最初にハンドルしたコンテナがナビゲーションを処理する。
    /// ランタイムでは DebugMenuWindow、エディタでは DebugMenuEditorWindow が受け取る。
    /// </summary>
    public sealed class DebugNavigateEvent : EventBase<DebugNavigateEvent>
    {
        public string PageName { get; private set; }

        public static DebugNavigateEvent GetPooled(IEventHandler target, string pageName)
        {
            var evt = GetPooled();
            evt.target = target;
            evt.PageName = pageName;
            evt.bubbles = true;
            evt.tricklesDown = false;
            return evt;
        }
    }
}
