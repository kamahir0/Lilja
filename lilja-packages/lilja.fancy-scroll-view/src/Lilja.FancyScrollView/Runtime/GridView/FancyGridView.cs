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
    /// グリッドレイアウトのスクロールビューを実装するための抽象基底クラス.
    /// 無限スクロールおよびスナップには対応していません.
    /// <see cref="FancyScrollView{TItemData, TContext}.Context"/> が不要な場合は
    /// 代わりに <see cref="FancyGridView{TItemData}"/> を使用します.
    /// </summary>
    /// <typeparam name="TItemData">アイテムのデータ型.</typeparam>
    /// <typeparam name="TContext"><see cref="FancyScrollView{TItemData, TContext}.Context"/> の型.</typeparam>
    public abstract class FancyGridView<TItemData, TContext> : FancyScrollRect<TItemData[], TContext>
        where TContext : class, IFancyGridViewContext, new()
    {
        /// <summary>
        /// デフォルトのセルグループクラス.
        /// </summary>
        protected abstract class DefaultCellGroup : FancyCellGroup<TItemData, TContext> { }

        /// <summary>
        /// 最初にセルを配置する軸方向のセル同士の余白.
        /// </summary>
        [SerializeField] protected float startAxisSpacing = 0f;

        /// <summary>
        /// 最初にセルを配置する軸方向のセル数.
        /// </summary>
        [SerializeField] protected int startAxisCellCount = 4;

        /// <summary>
        /// セルのサイズ.
        /// </summary>
        [SerializeField] protected Vector2 cellSize = new Vector2(100f, 100f);

        /// <summary>
        /// セルのグループ Prefab.
        /// </summary>
        /// <remarks>
        /// <see cref="FancyGridView{TItemData, TContext}"/> では,
        /// <see cref="FancyScrollView{TItemData, TContext}.CellPrefab"/> を最初にセルを配置する軸方向のセルコンテナとして使用します.
        /// </remarks>
        protected sealed override FancyCell<TItemData[], TContext> CellPrefab => cellGroupTemplate;

        /// <inheritdoc/>
        protected override float CellSize => Scroller.ScrollDirection == ScrollDirection.Horizontal
            ? cellSize.x
            : cellSize.y;

        /// <summary>
        /// アイテムの総数.
        /// </summary>
        public int DataCount { get; private set; }

        FancyCell<TItemData[], TContext> cellGroupTemplate;

#if UNITY_EDITOR
        GameObject editorPreviewSceneCellTemplate;
        bool editorPreviewSceneCellTemplateActiveSelf;
        bool editorPreviewSceneCellTemplateStateStored;
#endif

        /// <inheritdoc/>
        protected override void Initialize()
        {
            base.Initialize();

            Debug.Assert(startAxisCellCount > 0);

            Context.ScrollDirection = Scroller.ScrollDirection;
            Context.GetGroupCount = () => Mathf.Max(1, startAxisCellCount);
            Context.GetStartAxisSpacing = () => startAxisSpacing;
            Context.GetCellSize = () => Scroller.ScrollDirection == ScrollDirection.Horizontal
                ? cellSize.y
                : cellSize.x;

            SetupCellTemplate();
        }

        /// <summary>
        /// 最初にセルが生成される直前に呼び出されます.
        /// <see cref="Setup{TGroup}(FancyCell{TItemData, TContext})"/> メソッドを使用してセルテンプレートのセットアップを行ってください.
        /// </summary>
        /// <example>
        /// <code><![CDATA[
        /// using UnityEngine;
        /// using Lilja.FancyScrollView;
        ///
        /// public class MyGridView : FancyGridView<ItemData, Context>
        /// {
        ///     class CellGroup : DefaultCellGroup { }
        ///
        ///     [SerializeField] Cell cellPrefab = default;
        ///
        ///     protected override void SetupCellTemplate() => Setup<CellGroup>(cellPrefab);
        /// }
        /// ]]></code>
        /// </example>
        protected abstract void SetupCellTemplate();

        /// <summary>
        /// セルテンプレートのセットアップを行います.
        /// </summary>
        /// <param name="cellTemplate">セルのテンプレート.</param>
        /// <typeparam name="TGroup">セルグループの型.</typeparam>
        /// <remarks>
        /// <paramref name="cellTemplate"/> は Prefab asset または Hierarchy 上のセルオブジェクトを指定できます.
        /// Hierarchy 上のオブジェクトを指定した場合, テンプレート自身は実行時とプレビュー時に自動で非表示になります.
        /// </remarks>
        protected virtual void Setup<TGroup>(FancyCell<TItemData, TContext> cellTemplate)
            where TGroup : FancyCell<TItemData[], TContext>
        {
            HideCellTemplateIfNeeded(cellTemplate);
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

        void HideCellTemplateIfNeeded(FancyCell<TItemData, TContext> cellTemplate)
        {
            if (cellTemplate == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (IsEditorPreviewing)
            {
                StoreAndHideEditorPreviewCellTemplate(cellTemplate.gameObject);
                return;
            }
#endif

            FancyScrollTemplateUtility.HideSceneObjectTemplate(cellTemplate.gameObject);
        }

        /// <summary>
        /// 渡されたアイテム一覧に基づいて表示内容を更新します.
        /// </summary>
        /// <param name="items">アイテム一覧.</param>
        public virtual void UpdateContents(IList<TItemData> items)
        {
            DataCount = items.Count;
            base.UpdateContents(CreateGroups(items));
        }

        IList<TItemData[]> CreateGroups(IList<TItemData> items)
        {
            var groupSize = Mathf.Max(1, startAxisCellCount);
            return items
                .Select((item, index) => (item, index))
                .GroupBy(
                    x => x.index / groupSize,
                    x => x.item)
                .Select(group => group.ToArray())
                .ToArray();
        }

        /// <summary>
        /// 指定したアイテムの位置までジャンプします.
        /// </summary>
        /// <param name="itemIndex">アイテムのインデックス.</param>
        /// <param name="alignment">ビューポート内におけるセル位置の基準. 0f(先頭) ~ 1f(末尾).</param>
        protected override void JumpTo(int itemIndex, float alignment = 0.5f)
        {
            var groupIndex = itemIndex / Mathf.Max(1, startAxisCellCount);
            base.JumpTo(groupIndex, alignment);
        }

        /// <summary>
        /// 指定したアイテムの位置まで移動します.
        /// </summary>
        /// <param name="itemIndex">アイテムのインデックス.</param>
        /// <param name="duration">移動にかける秒数.</param>
        /// <param name="alignment">ビューポート内におけるセル位置の基準. 0f(先頭) ~ 1f(末尾).</param>
        /// <param name="onComplete">移動が完了した際に呼び出されるコールバック.</param>
        protected override void ScrollTo(int itemIndex, float duration, float alignment = 0.5f, Action onComplete = null)
        {
            var groupIndex = itemIndex / Mathf.Max(1, startAxisCellCount);
            base.ScrollTo(groupIndex, duration, alignment, onComplete);
        }

        /// <summary>
        /// 指定したアイテムの位置まで移動します.
        /// </summary>
        /// <param name="itemIndex">アイテムのインデックス.</param>
        /// <param name="duration">移動にかける秒数.</param>
        /// <param name="easing">移動に使用するイージング.</param>
        /// <param name="alignment">ビューポート内におけるセル位置の基準. 0f(先頭) ~ 1f(末尾).</param>
        /// <param name="onComplete">移動が完了した際に呼び出されるコールバック.</param>
        protected override void ScrollTo(int itemIndex, float duration, Ease easing, float alignment = 0.5f, Action onComplete = null)
        {
            var groupIndex = itemIndex / Mathf.Max(1, startAxisCellCount);
            base.ScrollTo(groupIndex, duration, easing, alignment, onComplete);
        }

#if UNITY_EDITOR
        protected virtual bool TryCreatePreviewItem(FancyScrollPreviewItemContext context, out TItemData item)
        {
            item = default;
            return false;
        }

        protected override bool SupportsEditorPreviewData()
        {
            return TryCreatePreviewItem(new FancyScrollPreviewItemContext(0, EditorPreviewItemCount), out _);
        }

        protected override IList<TItemData[]> CreateEditorPreviewItems(int itemCount)
        {
            var previewItems = new List<TItemData>(itemCount);
            for (var i = 0; i < itemCount; i++)
            {
                if (!TryCreatePreviewItem(new FancyScrollPreviewItemContext(i, itemCount), out var item))
                {
                    return Array.Empty<TItemData[]>();
                }

                previewItems.Add(item);
            }

            DataCount = previewItems.Count;
            return CreateGroups(previewItems);
        }

        protected override float GetEditorPreviewMaxPosition() => Mathf.Max(0, EditorPreviewItemCount - 1);

        protected override void ApplyEditorPreviewPosition(float position, bool forceRefresh)
        {
            var groupSize = Mathf.Max(1, startAxisCellCount);
            base.ApplyEditorPreviewPosition(position / groupSize, forceRefresh);
        }

        protected override void OnEditorPreviewEnd()
        {
            if (cellGroupTemplate != null)
            {
                DestroyImmediate(cellGroupTemplate.gameObject);
            }

            cellGroupTemplate = null;
            RestoreEditorPreviewCellTemplate();
            base.OnEditorPreviewEnd();
        }

        void StoreAndHideEditorPreviewCellTemplate(GameObject cellTemplate)
        {
            if (!FancyScrollTemplateUtility.IsSceneObjectTemplate(cellTemplate))
            {
                return;
            }

            if (!editorPreviewSceneCellTemplateStateStored || editorPreviewSceneCellTemplate != cellTemplate)
            {
                editorPreviewSceneCellTemplate = cellTemplate;
                editorPreviewSceneCellTemplateActiveSelf = cellTemplate.activeSelf;
                editorPreviewSceneCellTemplateStateStored = true;
            }

            cellTemplate.SetActive(false);
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
#endif
    }

    /// <summary>
    /// グリッドレイアウトのスクロールビューを実装するための抽象基底クラス.
    /// 無限スクロールおよびスナップには対応していません.
    /// </summary>
    /// <typeparam name="TItemData">アイテムのデータ型.</typeparam>
    /// <seealso cref="FancyGridView{TItemData, TContext}"/>
    public abstract class FancyGridView<TItemData> : FancyGridView<TItemData, FancyGridViewContext> { }
}
