using System;
using System.Collections.Generic;
using Lilja.DebugUI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lilja.DebugUI.Editor
{
    /// <summary>
    /// エディタウィンドウ側のページナビゲーターと IPageHost 実装。
    /// アニメーションなし・即時切替で DebugPage を rightPane に表示する。
    /// 所有権の取得・解放は HostRegistry 経由で行われる。
    /// </summary>
    internal sealed class EditorPageNavigator : DebugPageNavigatorBase, IPageHost
    {
        private readonly VisualElement _container;
        private readonly Stack<DebugPage> _history = new();
        private DebugPage _currentPage;

        /// <summary>ページ名ラベルを更新するコールバック。</summary>
        internal Action<string> OnLabelChanged;

        /// <summary>バックボタン可視性を更新するコールバック（true = 表示）。</summary>
        internal Action<bool> OnBackVisibilityChanged;

        /// <summary>ランタイムに所有権が奪われたとき（OwnershipRevoked）に呼ばれる。</summary>
        internal Action OnOwnershipLost;

        internal DebugPage CurrentPage => _currentPage;

        internal string CurrentPageName => _currentPage?.name;

        // ── IPageHost ────────────────────────────────────────────────────────

        public HostKind Kind => HostKind.Editor;

        /// <summary>
        /// エディタが所有権を獲得したとき。
        /// 実際の表示は PresentPage / Navigate の呼び出しで行うため no-op。
        /// </summary>
        public void OnOwnershipGranted() { }

        /// <summary>
        /// エディタが所有権を失ったとき。現ページを detach し状態をリセットする。
        /// </summary>
        public void OnOwnershipRevoked()
        {
            Release();
            OnOwnershipLost?.Invoke();
        }

        // ── コンストラクタ ───────────────────────────────────────────────────

        internal EditorPageNavigator(DebugPageCache pageCache, VisualElement container)
            : base(pageCache)
        {
            _container = container;
        }

        // ── ナビゲーション API ───────────────────────────────────────────────

        /// <summary>
        /// 指定ページを最初のページとして表示する（履歴をリセット）。
        /// HostRegistry.RequestOwnership(Editor) の後に呼ぶこと。
        /// </summary>
        internal void PresentPage(string pageName)
        {
            if (string.IsNullOrEmpty(pageName)) return;

            var page = GetCachedPage(pageName);
            if (page == null) return;

            _history.Clear();
            DetachCurrentPage();
            SetPage(page);
        }

        /// <summary>ページ内の NavigationButton からの遷移（履歴に push）。</summary>
        internal override void Navigate(string pageName)
        {
            if (string.IsNullOrEmpty(pageName)) return;

            var page = GetCachedPage(pageName);
            if (page == null) return;

            NavigateToPage(page);
        }

        /// <summary>キャッシュに登録しない一時ページへ遷移する。</summary>
        internal void NavigateTemp(string pageName, Action<IDebugUIBuilder> configure)
        {
            if (string.IsNullOrEmpty(pageName)) return;

            var page = new GenericDebugPage(pageName, configure);
            _pageCache.PreparePage(page);
            NavigateToPage(page);
        }

        internal override void Back()
        {
            if (_history.Count == 0) return;

            DetachCurrentPage();
            var prev = _history.Pop();
            SetPage(prev);
        }

        internal override void BackToRoot()
        {
            if (string.IsNullOrEmpty(RootPageName)) return;

            var rootPage = GetCachedPage(RootPageName);
            if (rootPage == null) return;

            _history.Clear();
            DetachCurrentPage();
            SetPage(rootPage);
        }

        /// <summary>
        /// 現ページを detach し、状態をリセットする。
        /// OnDisable / Dispose 時に呼ぶ。
        /// </summary>
        internal void Release()
        {
            DetachCurrentPage();
            _history.Clear();
        }

        // ── プライベート ─────────────────────────────────────────────────────

        private DebugPage GetCachedPage(string pageName)
        {
            var page = _pageCache.Get(pageName);
            if (page == null)
            {
                Debug.LogWarning($"[DebugMenu] EditorPageNavigator: page '{pageName}' not found.");
                return null;
            }

            return page;
        }

        private void NavigateToPage(DebugPage page)
        {
            if (page == null) return;

            var prevPage = _currentPage;
            DetachCurrentPage();

            if (prevPage != null && prevPage.name != page.name)
                _history.Push(prevPage);

            SetPage(page);
        }

        private void SetPage(DebugPage page)
        {
            if (page == null) return;

            _currentPage = page;

            EditorTextRenderingUtility.ApplyMode(page);
            if (page.parent != _container)
                _container.Add(page);

            page.style.left = new StyleLength(new Length(0, LengthUnit.Percent));
            EditorTextRenderingUtility.RefreshTextNow(page);
            page.OnShown();

            OnLabelChanged?.Invoke(page.name);
            OnBackVisibilityChanged?.Invoke(_history.Count > 0);
        }

        private void DetachCurrentPage()
        {
            if (_currentPage == null) return;

            var page = _currentPage;
            page.OnHidden();
            EditorTextRenderingUtility.ClearMode(page);
            page.RemoveFromHierarchy();

            _currentPage = null;
        }
    }
}
