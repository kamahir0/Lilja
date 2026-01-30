using System;
using Lilja.ScreenManagement;
using ScreenManagementSample.Domain;
using UnityEngine;

namespace ScreenManagementSample.Presentation
{
    /// <summary>
    /// アイテム選択Overlay（MVP - Presenter）
    /// TResult が null の場合はキャンセル（戻る）を意味する
    /// </summary>
    public class ItemSelectOverlay : PrefabOverlayBase<ValueTuple, InventoryItem>
    {
        [UnityView] private ItemSelectView _view;

        private readonly InventoryItem[] _items;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="items">選択可能なアイテム配列</param>
        public ItemSelectOverlay(InventoryItem[] items)
        {
            _items = items ?? Array.Empty<InventoryItem>();
        }

        protected override void OnViewLoaded()
        {
            // 動的にアイテムボタンを設定
            _view.SetItems(_items, OnItemSelected);
            _view.BackButton.onClick.AddListener(OnClickBack);

            _view.SetDescription("アイテムを選んでください");
        }

        protected override void OnViewUnloaded()
        {
            _view.ClearItemButtons();
            _view.BackButton.onClick.RemoveListener(OnClickBack);
        }

        private void OnItemSelected(InventoryItem item)
        {
            Debug.Log($"[ItemSelectOverlay] {item.Definition.Name}を選択");
            Close(item);
        }

        private void OnClickBack()
        {
            Debug.Log("[ItemSelectOverlay] キャンセル");
            Close(null); // nullを返してキャンセルを表現
        }
    }
}
