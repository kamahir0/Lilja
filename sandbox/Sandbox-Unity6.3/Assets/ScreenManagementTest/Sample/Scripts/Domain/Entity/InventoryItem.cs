namespace ScreenManagementSample.Domain
{
    /// <summary>
    /// 所持アイテムを表すエンティティ
    /// </summary>
    public class InventoryItem
    {
        /// <summary> アイテム定義 </summary>
        public ItemDefinition Definition { get; }

        /// <summary> 所持数 </summary>
        public int Count { get; private set; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public InventoryItem(ItemDefinition definition, int count = 1)
        {
            Definition = definition;
            Count = System.Math.Clamp(count, 1, definition.MaxStackCount);
        }

        /// <summary>
        /// 数量を追加する
        /// </summary>
        /// <param name="amount">追加する数量</param>
        /// <returns>実際に追加された数量</returns>
        public int Add(int amount)
        {
            if (amount <= 0 || !Definition.IsStackable)
            {
                return 0;
            }

            var spaceRemaining = Definition.MaxStackCount - Count;
            var actualAdded = System.Math.Min(amount, spaceRemaining);
            Count += actualAdded;
            return actualAdded;
        }

        /// <summary>
        /// 数量を減らす
        /// </summary>
        /// <param name="amount">減らす数量</param>
        /// <returns>実際に減らされた数量</returns>
        public int Remove(int amount)
        {
            if (amount <= 0)
            {
                return 0;
            }

            var actualRemoved = System.Math.Min(amount, Count);
            Count -= actualRemoved;
            return actualRemoved;
        }

        /// <summary>
        /// このスタックに追加できるかどうか
        /// </summary>
        public bool CanAdd(int amount)
        {
            return Definition.IsStackable && Count + amount <= Definition.MaxStackCount;
        }

        /// <summary>
        /// スタックが空かどうか
        /// </summary>
        public bool IsEmpty => Count <= 0;
    }
}
