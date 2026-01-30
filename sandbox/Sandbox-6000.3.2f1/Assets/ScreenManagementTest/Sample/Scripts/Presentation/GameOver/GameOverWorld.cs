using System;
using Lilja.ScreenManagement;
using UnityEngine;

namespace ScreenManagementSample.Presentation
{
    /// <summary>
    /// ゲームオーバー画面World（MVP - Presenter）
    /// </summary>
    public class GameOverWorld : WorldBase<ValueTuple>
    {
        [UnityView] private GameOverView _view;

        protected override void OnViewLoaded()
        {
            _view.TitleButton.onClick.AddListener(OnClickTitle);
        }

        protected override void OnViewUnloaded()
        {
            _view.TitleButton.onClick.RemoveListener(OnClickTitle);
        }

        private void OnClickTitle()
        {
            Debug.Log("[GameOverWorld] タイトル画面へ遷移します");
            World.Switch(typeof(TitleWorld), new ValueTuple());
        }
    }
}
