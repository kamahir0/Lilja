using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace Lilja.DebugUI
{
    /// <summary>
    /// ランタイム側のページナビゲーターとホスト実装。
    /// 履歴スタック・スライドアニメーション・IPageHost（所有権プロトコル）を管理する。
    /// </summary>
    internal sealed class RuntimePageNavigator : DebugPageNavigatorBase, IPageHost
    {
        private readonly Stack<DebugPage> _history = new();

        /// <summary>アニメーションスケジュールの基点となる VisualElement（Window 自身）</summary>
        private readonly VisualElement _animationScheduler;

        /// <summary>ページコンテンツを格納するコンテナ</summary>
        private readonly VisualElement _contentContainer;

        private readonly Action<string> _onLabelChanged;
        private readonly Action<bool> _onBackVisibilityChanged;

        private DebugPage _currentPage;
        private bool _isAnimating;
        private int _animVersion;

        private enum PagePosition { In = 0, OutL = -100, OutR = 100 }

        // ── IPageHost ────────────────────────────────────────────────────────

        public HostKind Kind => HostKind.Runtime;

        /// <summary>
        /// ランタイムが所有権を獲得したとき: ルート表示の 7 不変条件 (I1–I7) を確立する。
        /// </summary>
        public void OnOwnershipGranted() => ForceResetToRoot();

        /// <summary>
        /// ランタイムが所有権を失ったとき: アニメーションをキャンセルし、全ページを detach する。
        /// </summary>
        public void OnOwnershipRevoked()
        {
            Cancel();
            ReleaseAllPages();
        }

        // ── コンストラクタ ───────────────────────────────────────────────────

        internal RuntimePageNavigator(
            DebugPageCache pageCache,
            VisualElement contentContainer,
            VisualElement animationScheduler,
            Action<string> onLabelChanged,
            Action<bool> onBackVisibilityChanged)
            : base(pageCache)
        {
            _contentContainer = contentContainer;
            _animationScheduler = animationScheduler;
            _onLabelChanged = onLabelChanged;
            _onBackVisibilityChanged = onBackVisibilityChanged;
        }

        // ── 初期化 ───────────────────────────────────────────────────────────

        /// <summary>
        /// ルートページを初期化する。DebugMenuWindow 生成後に一度だけ呼ぶ。
        /// </summary>
        internal void InitRootPage(DebugPage rootPage)
        {
            if (rootPage == null) return;

            if (string.IsNullOrEmpty(rootPage.name))
                rootPage.name = rootPage.GetType().Name;

            RootPageName = rootPage.name;
            DebugMenuCore.Shared.RootPageName = rootPage.name;

            _pageCache.RegisterExisting(rootPage);
            _pageCache.PreparePage(rootPage);

            _currentPage = rootPage;
            _contentContainer.Add(rootPage);
            ShowPageImmediately(rootPage, PagePosition.In);
            rootPage.OnShown();
            NotifyBackVisibility();

            // 全ページを OutR 位置に事前アタッチし、パネル接続コストを起動時に分散させる
            foreach (var page in _pageCache.GetAllCachedPages())
            {
                if (page != null && page.parent != _contentContainer)
                {
                    _contentContainer.Add(page);
                    ShowPageImmediately(page, PagePosition.OutR);
                }
            }
        }

        // ── ナビゲーション API ───────────────────────────────────────────────

        internal override void Navigate(string pageName)
        {
            if (_isAnimating) return;
            if (_currentPage == null) return;

            var targetPage = _pageCache.Get(pageName);
            if (targetPage == null) return;

            OnNavigate(targetPage);
        }

        /// <summary>GenericDebugPage を即席生成して遷移する。事前登録不要。</summary>
        internal void NavigateTemp(string pageName, Action<IDebugUIBuilder> configure)
        {
            if (_isAnimating) return;
            if (_currentPage == null) return;

            var page = new GenericDebugPage(pageName, configure);
            _pageCache.PreparePage(page);
            OnNavigate(page);
        }

        internal override void Back()
        {
            if (_isAnimating) return;
            if (_history.Count == 0) return;

            ++_animVersion;
            _isAnimating = true;

            var prevPage = _history.Pop();
            var currentPage = _currentPage;

            _currentPage = prevPage;
            _onLabelChanged(prevPage.name);

            SlidePage(currentPage, PagePosition.In, PagePosition.OutR, DebugMenuSettings.PageSlideDuration, () =>
            {
                currentPage.OnHidden();
                _pageCache.Return(currentPage);
            });

            prevPage.OnShown();
            SlidePage(prevPage, PagePosition.OutL, PagePosition.In, DebugMenuSettings.PageSlideDuration, () =>
            {
                _isAnimating = false;
                NotifyBackVisibility();
            });
        }

        internal override void BackToRoot()
        {
            if (_isAnimating) return;
            if (_history.Count == 0) return;

            ++_animVersion;
            _isAnimating = true;

            var historyArray = _history.ToArray();
            var rootPage = historyArray[^1];
            _history.Clear();

            for (var i = 0; i < historyArray.Length - 1; i++)
            {
                historyArray[i].OnHidden();
                _pageCache.Return(historyArray[i]);
            }

            var currentPage = _currentPage;
            _currentPage = rootPage;
            _onLabelChanged(rootPage.name);

            SlidePage(currentPage, PagePosition.In, PagePosition.OutR, DebugMenuSettings.PageSlideDuration, () =>
            {
                currentPage.OnHidden();
                _pageCache.Return(currentPage);
            });

            rootPage.OnShown();
            SlidePage(rootPage, PagePosition.OutL, PagePosition.In, DebugMenuSettings.PageSlideDuration, () =>
            {
                _isAnimating = false;
                NotifyBackVisibility();
            });
        }

        // ── 所有権プロトコル用内部メソッド ────────────────────────────────

        /// <summary>
        /// アニメーションをキャンセルし、isAnimating を false に強制する。
        /// 所有権が剥奪される直前に呼ぶ。
        /// </summary>
        internal void Cancel()
        {
            ++_animVersion;
            _isAnimating = false;
        }

        /// <summary>
        /// 全ページを contentContainer から detach し、Navigator 状態を中立化する。
        /// OnOwnershipRevoked から呼ばれる。
        /// </summary>
        internal void ReleaseAllPages()
        {
            // 現ページの OnHidden を呼ぶ（アニメ完了コールバック経由で呼ばれていない場合の保険）
            _currentPage?.OnHidden();
            _currentPage = null;
            _history.Clear();

            // contentContainer 内の全子要素（キャッシュ外の temp ページ含む）を一括 detach
            var children = _contentContainer.Children().ToList();
            foreach (var child in children)
                child.RemoveFromHierarchy();

            // 全キャッシュページの位置スタイルをリセット（中立化）
            foreach (var page in _pageCache.GetAllCachedPages())
            {
                if (page != null)
                    page.style.left = new StyleLength(new Length(0, LengthUnit.Percent));
            }
        }

        /// <summary>
        /// ルート表示の 7 不変条件（I1–I7）を確立する。
        /// OnOwnershipGranted から呼ばれる。
        /// </summary>
        internal void ForceResetToRoot()
        {
            // I4, I7: アニメーション状態リセット
            ++_animVersion;
            _isAnimating = false;

            // I5: 履歴クリア
            _currentPage?.OnHidden();
            _currentPage = null;
            _history.Clear();

            if (string.IsNullOrEmpty(RootPageName)) return;

            // I2: 全キャッシュページを contentContainer に再アタッチ
            foreach (var page in _pageCache.GetAllCachedPages())
            {
                if (page == null) continue;
                if (page.parent != _contentContainer)
                    _contentContainer.Add(page);
            }

            // I1: ルートページを取得
            _currentPage = _pageCache.Get(RootPageName);
            if (_currentPage == null) return;

            // I3: ルートページを In (0%) に配置
            ShowPageImmediately(_currentPage, PagePosition.In);
            _currentPage.OnShown();

            // I6: 非 current ページを OutR (100%) に配置
            foreach (var page in _pageCache.GetAllCachedPages())
            {
                if (page == null || page == _currentPage) continue;
                ShowPageImmediately(page, PagePosition.OutR);
            }

            _onLabelChanged?.Invoke(RootPageName);
            _onBackVisibilityChanged?.Invoke(false);
        }

        // ── プライベート ─────────────────────────────────────────────────────

        private void OnNavigate(DebugPage targetPage)
        {
            // 所有権移譲で detach 済みのため、_contentContainer 外にある場合は再アタッチ
            if (targetPage.parent != _contentContainer)
                _contentContainer.Add(targetPage);

            _onLabelChanged(targetPage.name);

            var prevPage = _currentPage;
            _currentPage = targetPage;

            ++_animVersion;
            _isAnimating = true;

            if (prevPage.name == targetPage.name)
            {
                // 同一名ナビゲーション: 履歴に push せず、スライドで刷新
                SlidePage(prevPage, PagePosition.In, PagePosition.OutL, DebugMenuSettings.PageSlideDuration, () =>
                {
                    prevPage.OnHidden();
                    _pageCache.Return(prevPage);
                });
            }
            else
            {
                _history.Push(prevPage);
                SlidePage(prevPage, PagePosition.In, PagePosition.OutL, DebugMenuSettings.PageSlideDuration, () =>
                {
                    prevPage.OnHidden();
                });
            }

            targetPage.OnShown();
            SlidePage(targetPage, PagePosition.OutR, PagePosition.In, DebugMenuSettings.PageSlideDuration, () =>
            {
                _isAnimating = false;
                NotifyBackVisibility();
            });
        }

        private void ShowPageImmediately(DebugPage page, PagePosition position)
        {
            page.style.left = new StyleLength(new Length((float)position, LengthUnit.Percent));
        }

        private void SlidePage(DebugPage page, PagePosition from, PagePosition to, float duration, Action onComplete = null)
        {
            var version = _animVersion;
            DebugMenuAnimator.Slide(page, _animationScheduler, (float)from, (float)to, duration,
                shouldCancel: () => _animVersion != version,
                onComplete);
        }

        private void NotifyBackVisibility()
        {
            _onBackVisibilityChanged(_history.Count > 0);
        }
    }
}
