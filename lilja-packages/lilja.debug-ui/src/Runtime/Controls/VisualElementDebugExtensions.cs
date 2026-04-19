using System;
using UnityEngine.UIElements;

namespace Lilja.DebugUI
{
    public static class VisualElementDebugExtensions
    {
        /// <summary>
        /// アンカー要素の直後にUIを動的挿入する。
        /// 返り値を Dispose するとUIが削除される。
        /// </summary>
        public static IDisposable PlaceBehind<T>(this T anchor, Action<IDebugUIBuilder> configure)
            where T : VisualElement, IDebugUI
        {
            return InsertRelative(anchor, configure, offset: 1);
        }

        /// <summary>
        /// アンカー要素の直前にUIを動的挿入する。
        /// 返り値を Dispose するとUIが削除される。
        /// </summary>
        public static IDisposable PlaceInFront<T>(this T anchor, Action<IDebugUIBuilder> configure)
            where T : VisualElement, IDebugUI
        {
            return InsertRelative(anchor, configure, offset: 0);
        }

        private static IDisposable InsertRelative<T>(T anchor, Action<IDebugUIBuilder> configure, int offset)
            where T : VisualElement, IDebugUI
        {
            var parent = anchor.parent;
            if (parent == null)
                throw new InvalidOperationException("[DebugMenu] PlaceBehind/PlaceInFront: anchor 要素が親に追加されていません。");

            var wrapper = new VisualElement();
            configure(new DebugUIBuilder(wrapper, DebugMenu.CurrentCache));

            var index = parent.IndexOf(anchor);
            parent.Insert(index + offset, wrapper);

            return new DelegateDisposable(() => wrapper.RemoveFromHierarchy());
        }
    }
}
