namespace ScreenManagementSample.Application
{
    /// <summary>
    /// インベントリリポジトリインターフェース
    /// </summary>
    public interface IInventoryRepository
    {
        /// <summary> インベントリを取得する </summary>
        Domain.Inventory Get();

        /// <summary> インベントリを保存する </summary>
        void Save(Domain.Inventory inventory);
    }
}
