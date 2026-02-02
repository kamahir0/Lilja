using System.Collections.Generic;
using System.Linq;

namespace ScreenManagementSample.Domain
{
    /// <summary>
    /// インベントリ全体を管理するエンティティ
    /// </summary>
    public class Inventory
    {
        private readonly List<InventoryItem> _items = new List<InventoryItem>();

        /// <summary> 所持アイテム一覧（読み取り専用） </summary>
        public IReadOnlyList<InventoryItem> Items => _items;

        /// <summary> 最大スロット数（0以下の場合は無制限） </summary>
        public int MaxSlots { get; }

        /// <summary> 現在の使用スロット数 </summary>
        public int UsedSlots => _items.Count;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="maxSlots">最大スロット数（0以下で無制限）</param>
        public Inventory(int maxSlots = 0)
        {
            MaxSlots = maxSlots;
        }

        /// <summary>
        /// アイテムを追加する
        /// </summary>
        /// <param name="definition">追加するアイテムの定義</param>
        /// <param name="count">追加する数量</param>
        /// <returns>実際に追加された数量</returns>
        public int AddItem(ItemDefinition definition, int count = 1)
        {
            if (count <= 0)
            {
                return 0;
            }

            var remaining = count;

            // スタック可能な場合、既存のスタックに追加を試みる
            if (definition.IsStackable)
            {
                var existingItems = _items.Where(i => i.Definition.Id == definition.Id).ToList();
                foreach (var existingItem in existingItems)
                {
                    if (remaining <= 0)
                    {
                        break;
                    }

                    var added = existingItem.Add(remaining);
                    remaining -= added;
                }
            }

            // 残りがあれば新しいスロットに追加
            while (remaining > 0)
            {
                // スロット制限チェック
                if (MaxSlots > 0 && _items.Count >= MaxSlots)
                {
                    break;
                }

                var amountToAdd = System.Math.Min(remaining, definition.MaxStackCount);
                _items.Add(new InventoryItem(definition, amountToAdd));
                remaining -= amountToAdd;
            }

            return count - remaining;
        }

        /// <summary>
        /// アイテムを削除する
        /// </summary>
        /// <param name="itemId">削除するアイテムのID</param>
        /// <param name="count">削除する数量</param>
        /// <returns>実際に削除された数量</returns>
        public int RemoveItem(ItemId itemId, int count = 1)
        {
            if (count <= 0)
            {
                return 0;
            }

            var remaining = count;
            var itemsToRemove = new List<InventoryItem>();

            foreach (var item in _items.Where(i => i.Definition.Id == itemId))
            {
                if (remaining <= 0)
                {
                    break;
                }

                var removed = item.Remove(remaining);
                remaining -= removed;

                if (item.IsEmpty)
                {
                    itemsToRemove.Add(item);
                }
            }

            // 空になったスロットを削除
            foreach (var item in itemsToRemove)
            {
                _items.Remove(item);
            }

            return count - remaining;
        }

        /// <summary>
        /// 指定したアイテムの所持数を取得する
        /// </summary>
        public int GetItemCount(ItemId itemId)
        {
            return _items.Where(i => i.Definition.Id == itemId).Sum(i => i.Count);
        }

        /// <summary>
        /// 指定したアイテムを所持しているか確認する
        /// </summary>
        public bool HasItem(ItemId itemId, int count = 1)
        {
            return GetItemCount(itemId) >= count;
        }

        /// <summary>
        /// 指定したアイテムIDのInventoryItemを取得する（最初に見つかったもの）
        /// </summary>
        public InventoryItem GetItem(ItemId itemId)
        {
            return _items.FirstOrDefault(i => i.Definition.Id == itemId);
        }

        /// <summary>
        /// インベントリをクリアする
        /// </summary>
        public void Clear()
        {
            _items.Clear();
        }
    }
}
