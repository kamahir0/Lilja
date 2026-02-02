namespace ScreenManagementSample.Application
{
    /// <summary>
    /// プレイヤーリポジトリインターフェース
    /// </summary>
    public interface IPlayerRepository
    {
        /// <summary> プレイヤーを取得する </summary>
        Domain.Player Get();

        /// <summary> プレイヤーを保存する </summary>
        void Save(Domain.Player player);
    }
}
