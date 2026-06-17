using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lilja.DebugUI
{
    /// <summary>
    /// デバッグメニュー用のページ
    /// </summary>
    [UxmlElement]
    public partial class DebugPage : VisualElement
    {
        #region Virtual

        /// <summary>
        /// UIを構築する
        /// </summary>
        public virtual void Configure(IDebugUIBuilder builder) { }

        /// <summary>
        /// ページが表示される直前に呼ばれる（スライドアニメーション開始前）
        /// </summary>
        public virtual void OnShown() { }

        /// <summary>
        /// ページが非表示になった直後に呼ばれる（スライドアニメーション完了後）
        /// </summary>
        public virtual void OnHidden() { }

        #endregion

        // UI
        private readonly ScrollView _scrollView;

        /// <inheritdoc/>
        public override VisualElement contentContainer => _scrollView.contentContainer;

        // クラス
        private const string UssClassName = "c-page";
        private const string ScrollViewUssClassName = "c-scroll-view";
        private const string SurfaceUssClassName = "t-surface";

        public DebugPage()
        {
            AddToClassList(SurfaceUssClassName);
            AddToClassList(UssClassName);

            // UIBuilder で表示したときに見えるよう、初期は 0% としておく。
            // ランタイムでナビゲーションされる際は Navigator 側で遷移前の座標(100% / -100%)が再設定されるため問題ない。
            style.position = Position.Absolute;
            style.left = new StyleLength(new Length(0, LengthUnit.Percent));
            style.top = 0;
            style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            style.height = new StyleLength(new Length(100, LengthUnit.Percent));

            // スクロールビュー
            _scrollView = new ScrollView();
            _scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _scrollView.AddToClassList(ScrollViewUssClassName);
            hierarchy.Add(_scrollView);

            RegisterScrollerInteractions();
        }

        private void RegisterScrollerInteractions()
        {
            var scroller = _scrollView.verticalScroller;
            VisualElementInteractionHelper.Register(scroller.highButton);
            VisualElementInteractionHelper.Register(scroller.lowButton);

            // ドラッガー: ClampedDragger が TrickleDown で PointerDown を処理するため親 Slider 経由で検知
            var slider = scroller.slider;
            var dragger = slider.Q("unity-dragger");
            if (dragger != null)
                VisualElementInteractionHelper.RegisterSliderDragger(slider, dragger);

            // トラック背景: ホバーのみ変化
            var tracker = scroller.slider.Q("unity-tracker");
            if (tracker != null)
                VisualElementInteractionHelper.RegisterHoverOnly(tracker);
        }

        /// <summary>
        /// スクロール位置をリセットする
        /// </summary>
        public void ResetScrollPosition()
        {
            _scrollView.scrollOffset = Vector2.zero;
        }

        /// <summary>
        /// ページのコンテンツ末尾にUIを動的追加する。
        /// 返り値を Dispose するとUIが削除される。
        /// </summary>
        public IDisposable AddDebugUI(Action<IDebugUIBuilder> configure)
        {
            var wrapper = new VisualElement();
            configure(new DebugUIBuilder(wrapper, DebugMenu.CurrentCache));
            Add(wrapper);
            return new DelegateDisposable(() => wrapper.RemoveFromHierarchy());
        }
    }
}
