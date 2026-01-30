using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Lilja.ScreenManagement.Mock
{
    /// <summary>
    /// タイトル画面の World
    /// </summary>
    public class MockTitleWorld : WorldBase<ValueTuple>
    {
        [UnityView] private MockTitleView _view;

        /// <inheritdoc/>
        protected override void OnViewLoaded()
        {
            _view.StartButton.onClick.AddListener(OnClickStart);
        }

        /// <inheritdoc/>
        protected override void OnViewUnloaded()
        {
            _view.StartButton.onClick.RemoveListener(OnClickStart);
        }

        /// <inheritdoc/>
        protected override UniTask EnterAsync(EnterType enterType, CancellationToken cancellationToken)
        {
            Debug.Log("[TitleWorld] タイトル画面へ遷移しました");
            return UniTask.CompletedTask;
        }

        /// <summary> スタートボタンクリック時 </summary>
        private static void OnClickStart()
        {
            Debug.Log("[TitleWorld] 探索パートへ遷移します");
            World.Switch(typeof(MockExploreWorld), new ValueTuple());
        }
    }
}