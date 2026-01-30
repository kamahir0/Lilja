using System;
using Lilja.ScreenManagement;
using UnityEngine;

namespace ScreenManagementSample.Presentation
{
    /// <summary>
    /// タイトル画面World（MVP - Presenter）
    /// </summary>
    public class TitleWorld : WorldBase<ValueTuple>
    {
        [UnityView] private TitleView _view;

        protected override void OnViewLoaded()
        {
            _view.StartButton.onClick.AddListener(OnClickStart);
        }

        protected override void OnViewUnloaded()
        {
            _view.StartButton.onClick.RemoveListener(OnClickStart);
        }

        private void OnClickStart()
        {
            Debug.Log("[TitleWorld] マップ画面へ遷移します");
            // ゲームをリセットして新規開始
            Application.GameServices.Reset();
            World.Switch(typeof(MapWorld), new ValueTuple());
        }
    }
}
