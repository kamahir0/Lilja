using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lilja.DebugUI
{
    public static class DebugMenu
    {
        private static DebugMenuWindow _window;
        private static DebugMenuRoot _menuRoot;
        private static int _animVersion;

        /// <summary>DebugPage.AddDebugUI などから PageCache へのアクセスに使用する。</summary>
        internal static DebugPageCache CurrentCache => DebugMenuCore.Shared?.PageCache;

        /// <summary>Initialize() が呼ばれ、使用可能な状態かどうかを返す。</summary>
        public static bool IsInitialized => _window != null;

        // ── 初期化 ───────────────────────────────────────────────────────────

        public static void Initialize(DebugPage rootPage, PanelSettings panelSettings = null)
        {
            var go = new GameObject("[DebugMenu]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            var uiDoc = go.AddComponent<UIDocument>();
            uiDoc.panelSettings = panelSettings != null
                ? panelSettings
                : DebugMenuResources.LoadDefaultPanelSettings();
            Initialize(uiDoc, rootPage);
        }

        public static void Initialize(UIDocument uiDocument, DebugPage rootPage)
        {
            // Core singleton を先に生成（DebugMenuWindow のコンストラクタが参照するため）
            DebugMenuCore.Destroy();
            DebugMenuCore.Create();

            var root = uiDocument.rootVisualElement;
            root.Clear();

            var menuRoot = new DebugMenuRoot();
            root.Add(menuRoot);
            _menuRoot = menuRoot;

            var window = new DebugMenuWindow();
            menuRoot.Add(window);
            _window = window;

            // RuntimeHost を登録して所有権を取得
            DebugMenuCore.Shared.HostRegistry.RegisterRuntimeHost(window);
            DebugMenuCore.Shared.HostRegistry.RequestOwnership(HostKind.Runtime);

            // ルートページを初期化（ここで DebugMenuCore.RootPageName も確定する）
            window.InitRootPage(rootPage);

            // 初期状態は即時非表示
            window.SetHidden();
            menuRoot.pickingMode = PickingMode.Ignore;

            // 矩形外タップで閉じる
            menuRoot.SetupOutsideTapHandler(() => _window, Hide);
        }

        // ── 表示制御 ─────────────────────────────────────────────────────────

        public static void Show()
        {
            if (_window == null || _menuRoot == null) return;

            // エディタが所有権を持っていた場合は奪い取り、ForceResetToRoot を発動する
            DebugMenuCore.Shared?.HostRegistry.RequestOwnership(HostKind.Runtime);

            _menuRoot.pickingMode = PickingMode.Position;
            _window.style.translate = StyleKeyword.None;

            var version = ++_animVersion;
            DebugMenuAnimator.AnimateScaleOpacity(
                _window,
                scaleFrom: DebugMenuSettings.HideScale, scaleTo: 1f,
                opacityFrom: 0f, opacityTo: 1f,
                duration: DebugMenuSettings.ShowDuration,
                easing: DebugMenuAnimator.EaseOutCubic,
                shouldCancel: () => _animVersion != version,
                onComplete: null
            );
        }

        public static void Hide()
        {
            if (_window == null || _menuRoot == null) return;

            _menuRoot.pickingMode = PickingMode.Ignore;

            var version = ++_animVersion;
            DebugMenuAnimator.AnimateScaleOpacity(
                _window,
                scaleFrom: 1f, scaleTo: DebugMenuSettings.HideScale,
                opacityFrom: 1f, opacityTo: 0f,
                duration: DebugMenuSettings.HideDuration,
                easing: DebugMenuAnimator.EaseInCubic,
                shouldCancel: () => _animVersion != version,
                onComplete: _window.SetHidden
            );
        }

        public static void ResetPosition()
        {
            if (_window != null)
                _window.ResetPosition();
            else
                DebugMenuPositionController.ClearSavedPosition();
        }

        // ── ナビゲーション ───────────────────────────────────────────────────

        public static void NavigateTo(string pageName)
        {
            if (_window == null) return;
            if (!_window.IsPageRegistered(pageName))
            {
                Debug.LogError($"[DebugMenu] Page '{pageName}' is not registered.");
                return;
            }
            DebugMenuCore.Shared?.HostRegistry.RequestOwnership(HostKind.Runtime);
            _window.Navigate(pageName);
        }

        public static void Back()
        {
            DebugMenuCore.Shared?.HostRegistry.RequestOwnership(HostKind.Runtime);
            _window?.Back();
        }

        public static void BackToRoot()
        {
            DebugMenuCore.Shared?.HostRegistry.RequestOwnership(HostKind.Runtime);
            _window?.BackToRoot();
        }

        public static void NavigateToTemp(string pageName, Action<IDebugUIBuilder> configure)
        {
            DebugMenuCore.Shared?.HostRegistry.RequestOwnership(HostKind.Runtime);
            _window?.NavigateTemp(pageName, configure);
        }

        // ── ページアクセス ───────────────────────────────────────────────────

        public static DebugPage GetPage(string pageName)
            => _window?.GetPage(pageName);

        public static T GetPage<T>() where T : DebugPage
            => _window?.GetPage(typeof(T).Name) as T;

        // ── エディタウィンドウ用 API (internal) ───────────────────────────────

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
