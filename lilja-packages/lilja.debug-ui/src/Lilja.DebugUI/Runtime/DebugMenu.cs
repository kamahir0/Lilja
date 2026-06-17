using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lilja.DebugUI
{
    /// <summary>
    /// ランタイム用デバッグメニューの初期化、表示制御、ページ遷移を扱うエントリーポイント。
    /// </summary>
    public static partial class DebugMenu
    {
        private static DebugMenuWindow _window;
        private static DebugMenuRoot _menuRoot;
        private static int _animVersion;

        private const string RootName = "debug-menu-root";
        private const string WindowName = "debug-menu-window";

        /// <summary>Initialize() が呼ばれ、使用可能な状態かどうかを返す。</summary>
        public static bool IsInitialized => _window != null;

        /// <summary>ランタイムデバッグメニューが現在表示状態かどうかを返す。</summary>
        public static bool IsVisible => _menuRoot?.pickingMode == PickingMode.Position;

        // ── 初期化 ───────────────────────────────────────────────────────────

        /// <summary>
        /// 指定したルートページでランタイムデバッグメニューを初期化する。
        /// </summary>
        /// <param name="rootPage">最初に表示するルートページ。</param>
        /// <param name="panelSettings">使用する PanelSettings。null の場合はパッケージ内の既定設定を使用する。</param>
        public static void Initialize(DebugPage rootPage, PanelSettings panelSettings = null)
        {
            if (rootPage == null) throw new ArgumentNullException(nameof(rootPage));

            var go = new GameObject("[DebugMenu]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            var uiDoc = go.AddComponent<UIDocument>();
            InitializeUIDocument(uiDoc, panelSettings);
            InitializeRuntimeMenu(uiDoc, rootPage);
        }

        /// <summary>UIDocument に DebugMenu 用の PanelSettings と UXML を設定する。</summary>
        private static void InitializeUIDocument(UIDocument uiDocument, PanelSettings panelSettings)
        {
            if (uiDocument == null) throw new ArgumentNullException(nameof(uiDocument));

            var resolvedPanelSettings = panelSettings != null
                ? panelSettings
                : DebugMenuResources.LoadDefaultPanelSettings();
            if (resolvedPanelSettings == null)
                throw new InvalidOperationException("[DebugMenu] DebugMenuPanelSettings.asset が Resources/DebugMenu 配下に見つかりません。");

            uiDocument.panelSettings = resolvedPanelSettings;

            var visualTreeAsset = DebugMenuResources.LoadDebugMenuVisualTree();
            if (visualTreeAsset == null)
                throw new InvalidOperationException("[DebugMenu] DebugMenu.uxml が Resources/DebugMenu 配下に見つかりません。");

            // UIDocument は Inspector 選択時に Source Asset を参照して再構築されるため、必ず明示しておく。
            uiDocument.visualTreeAsset = visualTreeAsset;
        }

        /// <summary>DebugMenu のランタイム状態を初期化し、ルートページを接続する。</summary>
        private static void InitializeRuntimeMenu(UIDocument uiDocument, DebugPage rootPage)
        {
            // Core singleton を先に生成（DebugMenuWindow のコンストラクタが参照するため）
            DebugMenuCore.Destroy();
            DebugMenuCore.Create();

            var root = uiDocument.rootVisualElement;
            root.Clear();
            uiDocument.visualTreeAsset.CloneTree(root);

            var menuRoot = root.Q<DebugMenuRoot>(RootName);
            if (menuRoot == null)
                throw new InvalidOperationException($"[DebugMenu] DebugMenu.uxml に '{RootName}' が見つかりません。");
            _menuRoot = menuRoot;

            var window = menuRoot.Q<DebugMenuWindow>(WindowName);
            if (window == null)
                throw new InvalidOperationException($"[DebugMenu] DebugMenu.uxml に '{WindowName}' が見つかりません。");
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

        /// <summary>
        /// ランタイムデバッグメニューを表示する。
        /// </summary>
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

        /// <summary>
        /// ランタイムデバッグメニューを非表示にする。
        /// </summary>
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

        /// <summary>
        /// ランタイムデバッグメニューの表示位置を既定位置へ戻す。
        /// </summary>
        public static void ResetPosition()
        {
            if (_window != null)
                _window.ResetPosition();
            else
                DebugMenuPositionController.ClearSavedPosition();
        }

        // ── ナビゲーション ───────────────────────────────────────────────────

        /// <summary>
        /// 登録済みページ名を指定して、ランタイムデバッグメニュー内でページ遷移する。
        /// </summary>
        /// <param name="pageName">遷移先の登録済みページ名。</param>
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

        /// <summary>
        /// ランタイムデバッグメニューを1つ前のページへ戻す。
        /// </summary>
        public static void Back()
        {
            DebugMenuCore.Shared?.HostRegistry.RequestOwnership(HostKind.Runtime);
            _window?.Back();
        }

        /// <summary>
        /// ランタイムデバッグメニューをルートページへ戻す。
        /// </summary>
        public static void BackToRoot()
        {
            DebugMenuCore.Shared?.HostRegistry.RequestOwnership(HostKind.Runtime);
            _window?.BackToRoot();
        }

        /// <summary>
        /// ランタイムデバッグメニュー内で、一時ページへ遷移する。
        /// EditorWindow 上のボタンからは、現在のホストで処理される TempNavigationButton を使用する。
        /// </summary>
        /// <param name="pageName">一時ページ名。</param>
        /// <param name="configure">一時ページのUIを構築する処理。</param>
        public static void NavigateToTemp(string pageName, Action<IDebugUIBuilder> configure)
        {
            DebugMenuCore.Shared?.HostRegistry.RequestOwnership(HostKind.Runtime);
            _window?.NavigateTemp(pageName, configure);
        }

        // ── ページアクセス ───────────────────────────────────────────────────

        /// <summary>
        /// 登録済みページ名を指定して、ランタイムデバッグメニューのページインスタンスを取得する。
        /// </summary>
        /// <param name="pageName">取得する登録済みページ名。</param>
        /// <returns>見つかったページ。未初期化または未登録の場合は null。</returns>
        public static DebugPage GetPage(string pageName)
            => _window?.GetPage(pageName);

        /// <summary>
        /// ページ型を指定して、ランタイムデバッグメニューのページインスタンスを取得する。
        /// </summary>
        /// <typeparam name="T">取得するページ型。型名が登録ページ名として使われる。</typeparam>
        /// <returns>見つかったページ。未初期化または未登録の場合は null。</returns>
        public static T GetPage<T>() where T : DebugPage
            => _window?.GetPage(typeof(T).Name) as T;

    }
}
