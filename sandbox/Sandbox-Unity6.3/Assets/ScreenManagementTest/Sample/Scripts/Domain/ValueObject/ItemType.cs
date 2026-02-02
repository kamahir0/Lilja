namespace ScreenManagementSample.Domain
{
    /// <summary>
    /// アイテムの種類
    /// </summary>
    public enum ItemType
    {
        /// <summary> 消費アイテム（使用すると消費される） </summary>
        Consumable,

        /// <summary> 装備品（装備して効果を発揮） </summary>
        Equipment,

        /// <summary> 素材（合成やクラフトに使用） </summary>
        Material,

        /// <summary> キーアイテム（ストーリー進行などに必要、消費しない） </summary>
        KeyItem
    }
}
