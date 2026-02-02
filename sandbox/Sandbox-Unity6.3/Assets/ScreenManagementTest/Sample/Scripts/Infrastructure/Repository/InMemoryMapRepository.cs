using ScreenManagementSample.Domain;

namespace ScreenManagementSample.Infrastructure
{
    /// <summary>
    /// インメモリマップリポジトリ
    /// </summary>
    public class InMemoryMapRepository : Application.IMapRepository
    {
        private readonly GameMap _map;

        public InMemoryMapRepository()
        {
            // 5x5のグリッドマップ
            _map = new GameMap(width: 5, height: 5);
        }

        public GameMap Get() => _map;
    }
}
