using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Lilja.ScreenManagement;
using ScreenManagementSample.Application;
using ScreenManagementSample.Domain;
using UnityEngine;

namespace ScreenManagementSample.Presentation
{
    /// <summary>
    /// バトル画面Overlay（MVP - Presenter）
    /// フェーズ制御による1対多バトルをサポート
    /// </summary>
    public class BattleOverlay : SceneOverlayBase<ValueTuple, BattleResult>
    {
        [UnityView] private BattleView _view;

        private readonly Enemy[] _enemies;
        private bool _isBattleEnded;
        private bool _isPlayerDefending;

        public BattleOverlay(Enemy[] enemies)
        {
            _enemies = enemies;
        }

        protected override void OnViewLoaded()
        {
            _view.AttackButton.onClick.AddListener(OnClickAttack);
            _view.SkillButton.onClick.AddListener(OnClickSkill);
            _view.ItemButton.onClick.AddListener(OnClickItem);
            _view.DefendButton.onClick.AddListener(OnClickDefend);

            // 敵オブジェクトを敵の数だけ生成
            _view.InitializeEnemyObjects(_enemies.Length);

            // 初期表示
            SyncDisplay();
            var enemyNames = string.Join("、", _enemies.Select(e => e.Name));
            _view.SetMessage($"{enemyNames}が現れた！");

            // 消費アイテムがなければアイテムボタンを無効化
            UpdateItemButtonState();
        }

        protected override void OnViewUnloaded()
        {
            _view.AttackButton.onClick.RemoveListener(OnClickAttack);
            _view.SkillButton.onClick.RemoveListener(OnClickSkill);
            _view.ItemButton.onClick.RemoveListener(OnClickItem);
            _view.DefendButton.onClick.RemoveListener(OnClickDefend);

            // 敵オブジェクトをクリア
            _view.ClearEnemyObjects();
        }

