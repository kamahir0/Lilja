using System;
using System.Collections.Generic;
using Lilja.DebugUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using PackageManagerInfo = UnityEditor.PackageManager.PackageInfo;

namespace Lilja.DebugUI.Editor
{
    /// <summary>
    /// ランタイムの DebugMenu とリンクするエディタウィンドウ。
    /// PlayMode 中に EditorPageNavigator を通じて DebugPageCache のページを表示する。
    /// ナビゲーションはエディタ独自の履歴を持ち、ランタイムには影響しない。
    /// HostRegistry を介した排他所有権モデルにより、ランタイムとの状態不整合を防ぐ。
    /// </summary>
    public sealed class DebugMenuEditorWindow : EditorWindow
    {
        [MenuItem("Lilja/DebugUI/Debug Menu Inspector")]
        private static void Open()
        {
            var window = GetWindow<DebugMenuEditorWindow>();
            window.titleContent = new GUIContent("Debug Menu");
        }

        // ── ナビゲーション ────────────────────────────────────────────────────

        private EditorPageNavigator _navigator;

        // ── UI 要素 ────────────────────────────────────────────────────────────

        private VisualElement _notPlayingView;
        private VisualElement _playingView;
        private Label _headerTitle;
        private Button _backButton;
        private Button _backToRootButton;
        private ScrollView _pageListScrollView;
        private VisualElement _rightPane;

        // ── パス定数 ──────────────────────────────────────────────────────────

        private const string PackageName = "com.kamahir0.lilja.debug-ui";
        private const string ThemeUssRelativePath =
            "Editor/StyleSheets/DebugMenuEditorTheme.uss";
        private const string MenuUssRelativePath =
            "Runtime/StyleSheets/DebugMenu.uss";
        private const string LegacyThemeUssPath =
            "Assets/lilja.debug-ui/src/Editor/StyleSheets/DebugMenuEditorTheme.uss";
        private const string LegacyMenuUssPath =
            "Assets/lilja.debug-ui/src/Runtime/StyleSheets/DebugMenu.uss";
        private const float PageListMinWidth = 160f;
        private const float PageContentMinWidth = 320f;
        private const float WindowMinHeight = 240f;

        // ── ライフサイクル ─────────────────────────────────────────────────────

        private void OnEnable()
        {
            minSize = new Vector2(PageListMinWidth + PageContentMinWidth, WindowMinHeight);
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            ReleaseNavigator();
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;

            var themeUss = LoadPackageStyleSheet(ThemeUssRelativePath, LegacyThemeUssPath);
            var menuUss = LoadPackageStyleSheet(MenuUssRelativePath, LegacyMenuUssPath);
            if (themeUss != null) root.styleSheets.Add(themeUss);
            if (menuUss != null) root.styleSheets.Add(menuUss);
            root.AddToClassList("editor-debug-window");
            EditorTextRenderingUtility.ApplyMode(root);

            BuildNotPlayingView(root);
            BuildPlayingView(root);

            RefreshPlayModeState();
        }

        private static StyleSheet LoadPackageStyleSheet(string relativePath, string legacyPath)
        {
            var packageInfo = PackageManagerInfo.FindForAssembly(typeof(DebugMenuEditorWindow).Assembly);
            if (packageInfo != null && !string.IsNullOrEmpty(packageInfo.assetPath))
            {
                var packagePath = $"{packageInfo.assetPath}/{relativePath}";
                var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(packagePath);
                if (styleSheet != null) return styleSheet;
            }

            var namedPackagePath = $"Packages/{PackageName}/{relativePath}";
            var namedPackageStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(namedPackagePath);
            if (namedPackageStyleSheet != null) return namedPackageStyleSheet;

            return AssetDatabase.LoadAssetAtPath<StyleSheet>(legacyPath);
        }

        // ── PlayMode 対応 ──────────────────────────────────────────────────────

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingPlayMode:
                    // ランタイムが破棄される前にナビゲーターを解放
                    ReleaseNavigator();
                    break;

                case PlayModeStateChange.EnteredEditMode:
                    ShowNotPlayingView();
                    break;

