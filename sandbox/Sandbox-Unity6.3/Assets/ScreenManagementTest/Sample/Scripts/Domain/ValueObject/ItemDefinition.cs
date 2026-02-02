namespace ScreenManagementSample.Domain
{
    /// <summary>
    /// アイテム定義（マスターデータ）を表す値オブジェクト
    /// </summary>
    public sealed class ItemDefinition
    {
        /// <summary> アイテムID </summary>
        public ItemId Id { get; }

        /// <summary> アイテム名 </summary>
        public string Name { get; }

        /// <summary> アイテムの説明 </summary>
        public string Description { get; }

        /// <summary> アイテムの種類 </summary>
        public ItemType Type { get; }

        /// <summary> スタック可能かどうか </summary>
        public bool IsStackable { get; }

        /// <summary> 最大スタック数（スタック可能な場合） </summary>
        public int MaxStackCount { get; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public ItemDefinition(
            ItemId id,
            string name,
            string description,
            ItemType type,
            bool isStackable = true,
            int maxStackCount = 99)
        {
            Id = id;
            Name = name;
            Description = description;
            Type = type;
            IsStackable = isStackable;
            MaxStackCount = isStackable ? maxStackCount : 1;
        }
    }
}
