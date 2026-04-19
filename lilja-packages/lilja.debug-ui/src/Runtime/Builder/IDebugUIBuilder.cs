using System;
using UnityEngine.UIElements;

namespace Lilja.DebugUI
{
    public interface IDebugUIBuilder
    {
        /// <summary>
        /// VisualElement を追加する
        /// </summary>
        void VisualElement(VisualElement visualElement);

        /// <summary>
        /// 子を作成する
        /// </summary>
        IDebugUIBuilder CreateChildBuilder(VisualElement parent);

        /// <summary>
        /// ページをプールに登録する
        /// </summary>
        void RegisterPage(string pageName, Func<DebugPage> factory);
    }

    internal sealed class DebugUIBuilder : IDebugUIBuilder
    {
        private readonly VisualElement _parent;
        private readonly DebugPageCache _pageCache;

        public DebugUIBuilder(VisualElement parent, DebugPageCache pageCache)
        {
            _parent = parent;
            _pageCache = pageCache;
        }

        public void VisualElement(VisualElement visualElement)
        {
            _parent.Add(visualElement);
        }

        public IDebugUIBuilder CreateChildBuilder(VisualElement parent)
        {
            return new DebugUIBuilder(parent, _pageCache);
        }

        public void RegisterPage(string pageName, Func<DebugPage> factory)
        {
            if (_pageCache == null)
            {
                UnityEngine.Debug.LogWarning("[DebugMenu] DebugMenu.Initialize() が呼ばれる前に RegisterPage が呼ばれました。NavigationButton は無視されます。");
                return;
            }
            _pageCache.Register(pageName, factory);
        }
    }
}
