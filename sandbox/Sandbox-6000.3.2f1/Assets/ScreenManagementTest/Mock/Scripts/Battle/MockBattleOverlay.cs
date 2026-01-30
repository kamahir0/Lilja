using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Lilja.ScreenManagement.Mock
{
    /// <summary>
    /// バトルパートの Overlay
    /// </summary>
    public class MockBattleOverlay : SceneOverlayBase<ValueTuple, ValueTuple>
    {
        [UnityView] private MockBattleView _view;

        /// <inheritdoc/>
        protected override void OnViewLoaded()
        {
            _view.FinishButton.onClick.AddListener(OnClickFinish);
        }

        /// <inheritdoc/>
        protected override void OnViewUnloaded()
        {
            _view.FinishButton.onClick.RemoveListener(OnClickFinish);
        }

        /// <inheritdoc/>
        protected override UniTask EnterAsync(EnterType enterType, CancellationToken cancellationToken)
        {
            Debug.Log("[BattleOverlay] バトル画面を表示しました");
            return UniTask.CompletedTask;
        }

        /// <summary> 終了ボタンクリック時 </summary>
        private void OnClickFinish()
        {
            Debug.Log("[BattleOverlay] バトルを終了します");
            Close(new ValueTuple());
        }
    }
}