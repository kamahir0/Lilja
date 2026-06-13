/*
 * FancyScrollView (https://github.com/setchi/FancyScrollView)
 * Copyright (c) 2020 setchi
 * Licensed under MIT (https://github.com/setchi/FancyScrollView/blob/master/LICENSE)
 */

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Lilja.FancyScrollView
{
    /// <summary>
    /// 繧ｰ繝ｪ繝・ラ繝ｬ繧､繧｢繧ｦ繝医・繧ｹ繧ｯ繝ｭ繝ｼ繝ｫ繝薙Η繝ｼ繧貞ｮ溯｣・☆繧九◆繧√・謚ｽ雎｡蝓ｺ蠎輔け繝ｩ繧ｹ.
    /// 辟｡髯舌せ繧ｯ繝ｭ繝ｼ繝ｫ縺翫ｈ縺ｳ繧ｹ繝翫ャ繝励↓縺ｯ蟇ｾ蠢懊＠縺ｦ縺・∪縺帙ｓ.
    /// <see cref="FancyScrollView{TItemData, TContext}.Context"/> 縺御ｸ崎ｦ√↑蝣ｴ蜷医・
    /// 莉｣繧上ｊ縺ｫ <see cref="FancyGridView{TItemData}"/> 繧剃ｽｿ逕ｨ縺励∪縺・
    /// </summary>
    /// <typeparam name="TItemData">繧｢繧､繝・Β縺ｮ繝・・繧ｿ蝙・</typeparam>
    /// <typeparam name="TContext"><see cref="FancyScrollView{TItemData, TContext}.Context"/> 縺ｮ蝙・</typeparam>
    public abstract class FancyGridView<TItemData, TContext> : FancyScrollRectCore<TItemData[], TContext>
        where TContext : class, IFancyGridViewContext, new()
    {
        /// <summary>
        /// Grid view 縺悟・驛ｨ縺ｧ菴ｿ逕ｨ縺吶ｋ髱・generic 縺ｮ繧ｻ繝ｫ繧ｰ繝ｫ繝ｼ繝怜渕蠎輔け繝ｩ繧ｹ.
        /// </summary>
        protected abstract class DefaultCellGroup : FancyCellGroup<TItemData, TContext> { }

        /// <summary>
        /// 譛蛻昴↓繧ｻ繝ｫ繧帝・鄂ｮ縺吶ｋ霆ｸ譁ｹ蜷代・繧ｻ繝ｫ蜷悟｣ｫ縺ｮ菴咏區.
        /// </summary>
        [SerializeField] protected float startAxisSpacing = 0f;

        /// <summary>
        /// 譛蛻昴↓繧ｻ繝ｫ繧帝・鄂ｮ縺吶ｋ霆ｸ譁ｹ蜷代・繧ｻ繝ｫ謨ｰ.
        /// </summary>
        [SerializeField] protected int startAxisCellCount = 4;

        /// <summary>
        /// 繧ｻ繝ｫ縺ｮ繧ｵ繧､繧ｺ.
        /// </summary>
        [SerializeField] protected Vector2 cellSize = new Vector2(100f, 100f);

        FancyCell<TItemData[], TContext> cellGroupTemplate;

        /// <inheritdoc/>
        protected sealed override FancyCell<TItemData[], TContext> CellPrefab => cellGroupTemplate;

        /// <inheritdoc/>
        protected sealed override float CellSize => Scroller.ScrollDirection == ScrollDirection.Horizontal
            ? cellSize.x
            : cellSize.y;

        /// <summary>
        /// Edit-mode preview item count.
        /// </summary>
        protected virtual int PreviewItemCount => EditorPreviewItemCount;

        /// <summary>
        /// 繧｢繧､繝・Β縺ｮ邱乗焚.
        /// </summary>
        public int DataCount { get; private set; }

        /// <summary>
        /// 貂｡縺輔ｌ縺・flat item 荳隕ｧ縺ｫ蝓ｺ縺･縺・※陦ｨ遉ｺ蜀・ｮｹ繧呈峩譁ｰ縺励∪縺・
        /// </summary>
        /// <param name="items">Flat item 荳隕ｧ.</param>
        public void SetItems(IList<TItemData> items)
        {
            DataCount = items != null ? items.Count : 0;
            SetItemsCore(CreateGroups(items));
        }

        /// <summary>
        /// Edit-mode preview 逕ｨ縺ｮ flat item data 繧剃ｽ懈・縺励∪縺・
        /// </summary>
        /// <param name="context">Preview item context.</param>
        /// <returns>Preview item data.</returns>
        protected abstract TItemData CreatePreviewItem(FancyScrollPreviewItemContext context);

        /// <summary>
        /// 譛蛻昴↓繧ｻ繝ｫ縺檎函謌舌＆繧後ｋ逶ｴ蜑阪↓蜻ｼ縺ｳ蜃ｺ縺輔ｌ縺ｾ縺・
        /// <see cref="Setup{TGroup}(FancyCell{TItemData, TContext})"/> 繝｡繧ｽ繝・ラ繧剃ｽｿ逕ｨ縺励※繧ｻ繝ｫ繝・Φ繝励Ξ繝ｼ繝医・繧ｻ繝・ヨ繧｢繝・・繧定｡後▲縺ｦ縺上□縺輔＞.
        /// </summary>
        protected abstract void SetupCellTemplate();

        /// <summary>
        /// 繧ｻ繝ｫ繝・Φ繝励Ξ繝ｼ繝医・繧ｻ繝・ヨ繧｢繝・・繧定｡後＞縺ｾ縺・
        /// </summary>
        /// <param name="cellTemplate">繧ｻ繝ｫ縺ｮ繝・Φ繝励Ξ繝ｼ繝・</param>
        /// <typeparam name="TGroup">繧ｻ繝ｫ繧ｰ繝ｫ繝ｼ繝励・蝙・</typeparam>
        protected virtual void Setup<TGroup>(FancyCell<TItemData, TContext> cellTemplate)
            where TGroup : FancyCell<TItemData[], TContext>
        {
            if (cellTemplate == null)
            {
                throw new InvalidOperationException("Cell template is not assigned.");
            }

            Context.CellTemplate = cellTemplate.gameObject;

            cellGroupTemplate = new GameObject("Group").AddComponent<TGroup>();
            cellGroupTemplate.transform.SetParent(cellContainer, false);
            cellGroupTemplate.SetVisible(false);

#if UNITY_EDITOR
            if (IsEditorPreviewing)
            {
                MarkEditorPreviewObject(cellGroupTemplate.gameObject);
            }
#endif
        }

        /// <inheritdoc/>
        protected sealed override void SetupScrollRectContext(TContext context)
        {
            context.ScrollDirection = Scroller.ScrollDirection;
            context.GetGroupCount = () => Mathf.Max(1, startAxisCellCount);
            context.GetStartAxisSpacing = () => startAxisSpacing;
            context.GetCellSize = () => Scroller.ScrollDirection == ScrollDirection.Horizontal
                ? cellSize.y
                : cellSize.x;

            SetupCellTemplate();
        }

        /// <inheritdoc/>
        private protected override float GetScrollPositionForItem(int itemIndex)
        {
            return itemIndex / Mathf.Max(1, startAxisCellCount);
        }

        IList<TItemData[]> CreateGroups(IList<TItemData> items)
        {
            var source = items ?? Array.Empty<TItemData>();
            var groupSize = Mathf.Max(1, startAxisCellCount);

            return source
                .Select((item, index) => (item, index))
                .GroupBy(
                    x => x.index / groupSize,
                    x => x.item)
                .Select(group => group.ToArray())
                .ToArray();
        }

#if UNITY_EDITOR
        protected override string EditorPreviewCellDataTypeName => typeof(TItemData).Name;

        private protected override string GetEditorPreviewCellPrefabError()
        {
            return null;
        }

        protected sealed override int GetEditorPreviewItemCount() => Mathf.Max(0, PreviewItemCount);

        internal override float GetEditorPreviewMaxPosition() => Mathf.Max(0, GetEditorPreviewItemCount() - 1);

        protected sealed override IList<TItemData[]> CreateEditorPreviewItems(int itemCount)
        {
            var previewItems = Enumerable.Range(0, itemCount)
                .Select(index => CreatePreviewItem(new FancyScrollPreviewItemContext(index, itemCount)))
                .ToArray();

            DataCount = previewItems.Length;
            return CreateGroups(previewItems);
        }

        private protected override void ApplyEditorPreviewPosition(float position, bool forceRefresh)
        {
            var groupSize = Mathf.Max(1, startAxisCellCount);
            base.ApplyEditorPreviewPosition(position / groupSize, forceRefresh);
        }

        protected override void OnPreviewEnd()
        {
            if (cellGroupTemplate != null)
            {
                DestroyImmediate(cellGroupTemplate.gameObject);
            }

            cellGroupTemplate = null;
            base.OnPreviewEnd();
        }
#endif
    }

    /// <summary>
    /// 繧ｰ繝ｪ繝・ラ繝ｬ繧､繧｢繧ｦ繝医・繧ｹ繧ｯ繝ｭ繝ｼ繝ｫ繝薙Η繝ｼ繧貞ｮ溯｣・☆繧九◆繧√・謚ｽ雎｡蝓ｺ蠎輔け繝ｩ繧ｹ.
    /// 辟｡髯舌せ繧ｯ繝ｭ繝ｼ繝ｫ縺翫ｈ縺ｳ繧ｹ繝翫ャ繝励↓縺ｯ蟇ｾ蠢懊＠縺ｦ縺・∪縺帙ｓ.
    /// </summary>
    /// <typeparam name="TItemData">繧｢繧､繝・Β縺ｮ繝・・繧ｿ蝙・</typeparam>
    /// <seealso cref="FancyGridView{TItemData, TContext}"/>
    public abstract class FancyGridView<TItemData> : FancyGridView<TItemData, FancyGridViewContext> { }
}
