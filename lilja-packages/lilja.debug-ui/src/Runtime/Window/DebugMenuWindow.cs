using System;
using UnityEngine.UIElements;

namespace Lilja.DebugUI
{
    /// <summary>
    /// デバッグメニューのウィンドウ。IPageHost としてランタイム側の所有権プロトコルを担う。
    /// </summary>
    [UxmlElement]
    internal partial class DebugMenuWindow : VisualElement, IPageHost
    {
        // UI
        private Label _label;
        private VisualElement _header;
        private Button _backButton;
        private Button _backToRootButton;
        private VisualElement _contentContainer;

        private RuntimePageNavigator _navigator;
        private DebugMenuPositionController _positionController;

        // クラス
        private const string UssClassName = "c-menu-window";
        private const string HeaderUssClassName = UssClassName + "__header";
        private const string BackButtonUssClassName = UssClassName + "__back-button";
        private const string BackButtonIconUssClassName = UssClassName + "__back-button-icon";
        private const string BackToRootButtonUssClassName = UssClassName + "__back-to-root-button";
        private const string BackToRootButtonIconUssClassName = UssClassName + "__back-to-root-button-icon";
        private const string TitleUssClassName = UssClassName + "__title";
        private const string ContentUssClassName = UssClassName + "__content";
        private const string DefaultSizeUssClassName = UssClassName + "--default-size";
        private const string SurfaceUssClassName = "t-surface";
        private const string PageStackUssClassName = "c-page-stack";

        /// <inheritdoc/>
        public override VisualElement contentContainer => _contentContainer;

        [UxmlAttribute]
        public string Label
        {
            get => _label.text;
            set => _label.text = value;
        }

        // ── IPageHost ────────────────────────────────────────────────────────

        public HostKind Kind => HostKind.Runtime;

        /// <summary>ランタイムが所有権を取得: ForceResetToRoot でルート表示を確立する。</summary>
        public void OnOwnershipGranted() => _navigator?.ForceResetToRoot();

        /// <summary>
        /// ランタイムが所有権を失った（エディタが奪った）: アニメーションキャンセル + 全ページ detach。
        /// </summary>
        public void OnOwnershipRevoked()
        {
            DebugMenu.CancelAndHide();
            _navigator?.Cancel();
            _navigator?.ReleaseAllPages();
        }

        // ── コンストラクタ ───────────────────────────────────────────────────

        public DebugMenuWindow()
        {
            usageHints = UsageHints.DynamicTransform;
            AddToClassList(UssClassName);
            AddToClassList(SurfaceUssClassName);
            AddToClassList(DefaultSizeUssClassName);

            BuildHeader();
            BuildContent();

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        }

        // ── 初期化 ───────────────────────────────────────────────────────────

        /// <summary>
        /// ルートページを初期化する。DebugMenu.Initialize から1回だけ呼ばれる。
        /// </summary>
        internal void InitRootPage(DebugPage rootPage)
        {
            var pageCache = DebugMenuCore.Shared.PageCache;
            _navigator = new RuntimePageNavigator(
                pageCache,
                _contentContainer,
                this,
                label => Label = label,
                SetBackButtonVisibility);

            // RootPageName は RuntimePageNavigator.InitRootPage 内で確定する
            _navigator.InitRootPage(rootPage);
        }

        // ── ナビゲーション API ────────────────────────────────────────────────

        internal void Navigate(string pageName) => _navigator?.Navigate(pageName);

        internal void NavigateTemp(string pageName, Action<IDebugUIBuilder> configure)
            => _navigator?.NavigateTemp(pageName, configure);

        internal void RegisterPage(string pageName, Func<DebugPage> factory)
            => _navigator?.PageCache.Register(pageName, factory);

        internal bool IsPageRegistered(string pageName)
            => _navigator?.IsPageRegistered(pageName) ?? false;

        internal DebugPage GetPage(string pageName) => _navigator?.PageCache.Get(pageName);

        internal void Back() => _navigator?.Back();

        internal void BackToRoot() => _navigator?.BackToRoot();

        // ── View 状態反映 ─────────────────────────────────────────────────────

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            _positionController = new DebugMenuPositionController(this, _header);
            _positionController.RestoreOrDefault();
        }

        internal void ResetPosition() => _positionController?.ResetToDefault();

        internal void SetHidden()
        {
            style.translate = new StyleTranslate(new Translate(-5000, -5000));
            style.opacity = 0f;
        }

        private void SetBackButtonVisibility(bool visibility)
        {
            var v = visibility ? Visibility.Visible : Visibility.Hidden;
            _backButton.style.visibility = v;
            _backToRootButton.style.visibility = v;
        }

        // ── UI 構築 ──────────────────────────────────────────────────────────

        private void BuildHeader()
        {
            _header = new VisualElement();
            _header.AddToClassList(HeaderUssClassName);
            hierarchy.Add(_header);

            _backButton = new Button();
            _backButton.AddToClassList(BackButtonUssClassName);
            var backButtonIcon = new VisualElement();
            backButtonIcon.AddToClassList(BackButtonIconUssClassName);
            backButtonIcon.pickingMode = PickingMode.Ignore;
            _backButton.Add(backButtonIcon);
            ButtonInteractionHelper.Register(_backButton);
            _backButton.clicked += Back;
            _header.Add(_backButton);

            _label = new Label();
            _label.AddToClassList(TitleUssClassName);
            _header.Add(_label);

            _backToRootButton = new Button();
            _backToRootButton.AddToClassList(BackToRootButtonUssClassName);
            var backToRootButtonIcon = new VisualElement();
            backToRootButtonIcon.AddToClassList(BackToRootButtonIconUssClassName);
            backToRootButtonIcon.pickingMode = PickingMode.Ignore;
            _backToRootButton.Add(backToRootButtonIcon);
            ButtonInteractionHelper.Register(_backToRootButton);
            _backToRootButton.clicked += BackToRoot;
            _header.Add(_backToRootButton);
        }

        private void BuildContent()
        {
            _contentContainer = new VisualElement();
            _contentContainer.AddToClassList(ContentUssClassName);
            _contentContainer.AddToClassList(PageStackUssClassName);
            _contentContainer.RegisterCallback<DebugNavigateEvent>(OnNavigateEvent);
            hierarchy.Add(_contentContainer);
        }

        private void OnNavigateEvent(DebugNavigateEvent evt)
        {
            evt.StopPropagation();
            Navigate(evt.PageName);
        }
    }
}
