namespace ScreenManagementSample.Application
{
    /// <summary>
    /// マップリポジトリインターフェース
    /// </summary>
    public interface IMapRepository
    {
        /// <summary> マップを取得する </summary>
        Domain.GameMap Get();
    }
}
