namespace ScreenManagementSample.Domain
{
    /// <summary>
    /// アイテムを一意に識別する値オブジェクト
    /// </summary>
    public readonly struct ItemId
    {
        /// <summary> 識別子の値 </summary>
        public string Value { get; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public ItemId(string value)
        {
            Value = value ?? string.Empty;
        }

        /// <summary>
        /// 文字列からの暗黙的変換
        /// </summary>
        public static implicit operator ItemId(string value) => new ItemId(value);

        /// <summary>
        /// 文字列への暗黙的変換
        /// </summary>
        public static implicit operator string(ItemId id) => id.Value;

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is ItemId other && Value == other.Value;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return Value?.GetHashCode() ?? 0;
        }

        /// <inheritdoc />
        public override string ToString() => Value;

        public static bool operator ==(ItemId left, ItemId right) => left.Equals(right);
        public static bool operator !=(ItemId left, ItemId right) => !left.Equals(right);
    }
}
