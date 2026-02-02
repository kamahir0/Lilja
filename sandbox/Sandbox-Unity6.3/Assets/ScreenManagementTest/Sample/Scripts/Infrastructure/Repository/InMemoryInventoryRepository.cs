using ScreenManagementSample.Domain;

namespace ScreenManagementSample.Infrastructure
{
    /// <summary>
    /// インベントリのインメモリリポジトリ
    /// </summary>
    public class InMemoryInventoryRepository : Application.IInventoryRepository
    {
        private Inventory _inventory;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public InMemoryInventoryRepository()
        {
            // デフォルトのインベントリを生成（スロット数無制限）
            _inventory = new Inventory();
        }

        /// <inheritdoc />
        public Inventory Get() => _inventory;

        /// <inheritdoc />
        public void Save(Inventory inventory) => _inventory = inventory;
    }
}
