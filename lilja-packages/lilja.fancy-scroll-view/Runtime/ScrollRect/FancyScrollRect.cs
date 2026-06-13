/*
 * FancyScrollView (https://github.com/setchi/FancyScrollView)
 * Copyright (c) 2020 setchi
 * Licensed under MIT (https://github.com/setchi/FancyScrollView/blob/master/LICENSE)
 */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lilja.FancyScrollView
{
    /// <summary>
    /// Core implementation for ScrollRect-style views.
    /// </summary>
    /// <typeparam name="TCellData">Data type consumed by each pooled cell.</typeparam>
    /// <typeparam name="TContext"><see cref="FancyScrollViewCore{TCellData,TContext}.Context"/> 縺ｮ蝙・</typeparam>
    [RequireComponent(typeof(Scroller))]
    public abstract class FancyScrollRectCore<TCellData, TContext> : FancyScrollViewCore<TCellData, TContext>
        where TContext : class, IFancyScrollRectContext, new()
    {
        /// <summary>
        /// 繧ｹ繧ｯ繝ｭ繝ｼ繝ｫ荳ｭ縺ｫ繧ｻ繝ｫ縺悟・蛻ｩ逕ｨ縺輔ｌ繧九∪縺ｧ縺ｮ菴咏區縺ｮ繧ｻ繝ｫ謨ｰ.
        /// </summary>
        /// <remarks>
        /// <c>0</c> 繧呈欠螳壹☆繧九→繧ｻ繝ｫ縺悟ｮ悟・縺ｫ髫繧後◆逶ｴ蠕後↓蜀榊茜逕ｨ縺輔ｌ縺ｾ縺・
        /// <c>1</c> 莉･荳翫ｒ謖・ｮ壹☆繧九→, 縺昴・繧ｻ繝ｫ謨ｰ縺縺台ｽ吝・縺ｫ繧ｹ繧ｯ繝ｭ繝ｼ繝ｫ縺励※縺九ｉ蜀榊茜逕ｨ縺輔ｌ縺ｾ縺・
        /// </remarks>
        [SerializeField] protected float reuseCellMarginCount = 0f;

        /// <summary>
        /// 繧ｳ繝ｳ繝・Φ繝・・鬆ｭ縺ｮ菴咏區.
        /// </summary>
        [SerializeField] protected float paddingHead = 0f;

        /// <summary>
        /// 繧ｳ繝ｳ繝・Φ繝・忰蟆ｾ縺ｮ菴咏區.
        /// </summary>
        [SerializeField] protected float paddingTail = 0f;

        /// <summary>
        /// 繧ｹ繧ｯ繝ｭ繝ｼ繝ｫ霆ｸ譁ｹ蜷代・繧ｻ繝ｫ蜷悟｣ｫ縺ｮ菴咏區.
        /// </summary>
        [SerializeField] protected float spacing = 0f;

        /// <summary>
        /// 繧ｻ繝ｫ縺ｮ繧ｵ繧､繧ｺ.
        /// </summary>
        protected abstract float CellSize { get; }

        /// <summary>
        /// 繧ｹ繧ｯ繝ｭ繝ｼ繝ｫ蜿ｯ閭ｽ縺九←縺・°.
        /// </summary>
        /// <remarks>
        /// 繧｢繧､繝・Β謨ｰ縺悟香蛻・ｰ代↑縺上ン繝･繝ｼ繝昴・繝亥・縺ｫ蜈ｨ縺ｦ縺ｮ繧ｻ繝ｫ縺悟庶縺ｾ縺｣縺ｦ縺・ｋ蝣ｴ蜷医・ <c>false</c>, 縺昴ｌ莉･螟悶・ <c>true</c> 縺ｫ縺ｪ繧翫∪縺・
        /// </remarks>
        protected virtual bool Scrollable => MaxScrollPosition > 0f;

        Scroller cachedScroller;

        /// <summary>
        /// 繧ｹ繧ｯ繝ｭ繝ｼ繝ｫ菴咲ｽｮ繧貞宛蠕｡縺吶ｋ <see cref="FancyScrollView.Scroller"/> 縺ｮ繧､繝ｳ繧ｹ繧ｿ繝ｳ繧ｹ.
        /// </summary>
        protected Scroller Scroller => cachedScroller != null ? cachedScroller : (cachedScroller = GetComponent<Scroller>());

#if UNITY_EDITOR
        bool previewScrollerStateStored;
        bool previewScrollerDraggable;
        bool previewScrollbarActive;
        float previewScrollSensitivity;
        float previewScrollbarSize;
#endif

        float ScrollLength => 1f / Mathf.Max(cellInterval, 1e-2f) - 1f;

        float ViewportLength => ScrollLength - reuseCellMarginCount * 2f;

        float PaddingHeadLength => (paddingHead - spacing * 0.5f) / (CellSize + spacing);

        float MaxScrollPosition => ItemsSource.Count
            - ScrollLength
            + reuseCellMarginCount * 2f
            + (paddingHead + paddingTail - spacing) / (CellSize + spacing);

        /// <inheritdoc/>
        protected sealed override void SetupContext(TContext context)
        {
            context.ScrollDirection = Scroller.ScrollDirection;
            context.CalculateScrollSize = () =>
            {
                var interval = CellSize + spacing;
                var reuseMargin = interval * reuseCellMarginCount;
                var scrollSize = Scroller.ViewportSize + interval + reuseMargin * 2f;
                return (scrollSize, reuseMargin);
            };

            SetupScrollRectContext(context);
        }

        /// <summary>
        /// ScrollRect 逕ｨ context 縺瑚ｨｭ螳壹＆繧後◆蠕後↓蜻ｼ縺ｳ蜃ｺ縺輔ｌ縺ｾ縺・
        /// </summary>
        /// <param name="context">蜈ｱ譛・context.</param>
        protected virtual void SetupScrollRectContext(TContext context) { }

        protected override void InitializeCore()
        {
            base.InitializeCore();

            if (Scroller == null)
            {
                throw new MissingComponentException(string.Format(
                    "{0} requires a Scroller component on the same GameObject.",
                    GetType().Name));
            }

            Scroller.OnValueChanged(OnScrollerValueChanged);
            Scroller.OnSelectionChanged(OnScrollerSelectionChanged);
        }

        void OnScrollerValueChanged(float position)
        {
            ApplyScrollerPosition(position);
        }

        private protected virtual void ApplyScrollerPosition(float position)
        {
            UpdateScrollPosition(position);

            if (Scroller.Scrollbar)
            {
                if (position > ItemsSource.Count - 1)
                {
                    ShrinkScrollbar(position - (ItemsSource.Count - 1));
                }
                else if (position < 0f)
                {
                    ShrinkScrollbar(-position);
                }
            }
        }

        void UpdateScrollPosition(float scrollerPosition)
        {
            var position = ToFancyScrollViewPosition(Scrollable ? scrollerPosition : 0f);
            ApplyScrollRectPosition(position, false);
        }

        /// <summary>
        /// ScrollRect 螟画鋤貂医∩菴咲ｽｮ繧偵Ξ繧､繧｢繧ｦ繝医↓驕ｩ逕ｨ縺励∪縺・
        /// </summary>
        /// <param name="position">Scroll view position.</param>
        /// <param name="forceRefresh">繧ｻ繝ｫ蜀・ｮｹ繧ょｼｷ蛻ｶ譖ｴ譁ｰ縺吶ｋ縺九←縺・°.</param>
        protected void ApplyScrollRectPosition(float position, bool forceRefresh)
        {
            UpdatePositionInternal(position, forceRefresh);
        }

        /// <summary>
        /// 繧ｹ繧ｯ繝ｭ繝ｼ繝ｫ遽・峇繧定ｶ・∴縺ｦ繧ｹ繧ｯ繝ｭ繝ｼ繝ｫ縺輔ｌ縺滄㍼縺ｫ蝓ｺ縺･縺・※, 繧ｹ繧ｯ繝ｭ繝ｼ繝ｫ繝舌・縺ｮ繧ｵ繧､繧ｺ繧堤ｸｮ蟆上＠縺ｾ縺・
        /// </summary>
        /// <param name="offset">繧ｹ繧ｯ繝ｭ繝ｼ繝ｫ遽・峇繧定ｶ・∴縺ｦ繧ｹ繧ｯ繝ｭ繝ｼ繝ｫ縺輔ｌ縺滄㍼.</param>
        void ShrinkScrollbar(float offset)
        {
            var scale = 1f - ToFancyScrollViewPosition(offset) / (ViewportLength - PaddingHeadLength);
            UpdateScrollbarSize((ViewportLength - PaddingHeadLength) * scale);
        }

        /// <inheritdoc/>
        private protected override void OnItemsSourceChanged(IList<TCellData> items)
        {
            AdjustCellIntervalAndScrollOffset();
            if (Scroller != null)
            {
                Scroller.SetTotalCount(Mathf.Max(0, ScrollerItemCount));
            }
            RefreshScroller();
        }

        /// <inheritdoc/>
        private protected override void OnBeforeRefresh()
        {
            AdjustCellIntervalAndScrollOffset();
            RefreshScroller();
        }

        /// <summary>
        /// <see cref="Scroller"/> 縺ｮ蜷・ｨｮ迥ｶ諷九ｒ譖ｴ譁ｰ縺励∪縺・
        /// </summary>
        protected void RefreshScroller()
        {
            Scroller.Draggable = Scrollable;
            Scroller.ScrollSensitivity = ToRawScrollerPosition(ViewportLength - PaddingHeadLength);
            Scroller.Position = ToRawScrollerPosition(currentPosition);

            if (Scroller.Scrollbar)
            {
                Scroller.Scrollbar.gameObject.SetActive(Scrollable);
                UpdateScrollbarSize(ViewportLength);
            }
        }

        /// <summary>
        /// 繝薙Η繝ｼ繝昴・繝医→繧ｳ繝ｳ繝・Φ繝・・髟ｷ縺輔↓蝓ｺ縺･縺・※繧ｹ繧ｯ繝ｭ繝ｼ繝ｫ繝舌・縺ｮ繧ｵ繧､繧ｺ繧呈峩譁ｰ縺励∪縺・
        /// </summary>
        /// <param name="viewportLength">繝薙Η繝ｼ繝昴・繝医・繧ｵ繧､繧ｺ.</param>
        protected void UpdateScrollbarSize(float viewportLength)
        {
            var contentLength = Mathf.Max(ItemsSource.Count + (paddingHead + paddingTail - spacing) / (CellSize + spacing), 1);
            Scroller.Scrollbar.size = Scrollable ? Mathf.Clamp01(viewportLength / contentLength) : 1f;
        }

        private protected override float ToFancyScrollViewPosition(float position)
        {
            return position / Mathf.Max(ItemsSource.Count - 1, 1) * MaxScrollPosition - PaddingHeadLength;
        }

        private protected override float ToScrollerPosition(float position, float alignment = 0.5f)
        {
            var offset = alignment * (ScrollLength - (1f + reuseCellMarginCount * 2f))
                + (1f - alignment - 0.5f) * spacing / (CellSize + spacing);
            return ToRawScrollerPosition(Mathf.Clamp(position - offset, 0f, MaxScrollPosition));
        }

        float ToRawScrollerPosition(float position)
        {
            if (Mathf.Approximately(MaxScrollPosition, 0f))
            {
                return 0f;
            }

            return (position + PaddingHeadLength) / MaxScrollPosition * Mathf.Max(ItemsSource.Count - 1, 1);
        }

        /// <summary>
        /// 謖・ｮ壹＆繧後◆險ｭ螳壹ｒ螳溽樟縺吶ｋ縺溘ａ縺ｮ
        /// <see cref="FancyScrollViewCore{TCellData,TContext}.cellInterval"/> 縺ｨ
        /// <see cref="FancyScrollViewCore{TCellData,TContext}.scrollOffset"/> 繧定ｨ育ｮ励＠縺ｦ驕ｩ逕ｨ縺励∪縺・
        /// </summary>
        protected void AdjustCellIntervalAndScrollOffset()
        {
            var totalSize = Scroller.ViewportSize + (CellSize + spacing) * (1f + reuseCellMarginCount * 2f);
            cellInterval = (CellSize + spacing) / totalSize;
            scrollOffset = cellInterval * (1f + reuseCellMarginCount);
        }

        /// <summary>
        /// 謖・ｮ壹＠縺溘い繧､繝・Β縺ｮ菴咲ｽｮ縺ｾ縺ｧ繧ｸ繝｣繝ｳ繝励＠縺ｾ縺・
        /// </summary>
        /// <param name="itemIndex">繧｢繧､繝・Β縺ｮ繧､繝ｳ繝・ャ繧ｯ繧ｹ.</param>
        /// <param name="alignment">繝薙Η繝ｼ繝昴・繝亥・縺ｫ縺翫￠繧九そ繝ｫ菴咲ｽｮ縺ｮ蝓ｺ貅・ 0f(蜈磯ｭ) ~ 1f(譛ｫ蟆ｾ).</param>
        public void JumpTo(int itemIndex, float alignment = 0.5f)
        {
            EnsureInitialized();
            Scroller.Position = ToScrollerPosition(GetScrollPositionForItem(itemIndex), alignment);
        }

        /// <summary>
        /// 謖・ｮ壹＠縺溘い繧､繝・Β縺ｮ菴咲ｽｮ縺ｾ縺ｧ遘ｻ蜍輔＠縺ｾ縺・
        /// </summary>
        /// <param name="itemIndex">繧｢繧､繝・Β縺ｮ繧､繝ｳ繝・ャ繧ｯ繧ｹ.</param>
        /// <param name="duration">遘ｻ蜍輔↓縺九￠繧狗ｧ呈焚.</param>
        /// <param name="alignment">繝薙Η繝ｼ繝昴・繝亥・縺ｫ縺翫￠繧九そ繝ｫ菴咲ｽｮ縺ｮ蝓ｺ貅・ 0f(蜈磯ｭ) ~ 1f(譛ｫ蟆ｾ).</param>
        /// <param name="onComplete">遘ｻ蜍輔′螳御ｺ・＠縺滄圀縺ｫ蜻ｼ縺ｳ蜃ｺ縺輔ｌ繧九さ繝ｼ繝ｫ繝舌ャ繧ｯ.</param>
        public void ScrollTo(int itemIndex, float duration, float alignment = 0.5f, Action onComplete = null)
        {
            EnsureInitialized();
            Scroller.ScrollTo(ToScrollerPosition(GetScrollPositionForItem(itemIndex), alignment), duration, onComplete);
        }

        /// <summary>
        /// 謖・ｮ壹＠縺溘い繧､繝・Β縺ｮ菴咲ｽｮ縺ｾ縺ｧ遘ｻ蜍輔＠縺ｾ縺・
        /// </summary>
        /// <param name="itemIndex">繧｢繧､繝・Β縺ｮ繧､繝ｳ繝・ャ繧ｯ繧ｹ.</param>
        /// <param name="duration">遘ｻ蜍輔↓縺九￠繧狗ｧ呈焚.</param>
        /// <param name="easing">遘ｻ蜍輔↓菴ｿ逕ｨ縺吶ｋ繧､繝ｼ繧ｸ繝ｳ繧ｰ.</param>
        /// <param name="alignment">繝薙Η繝ｼ繝昴・繝亥・縺ｫ縺翫￠繧九そ繝ｫ菴咲ｽｮ縺ｮ蝓ｺ貅・ 0f(蜈磯ｭ) ~ 1f(譛ｫ蟆ｾ).</param>
        /// <param name="onComplete">遘ｻ蜍輔′螳御ｺ・＠縺滄圀縺ｫ蜻ｼ縺ｳ蜃ｺ縺輔ｌ繧九さ繝ｼ繝ｫ繝舌ャ繧ｯ.</param>
        public void ScrollTo(int itemIndex, float duration, Ease easing, float alignment = 0.5f, Action onComplete = null)
        {
            EnsureInitialized();
            Scroller.ScrollTo(ToScrollerPosition(GetScrollPositionForItem(itemIndex), alignment), duration, easing, onComplete);
        }

#if UNITY_EDITOR
        private protected override void ApplyEditorPreviewPosition(float position, bool forceRefresh)
        {
            var scrollerPosition = Scrollable ? ToScrollerPosition(position, 0.5f) : 0f;
            Scroller.Position = scrollerPosition;

            if (forceRefresh)
            {
                ApplyScrollRectPosition(ToFancyScrollViewPosition(Scrollable ? scrollerPosition : 0f), true);
            }
        }

        protected override void OnPreviewBegin()
        {
            base.OnPreviewBegin();

            previewScrollerDraggable = Scroller.Draggable;
            previewScrollSensitivity = Scroller.ScrollSensitivity;

            if (Scroller.Scrollbar)
            {
                previewScrollbarActive = Scroller.Scrollbar.gameObject.activeSelf;
                previewScrollbarSize = Scroller.Scrollbar.size;
            }

            previewScrollerStateStored = true;
        }

        protected override void OnPreviewEnd()
        {
            if (previewScrollerStateStored)
            {
                Scroller.Draggable = previewScrollerDraggable;
                Scroller.ScrollSensitivity = previewScrollSensitivity;

                if (Scroller.Scrollbar)
                {
                    Scroller.Scrollbar.gameObject.SetActive(previewScrollbarActive);
                    Scroller.Scrollbar.size = previewScrollbarSize;
                }
            }

            previewScrollerStateStored = false;
            base.OnPreviewEnd();
        }

        internal override void EndEditorPreview()
        {
            base.EndEditorPreview();
            if (Scroller != null)
            {
                Scroller.SetTotalCount(Mathf.Max(0, ScrollerItemCount));
            }
        }
#endif

        protected virtual void OnValidate()
        {
            if (Scroller != null)
            {
                AdjustCellIntervalAndScrollOffset();
            }

            if (loop)
            {
                loop = false;
                Debug.LogError("Loop is currently not supported in FancyScrollRect.");
            }

            if (Scroller != null && Scroller.SnapEnabled)
            {
                Scroller.SnapEnabled = false;
                Debug.LogError("Snap is currently not supported in FancyScrollRect.");
            }

            if (Scroller != null && Scroller.MovementType == MovementType.Unrestricted)
            {
                Scroller.MovementType = MovementType.Elastic;
                Debug.LogError("MovementType.Unrestricted is currently not supported in FancyScrollRect.");
            }
        }
    }

    /// <summary>
    /// ScrollRect 繧ｹ繧ｿ繧､繝ｫ縺ｮ繧ｹ繧ｯ繝ｭ繝ｼ繝ｫ繝薙Η繝ｼ繧貞ｮ溯｣・☆繧九◆繧√・謚ｽ雎｡蝓ｺ蠎輔け繝ｩ繧ｹ.
    /// 辟｡髯舌せ繧ｯ繝ｭ繝ｼ繝ｫ縺翫ｈ縺ｳ繧ｹ繝翫ャ繝励↓縺ｯ蟇ｾ蠢懊＠縺ｦ縺・∪縺帙ｓ.
    /// <see cref="FancyScrollView{TItemData, TContext}.Context"/> 縺御ｸ崎ｦ√↑蝣ｴ蜷医・
    /// 莉｣繧上ｊ縺ｫ <see cref="FancyScrollRect{TItemData}"/> 繧剃ｽｿ逕ｨ縺励∪縺・
    /// </summary>
    /// <typeparam name="TItemData">繧｢繧､繝・Β縺ｮ繝・・繧ｿ蝙・</typeparam>
    /// <typeparam name="TContext"><see cref="FancyScrollView{TItemData, TContext}.Context"/> 縺ｮ蝙・</typeparam>
    public abstract class FancyScrollRect<TItemData, TContext> : FancyScrollRectCore<TItemData, TContext>
        where TContext : class, IFancyScrollRectContext, new()
    {
        /// <summary>
        /// Edit-mode preview item count.
        /// </summary>
        protected virtual int PreviewItemCount => EditorPreviewItemCount;

        /// <summary>
        /// 貂｡縺輔ｌ縺溘い繧､繝・Β荳隕ｧ縺ｫ蝓ｺ縺･縺・※陦ｨ遉ｺ蜀・ｮｹ繧呈峩譁ｰ縺励∪縺・
        /// </summary>
        /// <param name="items">繧｢繧､繝・Β荳隕ｧ.</param>
        public void SetItems(IList<TItemData> items) => SetItemsCore(items);

        /// <summary>
        /// Edit-mode preview 逕ｨ縺ｮ item data 繧剃ｽ懈・縺励∪縺・
        /// </summary>
        /// <param name="context">Preview item context.</param>
        /// <returns>Preview item data.</returns>
        protected abstract TItemData CreatePreviewItem(FancyScrollPreviewItemContext context);

#if UNITY_EDITOR
        protected sealed override int GetEditorPreviewItemCount() => Mathf.Max(0, PreviewItemCount);

        protected sealed override IList<TItemData> CreateEditorPreviewItems(int itemCount)
        {
            var items = new List<TItemData>(itemCount);
            for (var i = 0; i < itemCount; i++)
            {
                items.Add(CreatePreviewItem(new FancyScrollPreviewItemContext(i, itemCount)));
            }

            return items;
        }
#endif
    }

    /// <summary>
    /// ScrollRect 繧ｹ繧ｿ繧､繝ｫ縺ｮ繧ｹ繧ｯ繝ｭ繝ｼ繝ｫ繝薙Η繝ｼ繧貞ｮ溯｣・☆繧九◆繧√・謚ｽ雎｡蝓ｺ蠎輔け繝ｩ繧ｹ.
    /// 辟｡髯舌せ繧ｯ繝ｭ繝ｼ繝ｫ縺翫ｈ縺ｳ繧ｹ繝翫ャ繝励↓縺ｯ蟇ｾ蠢懊＠縺ｦ縺・∪縺帙ｓ.
    /// </summary>
    /// <typeparam name="TItemData">繧｢繧､繝・Β縺ｮ繝・・繧ｿ蝙・</typeparam>
    /// <seealso cref="FancyScrollRect{TItemData, TContext}"/>
    public abstract class FancyScrollRect<TItemData> : FancyScrollRect<TItemData, FancyScrollRectContext> { }
}
