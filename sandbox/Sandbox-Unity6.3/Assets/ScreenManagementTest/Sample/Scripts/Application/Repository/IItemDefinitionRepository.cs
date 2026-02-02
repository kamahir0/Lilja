namespace ScreenManagementSample.Application
{
    /// <summary>
    /// アイテム定義リポジトリインターフェース
    /// </summary>
    public interface IItemDefinitionRepository
    {
        /// <summary> IDを指定してアイテム定義を取得する </summary>
        Domain.ItemDefinition Get(Domain.ItemId id);

        /// <summary> 全てのアイテム定義を取得する </summary>
        Domain.ItemDefinition[] GetAll();
    }
}
