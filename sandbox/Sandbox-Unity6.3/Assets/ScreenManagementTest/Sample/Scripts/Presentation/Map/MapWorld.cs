using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Lilja.ScreenManagement;
using Lilja.ScreenManagement.Dialog;
using ScreenManagementSample.Application;
using ScreenManagementSample.Domain;
using UnityEngine;

namespace ScreenManagementSample.Presentation
{
    /// <summary>
    /// マップ探索World（MVP - Presenter）
    /// </summary>
    public class MapWorld : WorldBase<ValueTuple>
    {
        [UnityView] private MapView _view;

        protected override void OnViewLoaded()
        {
            _view.MenuButton.onClick.AddListener(OnClickMenu);
            _view.UpButton.onClick.AddListener(OnClickUp);
            _view.DownButton.onClick.AddListener(OnClickDown);
            _view.LeftButton.onClick.AddListener(OnClickLeft);
            _view.RightButton.onClick.AddListener(OnClickRight);
            _view.InteractButton.onClick.AddListener(OnClickInteract);

            // 初期表示（アニメなし）
            SyncDisplayImmediate();

            // インタラクトポイントの表示を初期化
            InitializeInteractPoints();
        }

        protected override void OnViewUnloaded()
        {
            _view.MenuButton.onClick.RemoveListener(OnClickMenu);
            _view.UpButton.onClick.RemoveListener(OnClickUp);
            _view.DownButton.onClick.RemoveListener(OnClickDown);
            _view.LeftButton.onClick.RemoveListener(OnClickLeft);
            _view.RightButton.onClick.RemoveListener(OnClickRight);
            _view.InteractButton.onClick.RemoveListener(OnClickInteract);
        }

        protected override UniTask EnterAsync(EnterType enterType, CancellationToken cancellationToken)
        {
            // Resume時（バトルやメニューから戻った時）に表示を同期（アニメなし）
            SyncDisplayImmediate();

            // インタラクトボタンの状態を更新
            UpdateInteractButtonState();

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 表示を即座に同期（アニメなし）
        /// </summary>
        private void SyncDisplayImmediate()
        {
            var player = GameServices.PlayerRepository.Get();
            _view.SetPositionText(player.Position.X, player.Position.Y);
            _view.SetHpText(player.CurrentHp, player.MaxHp);
            _view.SetPlayerPosition(player.Position.X, player.Position.Y);
        }

        /// <summary>
        /// HP表示のみ同期
        /// </summary>
        private void SyncHpDisplay()
        {
            var player = GameServices.PlayerRepository.Get();
            _view.SetHpText(player.CurrentHp, player.MaxHp);
        }

        /// <summary>
        /// インタラクトポイントの表示を初期化
        /// </summary>
        private void InitializeInteractPoints()
        {
            // まず全タイルをリセット
            _view.ResetAllTileColors();

            // アクティブなインタラクトポイントを表示
            var interactPoints = GameServices.InteractPointRepository.GetAll();
            foreach (var point in interactPoints)
            {
                if (point.IsActive)
                {
                    _view.SetTileColor(point.Position.X, point.Position.Y, true);
                }
            }

            // インタラクトボタンの状態を更新
            UpdateInteractButtonState();
        }

        /// <summary>
        /// インタラクトボタンの有効/無効を更新
        /// </summary>
        private void UpdateInteractButtonState()
        {
            var player = GameServices.PlayerRepository.Get();
            var interactPoint = GameServices.InteractPointRepository.GetAt(player.Position);
            _view.SetInteractButtonActive(interactPoint != null);
        }

        private void OnClickMenu()
        {
            UniTask.Void(async () =>
            {
                Debug.Log("[MapWorld] メニューを開きます");
                await new MenuOverlay().CallAsync(default, DisposeCancellationToken);
            });
        }

        private void OnClickInteract()
        {
            UniTask.Void(async () =>
            {
                var player = GameServices.PlayerRepository.Get();
                var interactPoint = GameServices.InteractPointRepository.GetAt(player.Position);

                if (interactPoint == null)
                {
                    Debug.Log("[MapWorld] インタラクトできるポイントがありません");
                    return;
                }

                // インタラクト実行
                if (interactPoint.Interact())
                {
                    // インタラクトボタンを非表示
                    _view.SetInteractButtonActive(false);

                    // アイテムを取得
                    var itemDef = GameServices.ItemDefinitionRepository.Get(interactPoint.ItemId);
                    if (itemDef != null)
                    {
                        var inventory = GameServices.InventoryRepository.Get();
                        var addedCount = inventory.AddItem(itemDef, interactPoint.ItemCount);
                        Debug.Log($"[MapWorld] {itemDef.Name} を {addedCount}個 入手しました！");

                        // アイテム取得ダイアログを表示（DOTweenアニメーション付き）
                        await VariableDialog.Create<ValueTuple, ValueTuple>("アイテム取得")
                            .SetAnimation(new DOTweenDialogAnimation())
                            .SetStackAnimation(new DOTweenDialogStackAnimation())
                            .AddText($"{itemDef.Name} を {addedCount}個 手に入れた！")
                            .AddButton("OK", default)
                            .CallAsync(default, DisposeCancellationToken);
                    }

                    // タイルの色を通常に戻す
                    _view.SetTileColor(interactPoint.Position.X, interactPoint.Position.Y, false);
                }
            });
        }

        private void OnClickUp() => Move(0, 1);
        private void OnClickDown() => Move(0, -1);
        private void OnClickLeft() => Move(-1, 0);
        private void OnClickRight() => Move(1, 0);

        private void Move(int dx, int dy)
        {
            var player = GameServices.PlayerRepository.Get();
            var map = GameServices.MapRepository.Get();
            var newPosition = player.Position.Move(dx, dy);

            // マップ範囲チェック
            if (!map.IsInBounds(newPosition))
            {
                Debug.Log("[MapWorld] 移動できません（マップ外）");
                return;
            }

            player.MoveTo(newPosition);

            // 移動時はアニメ付きで位置を更新
            _view.SetPositionText(newPosition.X, newPosition.Y);
            _view.MovePlayerTo(newPosition.X, newPosition.Y);

            Debug.Log($"[MapWorld] 移動しました: {newPosition}");

            // インタラクトボタンの状態を更新
            UpdateInteractButtonState();

            // エンカウント判定
            CheckEncounter();
        }

        private void CheckEncounter()
        {
            if (GameServices.EncounterService.CheckEncounter())
            {
                UniTask.Void(async () =>
                {
                    Debug.Log("[MapWorld] エンカウント！");
                    var enemies = GameServices.EncounterService.GenerateEnemies();
                    var result = await new BattleOverlay(enemies).CallAsync(default, DisposeCancellationToken);

                    if (result == BattleResult.Defeat)
                    {
                        Debug.Log("[MapWorld] ゲームオーバー！");
                        World.Switch(typeof(GameOverWorld), new ValueTuple());
                    }
                    else
                    {
                        Debug.Log("[MapWorld] 勝利！探索を続けます");
                        // バトル後はHP表示のみ更新（位置は同じなのでアニメ不要）
                        SyncHpDisplay();
                    }
                });
            }
        }
    }
}

