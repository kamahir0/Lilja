using ScreenManagementSample.Domain;

namespace ScreenManagementSample.Application
{
    /// <summary>
    /// ゲーム全体のサービスロケーター（DIコンテナの代替）
    /// シンプルにするため静的クラスで実装
    /// </summary>
    public static class GameServices
    {
        /// <summary> プレイヤーリポジトリ </summary>
        public static IPlayerRepository PlayerRepository { get; private set; }

        /// <summary> マップリポジトリ </summary>
        public static IMapRepository MapRepository { get; private set; }

        /// <summary> アイテム定義リポジトリ </summary>
        public static IItemDefinitionRepository ItemDefinitionRepository { get; private set; }

        /// <summary> インベントリリポジトリ </summary>
        public static IInventoryRepository InventoryRepository { get; private set; }

        /// <summary> インタラクトポイントリポジトリ </summary>
        public static IInteractPointRepository InteractPointRepository { get; private set; }

        /// <summary> バトルサービス </summary>
        public static BattleService BattleService { get; private set; }

        /// <summary> エンカウントサービス </summary>
        public static EncounterService EncounterService { get; private set; }

        /// <summary>
        /// サービスを初期化する
        /// </summary>
        public static void Initialize()
        {
            PlayerRepository = new Infrastructure.InMemoryPlayerRepository();
            MapRepository = new Infrastructure.InMemoryMapRepository();
            ItemDefinitionRepository = new Infrastructure.InMemoryItemDefinitionRepository();
            InventoryRepository = new Infrastructure.InMemoryInventoryRepository();
            InteractPointRepository = new Infrastructure.InMemoryInteractPointRepository();
            BattleService = new BattleService();
            EncounterService = new EncounterService();
        }

        /// <summary>
        /// ゲームをリセットする（タイトルに戻る際に呼ぶ）
        /// </summary>
        public static void Reset()
        {
            var player = PlayerRepository.Get();
            var map = MapRepository.Get();
            player.Reset(new Position(0, 0));

            // インベントリをクリア
            var inventory = InventoryRepository.Get();
            inventory.Clear();

            // インタラクトポイントをリセット（再生成）
            InteractPointRepository.Reset();
        }
    }
}

