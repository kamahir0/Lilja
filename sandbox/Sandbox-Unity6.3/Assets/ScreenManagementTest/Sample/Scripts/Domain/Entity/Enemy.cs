namespace ScreenManagementSample.Domain
{
    /// <summary>
    /// 敵エンティティ
    /// </summary>
    public class Enemy
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

        /// <summary> 生存しているか </summary>
        public bool IsAlive => CurrentHp > 0;

        public Enemy(string name, int maxHp, int attack, int defense)
        {
            Name = name;
            MaxHp = maxHp;
            CurrentHp = maxHp;
            Attack = attack;
            Defense = defense;
        }

        /// <summary> ダメージを受ける </summary>
        public void TakeDamage(int damage)
        {
            var actualDamage = System.Math.Max(1, damage - Defense);
            CurrentHp = System.Math.Max(0, CurrentHp - actualDamage);
        }

        /// <summary> ステータスをリセットする </summary>
        public void Reset()
        {
            CurrentHp = MaxHp;
        }
    }
}
