namespace ScreenManagementSample.Domain
{
    /// <summary>
    /// エンカウント判定を提供するドメインサービス
    /// </summary>
    public class EncounterService
    {
        private readonly System.Random _random = new();

        /// <summary> エンカウント率（0.0〜1.0） </summary>
        public float EncounterRate { get; set; } = 0.15f;

        // 敵テンプレート
        private static readonly (string Name, int MaxHp, int Attack, int Defense)[] EnemyTemplates =
        {
            ("スライム", 30, 8, 2),
            ("ゴブリン", 45, 12, 4),
            ("コウモリ", 20, 10, 1),
        };

        /// <summary>
        /// エンカウント判定を行う
        /// </summary>
        /// <returns>エンカウントしたらtrue</returns>
        public bool CheckEncounter()
        {
            return _random.NextDouble() < EncounterRate;
        }

        /// <summary>
        /// エンカウントする敵を生成する（1〜3体）
        /// </summary>
        public Enemy[] GenerateEnemies()
        {
            var count = _random.Next(1, 4); // 1〜3体
            var enemies = new Enemy[count];

            for (int i = 0; i < count; i++)
            {
                var template = EnemyTemplates[_random.Next(EnemyTemplates.Length)];
                var suffix = count > 1 ? $" {(char)('A' + i)}" : "";
                enemies[i] = new Enemy(
                    name: template.Name + suffix,
                    maxHp: template.MaxHp,
                    attack: template.Attack,
                    defense: template.Defense
                );
            }

            return enemies;
        }
    }
}

