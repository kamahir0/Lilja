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
    /// Core implementation that owns lifecycle, pooling, scroller wiring, and edit-mode preview.
    /// Public scroll view types expose item-data oriented APIs on top of this class.
    /// </summary>
    /// <typeparam name="TCellData">Data type consumed by each pooled cell.</typeparam>
    /// <typeparam name="TContext"><see cref="Context"/> 縺ｮ蝙・</typeparam>
    public abstract class FancyScrollViewCore<TCellData, TContext> : FancyScrollViewBase
        where TContext : class, new()
    {
        /// <summary>
        /// 繧ｻ繝ｫ蜷悟｣ｫ縺ｮ髢馴囈.
        /// </summary>
        [SerializeField, Range(1e-2f, 1f)] protected float cellInterval = 0.2f;

        /// <summary>
        /// 繧ｹ繧ｯ繝ｭ繝ｼ繝ｫ菴咲ｽｮ縺ｮ蝓ｺ貅・
        /// </summary>
        /// <remarks>
        /// 縺溘→縺医・縲・<c>0.5</c> 繧呈欠螳壹＠縺ｦ繧ｹ繧ｯ繝ｭ繝ｼ繝ｫ菴咲ｽｮ縺・<c>0</c> 縺ｮ蝣ｴ蜷・ 荳ｭ螟ｮ縺ｫ譛蛻昴・繧ｻ繝ｫ縺碁・鄂ｮ縺輔ｌ縺ｾ縺・
        /// </remarks>
        [SerializeField, Range(0f, 1f)] protected float scrollOffset = 0.5f;

        /// <summary>
        /// 繧ｻ繝ｫ繧貞ｾｪ迺ｰ縺励※驟咲ｽｮ縺輔○繧九←縺・°.
        /// </summary>
        /// <remarks>
        /// <c>true</c> 縺ｫ縺吶ｋ縺ｨ譛蠕後・繧ｻ繝ｫ縺ｮ蠕後↓譛蛻昴・繧ｻ繝ｫ, 譛蛻昴・繧ｻ繝ｫ縺ｮ蜑阪↓譛蠕後・繧ｻ繝ｫ縺御ｸｦ縺ｶ繧医≧縺ｫ縺ｪ繧翫∪縺・
        /// 辟｡髯舌せ繧ｯ繝ｭ繝ｼ繝ｫ繧貞ｮ溯｣・☆繧句ｴ蜷医・ <c>true</c> 繧呈欠螳壹＠縺ｾ縺・
        /// </remarks>
        [SerializeField] protected bool loop = false;

        /// <summary>
        /// 繧ｻ繝ｫ縺ｮ隕ｪ隕∫ｴ縺ｨ縺ｪ繧・<c>Transform</c>.
        /// </summary>
        [SerializeField] protected Transform cellContainer = default;

        readonly List<FancyCell<TCellData, TContext>> pool = new List<FancyCell<TCellData, TContext>>();
        readonly IList<TCellData> emptyItems = new List<TCellData>();

        /// <summary>
        /// 蛻晄悄蛹匁ｸ医∩縺九←縺・°.
        /// </summary>
        protected bool initialized;

        /// <summary>
        /// 迴ｾ蝨ｨ縺ｮ繧ｹ繧ｯ繝ｭ繝ｼ繝ｫ菴咲ｽｮ.
        /// </summary>
        protected float currentPosition;

        /// <summary>
        /// 繧ｻ繝ｫ縺ｮ Prefab.
        /// </summary>
        protected abstract FancyCell<TCellData, TContext> CellPrefab { get; }

        /// <summary>
        /// 繧｢繧､繝・Β荳隕ｧ縺ｮ繝・・繧ｿ.
        /// </summary>
        protected IList<TCellData> ItemsSource { get; private set; } = new List<TCellData>();

        /// <summary>
        /// <typeparamref name="TContext"/> 縺ｮ繧､繝ｳ繧ｹ繧ｿ繝ｳ繧ｹ.
        /// 繧ｻ繝ｫ縺ｨ繧ｹ繧ｯ繝ｭ繝ｼ繝ｫ繝薙Η繝ｼ髢薙〒蜷後§繧､繝ｳ繧ｹ繧ｿ繝ｳ繧ｹ縺悟・譛峨＆繧後∪縺・ 諠・ｱ縺ｮ蜿励￠貂｡縺励ｄ迥ｶ諷九・菫晄戟縺ｫ菴ｿ逕ｨ縺励∪縺・
        /// </summary>
        protected TContext Context { get; } = new TContext();

#if UNITY_EDITOR
        IList<TCellData> itemsSourceBeforePreview;
        bool initializedBeforePreview;
        bool loopBeforePreview;
        bool editorPreviewing;
        float cellIntervalBeforePreview;
        float currentPositionBeforePreview;
        float scrollOffsetBeforePreview;
        int cachedEditorPreviewItemCount = -1;

        internal override bool EditorPreviewing => editorPreviewing;

        /// <summary>
        /// Edit-mode preview is currently active.
        /// </summary>
        protected bool IsEditorPreviewing => editorPreviewing;
#endif

        /// <summary>
        /// 繧ｻ繝ｫ縺ｨ繧ｹ繧ｯ繝ｭ繝ｼ繝ｫ繝薙Η繝ｼ髢薙〒蜈ｱ譛峨☆繧・context 繧定ｨｭ螳壹＠縺ｾ縺・
        /// </summary>
        /// <param name="context">蜈ｱ譛・context.</param>
        protected virtual void SetupContext(TContext context) { }

        /// <summary>
        /// 繧ｻ繝ｫ逕滓・逶ｴ蠕後↓蜻ｼ縺ｳ蜃ｺ縺輔ｌ縺ｾ縺・
        /// </summary>
        /// <param name="cell">逕滓・縺輔ｌ縺溘そ繝ｫ.</param>
        protected virtual void OnCellCreated(FancyCell<TCellData, TContext> cell) { }

        /// <summary>
        /// <see cref="Scroller"/> 縺ｮ驕ｸ謚槭う繝ｳ繝・ャ繧ｯ繧ｹ縺悟､画峩縺輔ｌ縺滄圀縺ｫ蜻ｼ縺ｳ蜃ｺ縺輔ｌ縺ｾ縺・
        /// </summary>
        /// <param name="index">驕ｸ謚槭う繝ｳ繝・ャ繧ｯ繧ｹ.</param>
        protected virtual void OnScrollerSelectionChanged(int index) { }

        /// <summary>
        /// <see cref="Scroller"/> 縺ｫ險ｭ螳壹☆繧狗ｷ剰ｦ∫ｴ謨ｰ.
        /// </summary>
        private protected virtual int ScrollerItemCount => ItemsSource.Count;

        /// <summary>
        /// 謖・ｮ壹＆繧後◆ item index 縺瑚｡ｨ縺吶せ繧ｯ繝ｭ繝ｼ繝ｫ菴咲ｽｮ.
        /// </summary>
        /// <param name="itemIndex">繧｢繧､繝・Β縺ｮ繧､繝ｳ繝・ャ繧ｯ繧ｹ.</param>
        /// <returns>繧ｹ繧ｯ繝ｭ繝ｼ繝ｫ菴咲ｽｮ.</returns>
        private protected virtual float GetScrollPositionForItem(int itemIndex) => itemIndex;

        /// <summary>
        /// <see cref="Scroller"/> 縺梧桶縺・せ繧ｯ繝ｭ繝ｼ繝ｫ菴咲ｽｮ繧偵％縺ｮ view 縺梧桶縺・ｽ咲ｽｮ縺ｫ螟画鋤縺励∪縺・
        /// </summary>
        /// <param name="position"><see cref="Scroller"/> 縺梧桶縺・せ繧ｯ繝ｭ繝ｼ繝ｫ菴咲ｽｮ.</param>
        /// <returns>縺薙・ view 縺梧桶縺・せ繧ｯ繝ｭ繝ｼ繝ｫ菴咲ｽｮ.</returns>
        private protected virtual float ToFancyScrollViewPosition(float position) => position;

        /// <summary>
        /// 縺薙・ view 縺梧桶縺・せ繧ｯ繝ｭ繝ｼ繝ｫ菴咲ｽｮ繧・<see cref="Scroller"/> 縺梧桶縺・ｽ咲ｽｮ縺ｫ螟画鋤縺励∪縺・
        /// </summary>
        /// <param name="position">縺薙・ view 縺梧桶縺・せ繧ｯ繝ｭ繝ｼ繝ｫ菴咲ｽｮ.</param>
        /// <param name="alignment">繝薙Η繝ｼ繝昴・繝亥・縺ｫ縺翫￠繧九そ繝ｫ菴咲ｽｮ縺ｮ蝓ｺ貅・ 0f(蜈磯ｭ) ~ 1f(譛ｫ蟆ｾ).</param>
        /// <returns><see cref="Scroller"/> 縺梧桶縺・せ繧ｯ繝ｭ繝ｼ繝ｫ菴咲ｽｮ.</returns>
        private protected virtual float ToScrollerPosition(float position, float alignment = 0.5f) => position;

        /// <summary>
        /// ItemsSource 縺梧峩譁ｰ縺輔ｌ縺溽峩蠕後↓蜻ｼ縺ｳ蜃ｺ縺輔ｌ縺ｾ縺・
        /// </summary>
        /// <param name="items">譖ｴ譁ｰ蠕後・ items.</param>
        private protected virtual void OnItemsSourceChanged(IList<TCellData> items) { }



        /// <summary>
        /// 繝ｬ繧､繧｢繧ｦ繝域峩譁ｰ縺ｮ逶ｴ蜑阪↓蜻ｼ縺ｳ蜃ｺ縺輔ｌ縺ｾ縺・
        /// </summary>
        private protected virtual void OnBeforeRefresh() { }

        /// <summary>
        /// 繧ｻ繝ｫ縺ｮ陦ｨ遉ｺ蜀・ｮｹ繧貞・驕ｩ逕ｨ縺励∪縺・
        /// </summary>
        public void RefreshItems() => RefreshInternal(true);

        /// <summary>
        /// 繧ｻ繝ｫ縺ｮ陦ｨ遉ｺ蜀・ｮｹ繧貞・驕ｩ逕ｨ縺帙★縲√Ξ繧､繧｢繧ｦ繝医□縺代ｒ譖ｴ譁ｰ縺励∪縺・
        /// </summary>
        public void RefreshLayout() => RefreshInternal(false);



        /// <summary>
        /// 貂｡縺輔ｌ縺溘い繧､繝・Β荳隕ｧ縺ｫ蝓ｺ縺･縺・※陦ｨ遉ｺ蜀・ｮｹ繧呈峩譁ｰ縺励∪縺・
        /// </summary>
        /// <param name="itemsSource">繧｢繧､繝・Β荳隕ｧ.</param>
        private protected void SetItemsCore(IList<TCellData> itemsSource)
        {
            EnsureInitialized();

            ItemsSource = itemsSource ?? emptyItems;
            OnItemsSourceChanged(ItemsSource);

            RefreshItems();
        }

        void RefreshInternal(bool forceRefresh)
        {
            EnsureInitialized();
            OnBeforeRefresh();
            UpdatePositionInternal(currentPosition, forceRefresh);
        }

        protected void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            ValidateContainer();
            SetupContext(Context);
            ValidateCellPrefab();
            Initialize();
            InitializeCore();
            initialized = true;
        }

        void ValidateContainer()
        {
            if (cellContainer == null)
            {
                throw new InvalidOperationException(string.Format(
                    "{0} requires Cell Container.",
                    GetType().Name));
            }
        }

        /// <summary>
        /// 蛻晄悄蛹悶ｒ陦後＞縺ｾ縺・
        /// </summary>
        protected virtual void Initialize() { }

        /// <summary>
        /// 霑ｽ蜉縺ｮ蛻晄悄蛹門・逅・ｒ陦後＞縺ｾ縺・
        /// </summary>
        protected virtual void InitializeCore() { }

        void ValidateCellPrefab()
        {
            if (CellPrefab == null)
            {
                throw new InvalidOperationException(string.Format(
                    "{0} requires a cell prefab of type FancyCell<{1}, {2}>.",
                    GetType().Name,
                    typeof(TCellData).Name,
                    typeof(TContext).Name));
            }
        }



        private protected void UpdatePositionInternal(float position, bool forceRefresh)
        {
            currentPosition = position;

            var p = position - scrollOffset / cellInterval;
            var firstIndex = Mathf.CeilToInt(p);
            var firstPosition = (Mathf.Ceil(p) - p) * cellInterval;

            if (firstPosition + pool.Count * cellInterval < 1f)
            {
                ResizePool(firstPosition);
            }

            UpdateCells(firstPosition, firstIndex, forceRefresh);
        }

        /// <summary>
        /// 繧ｹ繧ｯ繝ｭ繝ｼ繝ｫ菴咲ｽｮ繧呈峩譁ｰ縺励∪縺・
        /// </summary>
        /// <param name="position">繧ｹ繧ｯ繝ｭ繝ｼ繝ｫ菴咲ｽｮ.</param>
        protected virtual void UpdatePosition(float position)
        {
            UpdatePositionInternal(position, false);
        }

        void ResizePool(float firstPosition)
        {
            var addCount = Mathf.CeilToInt((1f - firstPosition) / cellInterval) - pool.Count;
            for (var i = 0; i < addCount; i++)
            {
                var cell = Instantiate(CellPrefab, cellContainer);

#if UNITY_EDITOR
                if (editorPreviewing)
                {
                    MarkEditorPreviewObject(cell.gameObject);
                }
#endif

                cell.SetContext(Context);
                cell.Initialize();
                OnCellCreated(cell);

#if UNITY_EDITOR
                if (editorPreviewing)
                {
                    MarkEditorPreviewObject(cell.gameObject);
                }
#endif

                cell.SetVisible(false);
                pool.Add(cell);
            }
        }

        /// <summary>
        /// Destroys all pooled cells and resets initialization state.
        /// </summary>
        /// <param name="destroyImmediately">Use immediate destruction. This is required for edit-mode cleanup.</param>
        private protected void ClearCellPool(bool destroyImmediately)
        {
            for (var i = 0; i < pool.Count; i++)
            {
                var cell = pool[i];
                if (cell != null)
                {
                    DestroyGameObject(cell.gameObject, destroyImmediately);
                }
            }

            pool.Clear();
            initialized = false;
        }

        static void DestroyGameObject(GameObject gameObject, bool destroyImmediately)
        {
            if (gameObject == null)
            {
                return;
            }

            if (destroyImmediately)
            {
                DestroyImmediate(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void UpdateCells(float firstPosition, int firstIndex, bool forceRefresh)
        {
            for (var i = 0; i < pool.Count; i++)
            {
                var index = firstIndex + i;
                var position = firstPosition + i * cellInterval;
                var cell = pool[CircularIndex(index, pool.Count)];

                if (loop)
                {
                    index = CircularIndex(index, ItemsSource.Count);
                }

                if (index < 0 || index >= ItemsSource.Count || position > 1f)
                {
                    cell.SetVisible(false);
                    continue;
                }

                if (forceRefresh || cell.Index != index || !cell.IsVisible)
                {
                    cell.Index = index;
                    cell.SetVisible(true);
                    cell.UpdateContent(ItemsSource[index]);
                }

                cell.UpdatePosition(position);
            }
        }

        int CircularIndex(int i, int size) => size < 1 ? 0 : i < 0 ? size - 1 + (i + 1) % size : i % size;

#if UNITY_EDITOR
        internal override string GetEditorPreviewError()
        {
            if (Application.isPlaying)
            {
                return "Preview is only available in Edit Mode.";
            }

            var cellPrefabError = GetEditorPreviewCellPrefabError();
            if (!string.IsNullOrEmpty(cellPrefabError))
            {
                return cellPrefabError;
            }

            if (cellContainer == null)
            {
                return "Cell Container is not assigned.";
            }

            if (GetEditorPreviewItemCount() <= 0)
            {
                return "Preview Item Count must be greater than 0.";
            }

            return GetAdditionalEditorPreviewError();
        }

        internal override float GetEditorPreviewMaxPosition() => Mathf.Max(0, GetEditorPreviewItemCount() - 1);

        internal override void BeginEditorPreview()
        {
            if (editorPreviewing)
            {
                return;
            }

            itemsSourceBeforePreview = ItemsSource;
            initializedBeforePreview = initialized;
            loopBeforePreview = loop;
            cellIntervalBeforePreview = cellInterval;
            currentPositionBeforePreview = currentPosition;
            scrollOffsetBeforePreview = scrollOffset;
            cachedEditorPreviewItemCount = -1;
            editorPreviewing = true;

            OnPreviewBegin();

            ClearCellPool(true);
            ClearEditorPreviewObjects();
        }

        internal override void UpdateEditorPreview(float position, bool forceRefresh)
        {
            if (!editorPreviewing)
            {
                BeginEditorPreview();
            }

            if (forceRefresh)
            {
                ClearCellPool(true);
                ClearEditorPreviewObjects();
            }

            var itemCount = GetEditorPreviewItemCount();
            if (forceRefresh || itemCount != cachedEditorPreviewItemCount)
            {
                cachedEditorPreviewItemCount = itemCount;
                ApplyEditorPreviewItems(CreateEditorPreviewItems(itemCount));
            }

            ApplyEditorPreviewPosition(position, forceRefresh);
            MarkEditorPreviewCells();
        }

        internal override void EndEditorPreview()
        {
            if (!editorPreviewing)
            {
                return;
            }

            ClearCellPool(true);
            ClearEditorPreviewObjects();

            ItemsSource = itemsSourceBeforePreview ?? emptyItems;
            initialized = initializedBeforePreview && pool.Count > 0;
            loop = loopBeforePreview;
            cellInterval = cellIntervalBeforePreview;
            currentPosition = currentPositionBeforePreview;
            scrollOffset = scrollOffsetBeforePreview;
            cachedEditorPreviewItemCount = -1;
            editorPreviewing = false;

            OnPreviewEnd();
        }

        protected virtual string EditorPreviewCellDataTypeName => typeof(TCellData).Name;

        private protected virtual string GetEditorPreviewCellPrefabError()
        {
            return CellPrefab == null
                ? string.Format(
                    "Assign a cell prefab of type FancyCell<{0}, {1}>.",
                    EditorPreviewCellDataTypeName,
                    typeof(TContext).Name)
                : null;
        }

        protected abstract int GetEditorPreviewItemCount();

        protected abstract IList<TCellData> CreateEditorPreviewItems(int itemCount);

        private protected virtual string GetAdditionalEditorPreviewError() => null;

        private protected virtual void ApplyEditorPreviewItems(IList<TCellData> items) => SetItemsCore(items);

        private protected virtual void ApplyEditorPreviewPosition(float position, bool forceRefresh)
        {
            UpdatePositionInternal(position, forceRefresh);
        }

        protected virtual void OnPreviewBegin() { }

        protected virtual void OnPreviewEnd() { }

        private protected void MarkEditorPreviewObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }

            SetHideFlagsRecursively(gameObject.transform, FancyScrollViewBase.EditorPreviewHideFlags);
        }

        void MarkEditorPreviewCells()
        {
            for (var i = 0; i < pool.Count; i++)
            {
                var cell = pool[i];
                if (cell != null)
                {
                    MarkEditorPreviewObject(cell.gameObject);
                }
            }
        }

        void ClearEditorPreviewObjects()
        {
            if (cellContainer == null)
            {
                return;
            }

            for (var i = cellContainer.childCount - 1; i >= 0; i--)
            {
                var child = cellContainer.GetChild(i);
                if (IsEditorPreviewObject(child.gameObject))
                {
                    DestroyGameObject(child.gameObject, true);
                }
            }
        }

        static bool IsEditorPreviewObject(GameObject gameObject)
        {
            return (gameObject.hideFlags & FancyScrollViewBase.EditorPreviewHideFlags) ==
                FancyScrollViewBase.EditorPreviewHideFlags;
        }

        static void SetHideFlagsRecursively(Transform target, HideFlags hideFlags)
        {
            target.gameObject.hideFlags = hideFlags;

            for (var i = 0; i < target.childCount; i++)
            {
                SetHideFlagsRecursively(target.GetChild(i), hideFlags);
            }
        }

        bool cachedLoop;
        float cachedCellInterval, cachedScrollOffset;

        void LateUpdate()
        {
            if (editorPreviewing)
            {
                return;
            }

            if (cachedLoop != loop ||
                cachedCellInterval != cellInterval ||
                cachedScrollOffset != scrollOffset)
            {
                cachedLoop = loop;
                cachedCellInterval = cellInterval;
                cachedScrollOffset = scrollOffset;

                RefreshLayout();
            }
        }
#endif
    }

    /// <summary>
    /// 繧ｹ繧ｯ繝ｭ繝ｼ繝ｫ繝薙Η繝ｼ繧貞ｮ溯｣・☆繧九◆繧√・謚ｽ雎｡蝓ｺ蠎輔け繝ｩ繧ｹ.
    /// 辟｡髯舌せ繧ｯ繝ｭ繝ｼ繝ｫ縺翫ｈ縺ｳ繧ｹ繝翫ャ繝励↓蟇ｾ蠢懊＠縺ｦ縺・∪縺・
    /// <see cref="FancyScrollView{TItemData, TContext}.Context"/> 縺御ｸ崎ｦ√↑蝣ｴ蜷医・
    /// 莉｣繧上ｊ縺ｫ <see cref="FancyScrollView{TItemData}"/> 繧剃ｽｿ逕ｨ縺励∪縺・
    /// </summary>
    /// <typeparam name="TItemData">繧｢繧､繝・Β縺ｮ繝・・繧ｿ蝙・</typeparam>
    /// <typeparam name="TContext"><see cref="Context"/> 縺ｮ蝙・</typeparam>
    public abstract class FancyScrollView<TItemData, TContext> : FancyScrollViewCore<TItemData, TContext>
        where TContext : class, new()
    {
        /// <summary>
        /// Edit-mode preview item count.
        /// </summary>
        protected virtual int PreviewItemCount => EditorPreviewItemCount;

        /// <summary>
        /// 貂｡縺輔ｌ縺溘い繧､繝・Β荳隕ｧ縺ｫ蝓ｺ縺･縺・※陦ｨ遉ｺ蜀・ｮｹ繧呈峩譁ｰ縺励∪縺・
        /// </summary>
        /// <param name="items">繧｢繧､繝・Β荳隕ｧ.</param>
        public virtual void SetItems(IList<TItemData> items) => SetItemsCore(items);

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
    /// <see cref="FancyScrollView{TItemData}"/> 縺ｮ繧ｳ繝ｳ繝・く繧ｹ繝医け繝ｩ繧ｹ.
    /// </summary>
    public sealed class NullContext { }

    /// <summary>
    /// 繧ｹ繧ｯ繝ｭ繝ｼ繝ｫ繝薙Η繝ｼ繧貞ｮ溯｣・☆繧九◆繧√・謚ｽ雎｡蝓ｺ蠎輔け繝ｩ繧ｹ.
    /// 辟｡髯舌せ繧ｯ繝ｭ繝ｼ繝ｫ縺翫ｈ縺ｳ繧ｹ繝翫ャ繝励↓蟇ｾ蠢懊＠縺ｦ縺・∪縺・
    /// </summary>
    /// <typeparam name="TItemData"></typeparam>
    /// <seealso cref="FancyScrollView{TItemData, TContext}"/>
    public abstract class FancyScrollView<TItemData> : FancyScrollView<TItemData, NullContext> { }
}
