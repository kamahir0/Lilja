using System;
using Lilja.ScreenManagement;
using ScreenManagementSample.Application;
using UnityEngine;

namespace ScreenManagementSample.Presentation
{
    /// <summary>
    /// メニュー画面Overlay（MVP - Presenter）
    /// </summary>
    public class MenuOverlay : PrefabOverlayBase<ValueTuple, ValueTuple>
    {
        [UnityView] private MenuView _view;

        protected override void OnViewLoaded()
        {
            _view.CloseButton.onClick.AddListener(OnClickClose);

            // ステータス表示を同期
            SyncDisplay();
        }

        protected override void OnViewUnloaded()
        {
            _view.CloseButton.onClick.RemoveListener(OnClickClose);
        }

        /// <summary>
        /// 表示を同期
        /// </summary>
        private void SyncDisplay()
        {
            var player = GameServices.PlayerRepository.Get();
            _view.SetStatus(
                player.Name,
                player.CurrentHp,
                player.MaxHp,
                player.Attack,
                player.Defense
            );
        }

        private void OnClickClose()
        {
            Debug.Log("[MenuOverlay] メニューを閉じます");
            Close(default);
        }
    }
}
