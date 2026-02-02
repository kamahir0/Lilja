namespace ScreenManagementSample.Domain
{
    /// <summary>
    /// プレイヤーエンティティ
    /// </summary>
    public class Player
    {
        /// <summary> 名前 </summary>
        public string Name { get; }

        /// <summary> 最大HP </summary>
        public int MaxHp { get; }

        /// <summary> 現在HP </summary>
        public int CurrentHp { get; private set; }

        /// <summary> 攻撃力 </summary>
        public int Attack { get; }

        /// <summary> 防御力 </summary>
        public int Defense { get; }

        /// <summary> マップ上の位置 </summary>
        public Position Position { get; private set; }

        /// <summary> 生存しているか </summary>
        public bool IsAlive => CurrentHp > 0;

        public Player(string name, int maxHp, int attack, int defense, Position initialPosition)
        {
            Name = name;
            MaxHp = maxHp;
            CurrentHp = maxHp;
            Attack = attack;
            Defense = defense;
            Position = initialPosition;
        }

        /// <summary> ダメージを受ける </summary>
        public void TakeDamage(int damage)
        {
            var actualDamage = System.Math.Max(1, damage - Defense);
            CurrentHp = System.Math.Max(0, CurrentHp - actualDamage);
        }

        /// <summary> 回復する </summary>
        public void Heal(int amount)
        {
            CurrentHp = System.Math.Min(MaxHp, CurrentHp + amount);
        }

        /// <summary> 移動する </summary>
        public void MoveTo(Position newPosition)
        {
            Position = newPosition;
        }

        /// <summary> ステータスをリセットする（新規ゲーム開始時） </summary>
        public void Reset(Position initialPosition)
        {
            CurrentHp = MaxHp;
            Position = initialPosition;
        }
    }
}