        protected override UniTask EnterAsync(EnterType enterType, CancellationToken cancellationToken)
        {
            Debug.Log("[BattleOverlay] バトル開始！");
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 生存している敵を取得
        /// </summary>
        private Enemy[] GetAliveEnemies()
        {
            return _enemies.Where(e => e.IsAlive).ToArray();
        }

        /// <summary>
        /// ステータス表示を同期
        /// </summary>
        private void SyncDisplay()
        {
            var player = GameServices.PlayerRepository.Get();
            _view.SetPlayerStatus(player.Name, player.CurrentHp, player.MaxHp);
            _view.SetEnemiesStatus(_enemies);

            // 敵オブジェクトの表示状態も更新
            _view.UpdateEnemyObjectsVisibility(_enemies);
        }

        /// <summary>
        /// アイテムボタンの状態を更新
        /// </summary>
        private void UpdateItemButtonState()
        {
            var inventory = GameServices.InventoryRepository.Get();
            var hasConsumables = inventory.Items.Any(i => i.Definition.Type == ItemType.Consumable);
            _view.SetItemButtonInteractable(hasConsumables);
        }

        /// <summary>
        /// コマンドメニューに戻る
        /// </summary>
        private void ReturnToCommandMenu()
        {
            _view.SetMessage("コマンド？");
            _view.SetCommandButtonsInteractable(true);
            UpdateItemButtonState();
        }

        #region コマンドハンドラ

        private void OnClickAttack()
        {
            if (_isBattleEnded) return;

            UniTask.Void(async () =>
            {
                _view.SetCommandButtonsInteractable(false);

                // フェーズ制御：ターゲット選択
                var aliveEnemies = GetAliveEnemies();
                if (aliveEnemies.Length == 1)
                {
                    // 敵が1体なら自動選択
                    await ExecutePlayerTurnAsync(Skill.Attack, aliveEnemies[0]);
                }
                else
                {
                    // 複数敵ならターゲット選択
                    var target = await new TargetSelectOverlay(aliveEnemies).CallAsync(default, DisposeCancellationToken);
                    if (target == null)
                    {
                        Debug.Log("[BattleOverlay] ターゲット選択キャンセル");
                        ReturnToCommandMenu();
                        return;
                    }

                    await ExecutePlayerTurnAsync(Skill.Attack, target);
                }
            });
        }

        private void OnClickSkill()
        {
            if (_isBattleEnded) return;

            UniTask.Void(async () =>
            {
                _view.SetCommandButtonsInteractable(false);

                // フェーズ制御: phase 0 = スキル選択, phase 1 = ターゲット選択
                int phase = 0;
                Skill selectedSkill = null;
                Enemy selectedTarget = null;

                while (phase >= 0)
                {
                    switch (phase)
                    {
                        case 0: // スキル選択
                            var availableSkills = new[] { Skill.HeavyAttack, Skill.Heal, Skill.SelfDestruct };
                            selectedSkill = await new SkillSelectOverlay(availableSkills).CallAsync(default, DisposeCancellationToken);

                            if (selectedSkill == null)
                            {
                                Debug.Log("[BattleOverlay] スキル選択キャンセル");
                                phase = -1; // ループ終了
                                ReturnToCommandMenu();
                            }
                            else if (selectedSkill.Type == SkillType.HeavyAttack || selectedSkill.Type == SkillType.SelfDestruct)
                            {
                                // 攻撃系はターゲット選択へ
                                phase = 1;
                            }
                            else
                            {
                                // 回復系は即実行
                                await ExecutePlayerTurnAsync(selectedSkill, null);
                                phase = -1;
                            }

                            break;

                        case 1: // ターゲット選択
                            var aliveEnemies = GetAliveEnemies();
                            if (aliveEnemies.Length == 1)
                            {
                                selectedTarget = aliveEnemies[0];
                                await ExecutePlayerTurnAsync(selectedSkill, selectedTarget);
                                phase = -1;
                            }
                            else
                            {
                                selectedTarget = await new TargetSelectOverlay(aliveEnemies).CallAsync(default, DisposeCancellationToken);
                                if (selectedTarget == null)
                                {
                                    Debug.Log("[BattleOverlay] ターゲット選択キャンセル → スキル選択に戻る");
                                    phase = 0; // スキル選択に戻る
                                }
                                else
                                {
                                    await ExecutePlayerTurnAsync(selectedSkill, selectedTarget);
                                    phase = -1;
                                }
                            }

                            break;
                    }
                }
            });
        }

        private void OnClickItem()
        {
            if (_isBattleEnded) return;

            UniTask.Void(async () =>
            {
                _view.SetCommandButtonsInteractable(false);

                var inventory = GameServices.InventoryRepository.Get();
                var consumables = inventory.Items
                    .Where(i => i.Definition.Type == ItemType.Consumable)
                    .ToArray();

                var selectedItem = await new ItemSelectOverlay(consumables).CallAsync(default, DisposeCancellationToken);

                if (selectedItem == null)
                {
                    Debug.Log("[BattleOverlay] アイテム選択キャンセル");
                    ReturnToCommandMenu();
                    return;
                }

                await ExecuteItemUseAsync(selectedItem);
            });
        }

        private void OnClickDefend()
        {
            if (_isBattleEnded) return;

            UniTask.Void(async () =>
            {
                _view.SetCommandButtonsInteractable(false);
                await ExecutePlayerTurnAsync(Skill.Defend, null);
            });
        }

        #endregion

        #region ターン処理

        private async UniTask ExecutePlayerTurnAsync(Skill skill, Enemy target)
        {
            var player = GameServices.PlayerRepository.Get();
            var battleService = GameServices.BattleService;

            // 攻撃系スキルの場合はアニメーション
            if ((skill.Type == SkillType.Attack || skill.Type == SkillType.HeavyAttack || skill.Type == SkillType.SelfDestruct) && target != null)
            {
                await _view.PlayPlayerAttackAnimationAsync(DisposeCancellationToken);

                var result = battleService.UseSkill(player, target, skill, _isPlayerDefending);
                _view.SetMessage(result.Message);

                _isPlayerDefending = false; // 攻撃したので防御解除

                if (result.Damage > 0)
                {
                    var targetIndex = Array.IndexOf(_enemies, target);
                    await _view.PlayDamageAnimationAsync(false, targetIndex, DisposeCancellationToken);
                }
            }
            else if (skill.Type == SkillType.Heal)
            {
                var result = battleService.UseSkill(player, _enemies[0], skill, _isPlayerDefending);
                _view.SetMessage(result.Message);
            }
            else if (skill.Type == SkillType.Defend)
            {
                _isPlayerDefending = true;
                _view.SetMessage($"{player.Name}は防御の構えをとった！");
            }

            SyncDisplay();
            await UniTask.Delay(500, cancellationToken: DisposeCancellationToken);

            // 勝敗判定
            var battleResult = battleService.CheckBattleResult(player, _enemies);
            if (battleResult.HasValue)
            {
                await EndBattleAsync(battleResult.Value);
                return;
            }

            await UniTask.Delay(300, cancellationToken: DisposeCancellationToken);

            // 敵のターン
            await ExecuteEnemyTurnAsync();
        }

        private async UniTask ExecuteItemUseAsync(InventoryItem item)
        {
            var player = GameServices.PlayerRepository.Get();
            var inventory = GameServices.InventoryRepository.Get();

            inventory.RemoveItem(item.Definition.Id, 1);

            int healAmount = item.Definition.Id.Value switch
            {
                "potion" => 30,
                "hi_potion" => 80,
                _ => 20
            };

            player.Heal(healAmount);
            _view.SetMessage($"{player.Name}は{item.Definition.Name}を使った！HPが{healAmount}回復した！");

            SyncDisplay();
            await UniTask.Delay(1000, cancellationToken: DisposeCancellationToken);

            // 敵のターン
            await ExecuteEnemyTurnAsync();
        }

        private async UniTask ExecuteEnemyTurnAsync()
        {
            var player = GameServices.PlayerRepository.Get();
            var battleService = GameServices.BattleService;

            // 生存している敵全員が攻撃
            var aliveEnemies = GetAliveEnemies();
            foreach (var enemy in aliveEnemies)
            {
                await _view.PlayEnemyAttackAnimationAsync(Array.IndexOf(_enemies, enemy), DisposeCancellationToken);

                var enemyDamage = battleService.EnemyAttack(enemy, player, _isPlayerDefending);

                if (_isPlayerDefending)
                {
                    _view.SetMessage($"{enemy.Name}の攻撃！{player.Name}は防御した！{enemyDamage}のダメージ！");
                }
                else
                {
                    _view.SetMessage($"{enemy.Name}の攻撃！{player.Name}に{enemyDamage}のダメージ！");
                }

                await _view.PlayDamageAnimationAsync(true, 0, DisposeCancellationToken);
                SyncDisplay();

                await UniTask.Delay(500, cancellationToken: DisposeCancellationToken);

                // プレイヤー死亡チェック
                if (!player.IsAlive)
                {
                    await EndBattleAsync(BattleResult.Defeat);
                    return;
                }
            }

            // 防御は1ターンで解除
            _isPlayerDefending = false;

            // 勝敗判定
            var battleResult = battleService.CheckBattleResult(player, _enemies);
            if (battleResult.HasValue)
            {
                await EndBattleAsync(battleResult.Value);
                return;
            }

            ReturnToCommandMenu();
        }

        private async UniTask EndBattleAsync(BattleResult result)
        {
            _isBattleEnded = true;
            _view.SetMessage(result == BattleResult.Victory ? "勝利！" : "敗北...");
            await UniTask.Delay(1500, cancellationToken: DisposeCancellationToken);
            Close(result);
        }

        #endregion
    }
}


