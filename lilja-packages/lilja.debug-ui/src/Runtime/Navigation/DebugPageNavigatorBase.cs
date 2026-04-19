namespace Lilja.DebugUI
{
    /// <summary>
    /// Runtime / Editor 両ナビゲーターの共通基底。
    /// PageCache の参照のみを共有し、遷移の具体的な見た目（アニメーション有無など）は派生クラスに委ねる。
    /// </summary>
    internal abstract class DebugPageNavigatorBase
    {
        protected readonly DebugPageCache _pageCache;

        internal DebugPageCache PageCache => _pageCache;
        internal string RootPageName { get; set; }

        protected DebugPageNavigatorBase(DebugPageCache pageCache)
        {
            _pageCache = pageCache;
        }

        internal abstract void Navigate(string pageName);
        internal abstract void Back();
        internal abstract void BackToRoot();
        internal bool IsPageRegistered(string pageName) => _pageCache.Contains(pageName);
    }
}
