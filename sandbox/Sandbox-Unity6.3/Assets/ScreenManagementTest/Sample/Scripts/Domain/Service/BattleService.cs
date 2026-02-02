namespace ScreenManagementSample.Domain
{
    /// <summary>
    /// バトルロジックを提供するドメインサービス
    /// </summary>
    public class BattleService
    {
        /// <summary>
        /// プレイヤーが敵を攻撃する
        /// </summary>
        public int PlayerAttack(Player player, Enemy enemy)
        {
            var damage = player.Attack;
            enemy.TakeDamage(damage);
            return System.Math.Max(1, damage - enemy.Defense);
        }

        /// <summary>
        /// プレイヤーがスキルを使用する
        /// </summary>
        public SkillResult UseSkill(Player player, Enemy enemy, Skill skill, bool isDefending)
        {
            switch (skill.Type)
            {
                case SkillType.Attack:
                {
                    var baseDamage = player.Attack + skill.Power;
                    var actualDamage = System.Math.Max(1, baseDamage - enemy.Defense);
                    enemy.TakeDamage(baseDamage);
                    return new SkillResult($"{player.Name}の攻撃！{enemy.Name}に{actualDamage}のダメージ！", actualDamage, 0, false);
                }
                case SkillType.HeavyAttack:
                {
                    var baseDamage = player.Attack + skill.Power;
                    var actualDamage = System.Math.Max(1, baseDamage - enemy.Defense);
                    enemy.TakeDamage(baseDamage);
                    return new SkillResult($"{player.Name}の強攻撃！{enemy.Name}に{actualDamage}の大ダメージ！", actualDamage, 0, false);
                }
                case SkillType.Heal:
                {
                    var prevHp = player.CurrentHp;
                    player.Heal(skill.HealAmount);
                    var healed = player.CurrentHp - prevHp;
                    return new SkillResult($"{player.Name}はHPを{healed}回復した！", 0, healed, false);
                }
                case SkillType.Defend:
                    return new SkillResult($"{player.Name}は防御の構えをとった！", 0, 0, true);
                case SkillType.SelfDestruct:
                {
                    // プレイヤーのHPを0にする
                    var playerDamage = player.CurrentHp;
                    player.TakeDamage(playerDamage);

                    // 敵に大ダメージ
                    var baseDamage = skill.Power;
                    var actualDamage = System.Math.Max(1, baseDamage - enemy.Defense);
                    enemy.TakeDamage(baseDamage);

                    return new SkillResult($"{player.Name}は自爆した！敵に{actualDamage}のダメージ！", actualDamage, 0, false);
                }
                default:
                    return new SkillResult("何も起こらなかった...", 0, 0, false);
            }
        }

        /// <summary>
        /// 敵がプレイヤーを攻撃する
        /// </summary>
        public int EnemyAttack(Enemy enemy, Player player, bool isPlayerDefending = false)
        {
            var damage = enemy.Attack;
            if (isPlayerDefending)
            {
                damage /= 2; // 防御中はダメージ半減
            }

            player.TakeDamage(damage);
            return System.Math.Max(1, damage - player.Defense);
        }

        /// <summary>
        /// バトル結果を判定する（単一敵）
        /// </summary>
        public BattleResult? CheckBattleResult(Player player, Enemy enemy)
        {
            if (!enemy.IsAlive) return BattleResult.Victory;
            if (!player.IsAlive) return BattleResult.Defeat;
            return null; // バトル継続
        }

        /// <summary>
        /// バトル結果を判定する（複数敵）
        /// </summary>
        public BattleResult? CheckBattleResult(Player player, Enemy[] enemies)
        {
            // プレイヤーが倒れたら敗北
            if (!player.IsAlive) return BattleResult.Defeat;

            // 全敵が倒れていたら勝利
            bool allEnemiesDead = true;
            foreach (var enemy in enemies)
            {
                if (enemy.IsAlive)
                {
                    allEnemiesDead = false;
                    break;
                }
            }

            if (allEnemiesDead) return BattleResult.Victory;

            return null; // バトル継続
        }
    }

    /// <summary>
    /// スキル使用結果
    /// </summary>
    public readonly struct SkillResult
    {
        /// <summary> 結果メッセージ </summary>
        public string Message { get; }

        /// <summary> ダメージ量 </summary>
        public int Damage { get; }

        /// <summary> 回復量 </summary>
        public int Heal { get; }

        /// <summary> 防御状態になったか </summary>
        public bool IsDefending { get; }

        public SkillResult(string message, int damage, int heal, bool isDefending)
        {
            Message = message;
            Damage = damage;
            Heal = heal;
            IsDefending = isDefending;
        }
    }
}
