using ScreenManagementSample.Domain;

namespace ScreenManagementSample.Infrastructure
{
    /// <summary>
    /// インメモリプレイヤーリポジトリ
    /// </summary>
    public class InMemoryPlayerRepository : Application.IPlayerRepository
    {
        private Player _player;

        public InMemoryPlayerRepository()
        {
            // デフォルトのプレイヤーを生成
            _player = new Player(
                name: "勇者",
                maxHp: 100,
                attack: 15,
                defense: 5,
                initialPosition: new Position(0, 0)
            );
        }

        public Player Get() => _player;

        public void Save(Player player) => _player = player;
    }
}
