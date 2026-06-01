using System;
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

    /// <summary>
    /// TempNavigationButton がクリックされたときに発火するカスタムイベント。
    /// キャッシュに登録しない一時ページを、現在表示しているホスト内で表示する。
    /// </summary>
    public sealed class DebugTempNavigateEvent : EventBase<DebugTempNavigateEvent>
    {
        public string PageName { get; private set; }
        public Action<IDebugUIBuilder> Configure { get; private set; }

        public static DebugTempNavigateEvent GetPooled(IEventHandler target, string pageName, Action<IDebugUIBuilder> configure)
        {
            var evt = GetPooled();
            evt.target = target;
            evt.PageName = pageName;
            evt.Configure = configure;
            evt.bubbles = true;
            evt.tricklesDown = false;
            return evt;
        }
    }
}
