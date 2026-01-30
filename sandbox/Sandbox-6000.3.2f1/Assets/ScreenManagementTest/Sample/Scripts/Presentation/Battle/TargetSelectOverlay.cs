using System;
using Lilja.ScreenManagement;
using ScreenManagementSample.Domain;
using UnityEngine;

namespace ScreenManagementSample.Presentation
{
    /// <summary>
    /// ターゲット選択Overlay（MVP - Presenter）
    /// TResult が null の場合はキャンセル（戻る）を意味する
    /// </summary>
    public class TargetSelectOverlay : PrefabOverlayBase<ValueTuple, Enemy>
    {
        [UnityView] private TargetSelectView _view;

        private readonly Enemy[] _enemies;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="enemies">選択可能な敵配列</param>
        public TargetSelectOverlay(Enemy[] enemies)
        {
            _enemies = enemies ?? Array.Empty<Enemy>();
        }

        protected override void OnViewLoaded()
        {
            // 動的にターゲットボタンを設定
            _view.SetTargets(_enemies, OnTargetSelected);
            _view.BackButton.onClick.AddListener(OnClickBack);

            _view.SetDescription("ターゲットを選んでください");
        }

        protected override void OnViewUnloaded()
        {
            _view.ClearTargetButtons();
            _view.BackButton.onClick.RemoveListener(OnClickBack);
        }

        private void OnTargetSelected(Enemy enemy)
        {
            Debug.Log($"[TargetSelectOverlay] {enemy.Name}を選択");
            Close(enemy);
        }

        private void OnClickBack()
        {
            Debug.Log("[TargetSelectOverlay] キャンセル");
            Close(null); // nullを返してキャンセルを表現
        }
    }
}
