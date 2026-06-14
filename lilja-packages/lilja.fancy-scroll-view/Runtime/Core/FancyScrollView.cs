/*
 * FancyScrollView (https://github.com/setchi/FancyScrollView)
 * Copyright (c) 2020 setchi
 * Licensed under MIT (https://github.com/setchi/FancyScrollView/blob/master/LICENSE)
 */

using System.Collections.Generic;
using UnityEngine;

namespace Lilja.FancyScrollView
{
#if UNITY_EDITOR
    internal interface IFancyScrollViewPreview
    {
        bool EditorPreviewing { get; }
        bool SupportsEditorPreview { get; }
        string GetEditorPreviewError();
        float GetEditorPreviewMaxPosition();
        void BeginEditorPreview();
        void UpdateEditorPreview(float position, bool forceRefresh);
        void EndEditorPreview();
    }
#endif

    internal static class FancyScrollTemplateUtility
    {
        public static bool IsSceneObjectTemplate(GameObject template)
        {
            return template != null && template.scene.IsValid();
        }

        public static void HideSceneObjectTemplate(GameObject template)
        {
            if (IsSceneObjectTemplate(template))
            {
                template.SetActive(false);
            }
        }
    }

    /// <summary>
    /// スクロールビューを実装するための抽象基底クラス.
    /// 無限スクロールおよびスナップに対応しています.
    /// <see cref="FancyScrollView{TItemData, TContext}.Context"/> が不要な場合は
    /// 代わりに <see cref="FancyScrollView{TItemData}"/> を使用します.
    /// </summary>
    /// <typeparam name="TItemData">アイテムのデータ型.</typeparam>
    /// <typeparam name="TContext"><see cref="Context"/> の型.</typeparam>
    public abstract class FancyScrollView<TItemData, TContext> : MonoBehaviour
#if UNITY_EDITOR
        , IFancyScrollViewPreview
#endif
        where TContext : class, new()
    {
        /// <summary>
        /// セル同士の間隔.
        /// </summary>
        [SerializeField, Range(1e-2f, 1f)] protected float cellInterval = 0.2f;

        /// <summary>
        /// スクロール位置の基準.
        /// </summary>
        /// <remarks>
        /// たとえば、 <c>0.5</c> を指定してスクロール位置が <c>0</c> の場合, 中央に最初のセルが配置されます.
        /// </remarks>
        [SerializeField, Range(0f, 1f)] protected float scrollOffset = 0.5f;

        /// <summary>
        /// セルを循環して配置させるどうか.
        /// </summary>
        /// <remarks>
        /// <c>true</c> にすると最後のセルの後に最初のセル, 最初のセルの前に最後のセルが並ぶようになります.
        /// 無限スクロールを実装する場合は <c>true</c> を指定します.
        /// </remarks>
        [SerializeField] protected bool loop = false;

        /// <summary>
        /// セルの親要素となる <c>Transform</c>.
        /// </summary>
        [SerializeField] protected Transform cellContainer = default;

#if UNITY_EDITOR
        [SerializeField, Min(1)] int editorPreviewItemCount = 30;
#endif

        readonly IList<FancyCell<TItemData, TContext>> pool = new List<FancyCell<TItemData, TContext>>();
        readonly IList<TItemData> emptyItems = new List<TItemData>();

        /// <summary>
        /// 初期化済みかどうか.
        /// </summary>
        protected bool initialized;

        /// <summary>
        /// 現在のスクロール位置.
        /// </summary>
        protected float currentPosition;

        /// <summary>
        /// セルのテンプレート.
        /// </summary>
        /// <remarks>
        /// Prefab asset または Hierarchy 上のセルオブジェクトを指定できます.
        /// Hierarchy 上のオブジェクトを指定した場合, テンプレート自身は実行時とプレビュー時に自動で非表示になります.
        /// </remarks>
        protected abstract FancyCell<TItemData, TContext> CellPrefab { get; }

        /// <summary>
        /// アイテム一覧のデータ.
        /// </summary>
        protected IList<TItemData> ItemsSource { get; set; } = new List<TItemData>();

        /// <summary>
        /// <typeparamref name="TContext"/> のインスタンス.
        /// セルとスクロールビュー間で同じインスタンスが共有されます. 情報の受け渡しや状態の保持に使用します.
        /// </summary>
        protected TContext Context { get; } = new TContext();

        /// <summary>
        /// 初期化を行います.
        /// </summary>
        /// <remarks>
        /// 最初にセルが生成される直前に呼び出されます.
        /// </remarks>
        protected virtual void Initialize() { }

        /// <summary>
        /// 渡されたアイテム一覧に基づいて表示内容を更新します.
        /// </summary>
        /// <param name="itemsSource">アイテム一覧.</param>
        protected virtual void UpdateContents(IList<TItemData> itemsSource)
        {
            ItemsSource = itemsSource ?? emptyItems;
            Refresh();
        }

        /// <summary>
        /// セルのレイアウトを強制的に更新します.
        /// </summary>
        protected virtual void Relayout() => UpdatePosition(currentPosition, false);

        /// <summary>
        /// セルのレイアウトと表示内容を強制的に更新します.
        /// </summary>
        protected virtual void Refresh() => UpdatePosition(currentPosition, true);

        /// <summary>
        /// スクロール位置を更新します.
        /// </summary>
        /// <param name="position">スクロール位置.</param>
        protected virtual void UpdatePosition(float position) => UpdatePosition(position, false);

        void UpdatePosition(float position, bool forceRefresh)
        {
            if (!initialized)
            {
                Initialize();
                initialized = true;
            }

            HideCellTemplateIfNeeded();

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

        void ResizePool(float firstPosition)
        {
            Debug.Assert(CellPrefab != null);
            Debug.Assert(cellContainer != null);

            HideCellTemplateIfNeeded();

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
                cell.SetVisible(false);
                pool.Add(cell);
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

        void HideCellTemplateIfNeeded()
        {
            if (CellPrefab != null)
            {
                FancyScrollTemplateUtility.HideSceneObjectTemplate(CellPrefab.gameObject);
            }
        }

        int CircularIndex(int i, int size) => size < 1 ? 0 : i < 0 ? size - 1 + (i + 1) % size : i % size;

#if UNITY_EDITOR
        const HideFlags EditorPreviewHideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

        IList<TItemData> itemsSourceBeforePreview;
        bool initializedBeforePreview;
        bool loopBeforePreview;
        bool editorPreviewing;
        float cellIntervalBeforePreview;
        float currentPositionBeforePreview;
        float scrollOffsetBeforePreview;
        int cachedEditorPreviewItemCount = -1;
        GameObject editorPreviewSceneCellTemplate;
        bool editorPreviewSceneCellTemplateActiveSelf;
        bool editorPreviewSceneCellTemplateStateStored;

        bool IFancyScrollViewPreview.EditorPreviewing => editorPreviewing;

        bool IFancyScrollViewPreview.SupportsEditorPreview => SupportsEditorPreviewData();

        /// <summary>
        /// Edit-mode preview item count.
        /// </summary>
        protected int EditorPreviewItemCount => Mathf.Max(1, editorPreviewItemCount);

        internal bool IsEditorPreviewing => editorPreviewing;

        string IFancyScrollViewPreview.GetEditorPreviewError()
        {
            if (Application.isPlaying)
            {
                return "Preview is only available in Edit Mode.";
            }

            if (CellPrefab == null)
            {
                return string.Format(
                    "Assign a cell prefab of type FancyCell<{0}, {1}>.",
                    typeof(TItemData).Name,
                    typeof(TContext).Name);
            }

            if (cellContainer == null)
            {
                return "Cell Container is not assigned.";
            }

            if (EditorPreviewItemCount <= 0)
            {
                return "Preview Item Count must be greater than 0.";
            }

            return GetAdditionalEditorPreviewError();
        }

        float IFancyScrollViewPreview.GetEditorPreviewMaxPosition() => GetEditorPreviewMaxPosition();

        void IFancyScrollViewPreview.BeginEditorPreview()
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

            StoreAndHideEditorPreviewCellTemplate();
            OnEditorPreviewBegin();

            ClearCellPool(true);
            ClearEditorPreviewObjects();
        }

        void IFancyScrollViewPreview.UpdateEditorPreview(float position, bool forceRefresh)
        {
            if (!editorPreviewing)
            {
                ((IFancyScrollViewPreview)this).BeginEditorPreview();
            }

            if (forceRefresh)
            {
                ClearCellPool(true);
                ClearEditorPreviewObjects();
            }

            var itemCount = EditorPreviewItemCount;
            if (forceRefresh || itemCount != cachedEditorPreviewItemCount)
            {
                cachedEditorPreviewItemCount = itemCount;
                UpdateContents(CreateEditorPreviewItems(itemCount));
            }

            ApplyEditorPreviewPosition(position, forceRefresh);
            MarkEditorPreviewCells();
        }

        void IFancyScrollViewPreview.EndEditorPreview()
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

            RestoreEditorPreviewCellTemplate();
            OnEditorPreviewEnd();
        }

        protected virtual bool TryCreatePreviewItem(FancyScrollPreviewItemContext context, out TItemData item)
        {
            item = default;
            return false;
        }

        protected virtual bool SupportsEditorPreviewData()
        {
            return TryCreatePreviewItem(new FancyScrollPreviewItemContext(0, EditorPreviewItemCount), out _);
        }

        protected virtual string GetAdditionalEditorPreviewError() => null;

        protected virtual float GetEditorPreviewMaxPosition() => Mathf.Max(0, EditorPreviewItemCount - 1);

        protected virtual void ApplyEditorPreviewPosition(float position, bool forceRefresh)
        {
            UpdatePosition(position, forceRefresh);
        }

        protected virtual void OnEditorPreviewBegin() { }

        protected virtual void OnEditorPreviewEnd() { }

        protected virtual IList<TItemData> CreateEditorPreviewItems(int itemCount)
        {
            var items = new List<TItemData>(itemCount);
            for (var i = 0; i < itemCount; i++)
            {
                if (!TryCreatePreviewItem(new FancyScrollPreviewItemContext(i, itemCount), out var item))
                {
                    return emptyItems;
                }

                items.Add(item);
            }

            return items;
        }

        void ClearCellPool(bool destroyImmediately)
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

        void StoreAndHideEditorPreviewCellTemplate()
        {
            if (CellPrefab == null || !FancyScrollTemplateUtility.IsSceneObjectTemplate(CellPrefab.gameObject))
            {
                return;
            }

            editorPreviewSceneCellTemplate = CellPrefab.gameObject;
            editorPreviewSceneCellTemplateActiveSelf = editorPreviewSceneCellTemplate.activeSelf;
            editorPreviewSceneCellTemplateStateStored = true;
            editorPreviewSceneCellTemplate.SetActive(false);
        }

        void RestoreEditorPreviewCellTemplate()
        {
            if (!editorPreviewSceneCellTemplateStateStored)
            {
                return;
            }

            if (editorPreviewSceneCellTemplate != null)
            {
                editorPreviewSceneCellTemplate.SetActive(editorPreviewSceneCellTemplateActiveSelf);
            }

            editorPreviewSceneCellTemplate = null;
            editorPreviewSceneCellTemplateActiveSelf = false;
            editorPreviewSceneCellTemplateStateStored = false;
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

        protected void MarkEditorPreviewObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }

            SetHideFlagsRecursively(gameObject.transform, EditorPreviewHideFlags);
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
            return (gameObject.hideFlags & EditorPreviewHideFlags) == EditorPreviewHideFlags;
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

                Relayout();
            }
        }
#endif
    }

    /// <summary>
    /// <see cref="FancyScrollView{TItemData}"/> のコンテキストクラス.
    /// </summary>
    public sealed class NullContext { }

    /// <summary>
    /// スクロールビューを実装するための抽象基底クラス.
    /// 無限スクロールおよびスナップに対応しています.
    /// </summary>
    /// <typeparam name="TItemData"></typeparam>
    /// <seealso cref="FancyScrollView{TItemData, TContext}"/>
    public abstract class FancyScrollView<TItemData> : FancyScrollView<TItemData, NullContext> { }
}