                case PlayModeStateChange.EnteredPlayMode:
                    EditorApplication.delayCall += WaitForInitialize;
                    break;
            }
        }

        private void WaitForInitialize()
        {
            if (!Application.isPlaying) return;

            var core = DebugMenuCore.Shared;
            if (core == null || core.PageCache.GetPageNames().Count == 0)
            {
                EditorApplication.delayCall += WaitForInitialize;
                return;
            }

            ShowPlayingView();
        }

        private void RefreshPlayModeState()
        {
            var core = DebugMenuCore.Shared;
            if (Application.isPlaying && core != null && core.PageCache.GetPageNames().Count > 0)
                ShowPlayingView();
            else
                ShowNotPlayingView();
        }

        private void ShowNotPlayingView()
        {
            if (_notPlayingView == null || _playingView == null) return;
            _notPlayingView.style.display = DisplayStyle.Flex;
            _playingView.style.display = DisplayStyle.None;
        }

        private void ShowPlayingView()
        {
            if (_notPlayingView == null || _playingView == null) return;
            _notPlayingView.style.display = DisplayStyle.None;
            _playingView.style.display = DisplayStyle.Flex;

            // EditorPageNavigator を初期化（再入を防ぐため既存を解放してから）
            ReleaseNavigator();
            SetupNavigator();

            RefreshPageList();

            // エディタウィンドウを開いた瞬間にルートページを表示し、ランタイム側を即時非表示にする
            var rootPageName = DebugMenuCore.Shared?.RootPageName;
            if (!string.IsNullOrEmpty(rootPageName))
                EditorPresentPage(rootPageName);
        }

        // ── ナビゲーター管理 ───────────────────────────────────────────────────

        private void SetupNavigator()
        {
            var core = DebugMenuCore.Shared;
            if (core == null) return;

            _navigator = new EditorPageNavigator(core.PageCache, _rightPane);
            _navigator.RootPageName = core.RootPageName;

            _navigator.OnLabelChanged = name =>
            {
                if (_headerTitle != null) _headerTitle.text = name;
                UpdateHeader();
            };
            _navigator.OnBackVisibilityChanged = UpdateHeaderButtons;
            _navigator.OnOwnershipLost = () =>
            {
                // ランタイムが所有権を奪ったとき: ヘッダーをリセット
                if (_headerTitle != null) _headerTitle.text = "ページを選択してください";
                UpdateHeader();
            };

            DebugMenu.RegisterEditorHost(_navigator);
        }

        private void ReleaseNavigator()
        {
            if (_navigator == null) return;

            // 所有権をランタイムに返す（ForceResetToRoot が走り、I1–I7 が確立される）
            DebugMenu.RequestOwnership(HostKind.Runtime);
            DebugMenu.RegisterEditorHost(null);

            _navigator.Release();
            _navigator = null;

            if (_headerTitle != null) _headerTitle.text = "ページを選択してください";
            UpdateHeader();
        }

        // ── ナビゲーション操作 ──────────────────────────────────────────────────

        private void OnNavigateEvent(DebugNavigateEvent evt)
        {
            evt.StopPropagation();
            EditorNavigateTo(evt.PageName);
        }

        private void OnTempNavigateEvent(DebugTempNavigateEvent evt)
        {
            evt.StopPropagation();
            EditorNavigateToTemp(evt.PageName, evt.Configure);
        }

        /// <summary>NavigationButton などページ内からの遷移（履歴 push）。</summary>
        private void EditorNavigateTo(string pageName)
        {
            if (_navigator == null || string.IsNullOrEmpty(pageName)) return;

            // 所有権を取得（まだ持っていなければ RuntimeHost.OnOwnershipRevoked が走る）
            DebugMenu.RequestOwnership(HostKind.Editor);
            _navigator.Navigate(pageName);
            ScheduleScrollbarFix();
        }

        /// <summary>TempNavigationButton からの一時ページ遷移（履歴 push）。</summary>
        private void EditorNavigateToTemp(string pageName, Action<IDebugUIBuilder> configure)
        {
            if (_navigator == null || string.IsNullOrEmpty(pageName)) return;

            DebugMenu.RequestOwnership(HostKind.Editor);
            _navigator.NavigateTemp(pageName, configure);
            ScheduleScrollbarFix();
        }

        /// <summary>ページ一覧クリック / Back Root: 履歴リセットして最上位として表示。</summary>
        private void EditorPresentPage(string pageName)
        {
            if (_navigator == null || string.IsNullOrEmpty(pageName)) return;

            DebugMenu.RequestOwnership(HostKind.Editor);
            _navigator.PresentPage(pageName);
            ScheduleScrollbarFix();
        }

        private void EditorBack()
        {
            if (_navigator == null || _navigator.CurrentPageName == null) return;

            // Back は所有権を持ったままで行う（既に Editor owner のはず）
            _navigator.Back();
            ScheduleScrollbarFix();
        }

        private void EditorBackToRoot()
        {
            if (_navigator == null) return;
            _navigator.BackToRoot();
            ScheduleScrollbarFix();
        }

        // ── UI 補助 ────────────────────────────────────────────────────────────

        /// <summary>
        /// パネル間移動後、ScrollView の dragger サイズを正しく再計算させる。
        /// </summary>
        private void ScheduleScrollbarFix()
        {
            if (_navigator == null) return;

            var page = _navigator.CurrentPage;
            if (page == null) return;

            SuppressScrollbarFocus(page);

            rootVisualElement.schedule.Execute(() =>
            {
                // ナビゲーターが別ページに移っていたらスキップ
                if (_navigator == null || _navigator.CurrentPage != page) return;

                var sv = page.Q<ScrollView>();
                if (sv == null) return;

                var scroller = sv.verticalScroller;
                float contentH = sv.contentContainer.layout.height;
                if (contentH <= 0f) return;

                float factor = scroller.slider.layout.height / contentH;
                scroller.Adjust(factor);
            }).ExecuteLater(16);
        }

        private static void SuppressScrollbarFocus(VisualElement root)
        {
            root.Query<ScrollView>().ForEach(sv =>
            {
                sv.focusable = false;
                sv.verticalScroller.focusable = false;
                sv.verticalScroller.slider.focusable = false;
            });
        }

        private void UpdateHeader()
        {
            if (_headerTitle == null) return;
            var hasPage = _navigator != null && !string.IsNullOrEmpty(_navigator.CurrentPageName);
            if (!hasPage) _headerTitle.text = "ページを選択してください";
            UpdateHeaderButtons(_navigator != null && hasPage);
        }

        private void UpdateHeaderButtons(bool hasHistory)
        {
            if (_backButton == null || _backToRootButton == null) return;

            // _navigator の履歴に依存した実際の値を使う
            bool canGoBack = _navigator != null &&
                !string.IsNullOrEmpty(_navigator.CurrentPageName) &&
                hasHistory;

            var v = canGoBack ? Visibility.Visible : Visibility.Hidden;
            _backButton.style.visibility = v;
            _backToRootButton.style.visibility = v;
        }

        // ── UI 構築 ────────────────────────────────────────────────────────────

        private void BuildNotPlayingView(VisualElement root)
        {
            _notPlayingView = new VisualElement();
            _notPlayingView.style.flexGrow = 1;
            _notPlayingView.style.alignItems = Align.Center;
            _notPlayingView.style.justifyContent = Justify.Center;

            var label = new Label("PlayMode 実行中のみ使用できます");
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            _notPlayingView.Add(label);

            root.Add(_notPlayingView);
        }

        private void BuildPlayingView(VisualElement root)
        {
            _playingView = new VisualElement();
            _playingView.style.flexGrow = 1;
            _playingView.style.flexDirection = FlexDirection.Column;

            // ヘッダー
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 4;
            header.style.paddingRight = 4;
            header.style.paddingTop = 2;
            header.style.paddingBottom = 2;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new StyleColor(new Color(0, 0, 0, 0.3f));

            _backButton = new Button(EditorBack) { text = "←" };
            _backButton.style.visibility = Visibility.Hidden;
            _backButton.AddToClassList("c-control-size");
            _backButton.AddToClassList("c-button");
            _backButton.AddToClassList("c-button--secondary");
            header.Add(_backButton);

            _headerTitle = new Label("ページを選択してください");
            _headerTitle.style.flexGrow = 1;
            _headerTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            header.Add(_headerTitle);

            _backToRootButton = new Button(EditorBackToRoot) { text = "Root" };
            _backToRootButton.style.visibility = Visibility.Hidden;
            _backToRootButton.AddToClassList("c-control-size");
            _backToRootButton.AddToClassList("c-button");
            _backToRootButton.AddToClassList("c-button--secondary");
            header.Add(_backToRootButton);

            _playingView.Add(header);

            // ボディ（左ペイン: ページ一覧 + 右ペイン: ページ表示）
            var body = new TwoPaneSplitView(0, PageListMinWidth, TwoPaneSplitViewOrientation.Horizontal);
            body.style.flexGrow = 1;

            var leftContainer = new VisualElement();
            leftContainer.style.flexGrow = 1;
            leftContainer.style.minWidth = PageListMinWidth;
            leftContainer.style.borderRightWidth = 1;
            leftContainer.style.borderRightColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 0.5f));

            var leftHeader = new Label("Pages");
            leftHeader.style.paddingLeft = 4;
            leftHeader.style.paddingTop = 2;
            leftHeader.style.paddingBottom = 2;
            leftHeader.style.borderBottomWidth = 1;
            leftHeader.style.borderBottomColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 0.5f));
            leftContainer.Add(leftHeader);

            _pageListScrollView = new ScrollView();
            _pageListScrollView.style.flexGrow = 1;
            leftContainer.Add(_pageListScrollView);

            body.Add(leftContainer);

            _rightPane = new VisualElement();
            _rightPane.AddToClassList("c-page-stack");
            _rightPane.style.flexGrow = 1;
            _rightPane.style.minWidth = PageContentMinWidth;
            _rightPane.style.overflow = Overflow.Hidden;
            _rightPane.style.position = Position.Relative;
            _rightPane.RegisterCallback<DebugNavigateEvent>(OnNavigateEvent);
            _rightPane.RegisterCallback<DebugTempNavigateEvent>(OnTempNavigateEvent);
            body.Add(_rightPane);

            _playingView.Add(body);
            root.Add(_playingView);
        }

        private void RefreshPageList()
        {
            if (_pageListScrollView == null) return;
            _pageListScrollView.Clear();

            var core = DebugMenuCore.Shared;
            if (core == null) return;

            var names = core.PageCache.GetPageNames();
            var rootPageName = core.RootPageName;

            // ルートページを先頭に並べ直す
            var orderedNames = new List<string>(names);
            if (!string.IsNullOrEmpty(rootPageName))
            {
                orderedNames.Remove(rootPageName);
                orderedNames.Insert(0, rootPageName);
            }

            foreach (var name in orderedNames)
            {
                var pageName = name;
                var btn = new Button(() => EditorPresentPage(pageName)) { text = pageName };
                btn.style.marginLeft = 0;
                btn.style.marginRight = 0;
                btn.style.marginTop = 1;
                btn.style.marginBottom = 0;
                _pageListScrollView.Add(btn);
            }
        }
    }

    internal static class EditorTextRenderingUtility
    {
        internal static void ApplyMode(VisualElement root)
        {
            if (root == null) return;

            ApplyModeTo(root);
            root.Query<TextElement>().ForEach(ApplyModeTo);
        }

        internal static void ClearMode(VisualElement root)
        {
            if (root == null) return;

            ClearModeFrom(root);
            root.Query<TextElement>().ForEach(ClearModeFrom);
        }

        internal static void RefreshTextNow(VisualElement root)
        {
            if (root == null) return;

            RefreshText(root);
        }

        private static void RefreshText(VisualElement root)
        {
            ApplyMode(root);

            root.Query<TextElement>().ForEach(text =>
            {
                text.MarkDirtyText();
                text.MarkDirtyRepaint();
            });
        }

        private static void ApplyModeTo(VisualElement element)
        {
            element.style.unityTextGenerator = TextGeneratorType.Standard;
            element.style.unityEditorTextRenderingMode = EditorTextRenderingMode.Bitmap;
        }

        private static void ClearModeFrom(VisualElement element)
        {
            element.style.unityTextGenerator = StyleKeyword.Null;
            element.style.unityEditorTextRenderingMode = StyleKeyword.Null;
        }

    }
}
