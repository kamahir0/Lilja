namespace ScreenManagementSample.Domain
{
    /// <summary>
    /// インタラクト可能なポイントを表すエンティティ
    /// </summary>
    public class InteractPoint
    {
        /// <summary> マップ上の位置 </summary>
        public Position Position { get; }

        /// <summary> 取得できるアイテムのID </summary>
        public ItemId ItemId { get; }

        /// <summary> 取得できるアイテム数 </summary>
        public int ItemCount { get; }

        /// <summary> インタラクト可能かどうか（使用済みならfalse） </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public InteractPoint(Position position, ItemId itemId, int itemCount = 1)
        {
            Position = position;
            ItemId = itemId;
            ItemCount = itemCount;
            IsActive = true;
        }

        /// <summary>
        /// インタラクトを実行する（使用済みにする）
        /// </summary>
        /// <returns>インタラクトが成功したらtrue</returns>
        public bool Interact()
        {
            if (!IsActive)
            {
                return false;
            }

            IsActive = false;
            return true;
        }

        /// <summary>
        /// リセットする（再度インタラクト可能にする）
        /// </summary>
        public void Reset()
        {
            IsActive = true;
        }
    }
}
