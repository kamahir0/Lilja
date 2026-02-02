using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Lilja.ScreenManagement.Dialog;
using UnityEngine;

namespace Lilja.ScreenManagement.Mock
{
    /// <summary>
    /// キャラ詳細画面の Overlay
    /// </summary>
    public class MockMenuCharacterOverlay : PrefabOverlayBase<ValueTuple, ValueTuple>
    {
        [UnityView] private MockMenuCharacterView _view;

        /// <inheritdoc/>
        protected override UniTask EnterAsync(EnterType enterType, CancellationToken cancellationToken)
        {
            Debug.Log("[MenuCharacterOverlay] キャラ詳細画面を表示しました");
            return UniTask.CompletedTask;
        }

        /// <inheritdoc/>
        protected override void OnViewLoaded()
        {
            _view.TestButton.onClick.AddListener(OnClickTest);
            _view.CloseButton.onClick.AddListener(OnClickClose);
        }

        /// <inheritdoc/>
        protected override void OnViewUnloaded()
        {
            _view.TestButton.onClick.RemoveListener(OnClickTest);
            _view.CloseButton.onClick.RemoveListener(OnClickClose);
        }

        /// <summary> テストボタンクリック時 </summary>
        private void OnClickTest()
        {
            UniTask.Void(async () =>
            {
                var result = await VariableDialog.Create<ValueTuple, bool>("test")
                    .AddText("test")
                    .AddButton("OK", true)
                    .CallAsync(default, DisposeCancellationToken);

                Debug.Log($"[MenuCharacterOverlay] result: {result}");
            });
        }

        /// <summary> 閉じるボタンクリック時 </summary>
        private void OnClickClose()
        {
            Debug.Log("[MenuCharacterOverlay] キャラ詳細画面を閉じます");
            Close(default);
        }
    }
}