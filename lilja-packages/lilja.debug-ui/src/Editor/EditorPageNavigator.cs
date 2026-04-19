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
        private readonly Stack<string> _history = new();
        private string _currentPageName;

        /// <summary>ページ名ラベルを更新するコールバック。</summary>
        internal Action<string> OnLabelChanged;

        /// <summary>バックボタン可視性を更新するコールバック（true = 表示）。</summary>
        internal Action<bool> OnBackVisibilityChanged;

        /// <summary>ランタイムに所有権が奪われたとき（OwnershipRevoked）に呼ばれる。</summary>
        internal Action OnOwnershipLost;

        internal string CurrentPageName => _currentPageName;

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
            DetachCurrentPage();
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

            _history.Clear();
            SetPage(pageName);
        }

        /// <summary>ページ内の NavigationButton からの遷移（履歴に push）。</summary>
        internal override void Navigate(string pageName)
        {
            if (string.IsNullOrEmpty(pageName)) return;

            var prevName = _currentPageName;
            DetachCurrentPage();

            if (!string.IsNullOrEmpty(prevName) && prevName != pageName)
                _history.Push(prevName);

            SetPage(pageName);
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

            _history.Clear();
            DetachCurrentPage();
            SetPage(RootPageName);
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

        private void SetPage(string pageName)
        {
            _currentPageName = pageName;

            var page = _pageCache.Get(pageName);
            if (page == null)
            {
                Debug.LogWarning($"[DebugMenu] EditorPageNavigator: page '{pageName}' not found.");
                return;
            }

            _container.Add(page);
            page.style.left = new StyleLength(new Length(0, LengthUnit.Percent));
            page.OnShown();

            OnLabelChanged?.Invoke(pageName);
            OnBackVisibilityChanged?.Invoke(_history.Count > 0);
        }

        private void DetachCurrentPage()
        {
            if (string.IsNullOrEmpty(_currentPageName)) return;

            var page = _pageCache.Get(_currentPageName);
            if (page != null)
            {
                page.OnHidden();
                page.RemoveFromHierarchy();
            }

            _currentPageName = null;
        }
    }
}
