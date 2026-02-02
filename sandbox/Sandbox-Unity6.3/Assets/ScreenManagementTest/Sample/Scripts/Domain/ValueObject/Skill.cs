namespace ScreenManagementSample.Domain
{
    /// <summary>
    /// スキルの種類
    /// </summary>
    public enum SkillType
    {
        /// <summary> 通常攻撃 </summary>
        Attack,

        /// <summary> 強攻撃（高ダメージ） </summary>
        HeavyAttack,

        /// <summary> 回復 </summary>
        Heal,

        /// <summary> 防御（次の被ダメージ減少） </summary>
        Defend,

        /// <summary> 自爆 </summary>
        SelfDestruct
    }

    /// <summary>
    /// スキル（ValueObject）
    /// </summary>
    public sealed class Skill
    {
        /// <summary> スキル名 </summary>
        public string Name { get; }

        /// <summary> スキルの説明 </summary>
        public string Description { get; }

        /// <summary> スキルタイプ </summary>
        public SkillType Type { get; }

        /// <summary> 威力（攻撃スキルの場合） </summary>
        public int Power { get; }

        /// <summary> 回復量（回復スキルの場合） </summary>
        public int HealAmount { get; }

        private Skill(string name, string description, SkillType type, int power = 0, int healAmount = 0)
        {
            Name = name;
            Description = description;
            Type = type;
            Power = power;
            HealAmount = healAmount;
        }

        /// <summary> 通常攻撃 </summary>
        public static Skill Attack { get; } = new Skill(
            "こうげき",
            "通常攻撃を行う",
            SkillType.Attack,
            power: 10
        );

        /// <summary> 強攻撃 </summary>
        public static Skill HeavyAttack { get; } = new Skill(
            "つよいこうげき",
            "渾身の一撃を放つ",
            SkillType.HeavyAttack,
            power: 25
        );

        /// <summary> 回復 </summary>
        public static Skill Heal { get; } = new Skill(
            "かいふく",
            "HPを回復する",
            SkillType.Heal,
            healAmount: 30
        );

        /// <summary> 防御 </summary>
        public static Skill Defend { get; } = new Skill(
            "ぼうぎょ",
            "防御してダメージを減らす",
            SkillType.Defend
        );

        /// <summary> 自爆 </summary>
        public static Skill SelfDestruct { get; } = new Skill(
            "じばく",
            "敵に大ダメージを与えて自滅する",
            SkillType.SelfDestruct,
            power: 9999
        );

        /// <summary> 全スキルのリスト </summary>
        public static Skill[] All { get; } = { Attack, HeavyAttack, Heal, Defend, SelfDestruct };
    }
}
