using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Lilja.ScreenManagement.Mock
{
    /// <summary>
    /// メニュー画面の Overlay
    /// </summary>
    public class MockMenuOverlay : PrefabOverlayBase<ValueTuple, ValueTuple>
    {
        [UnityView] private MockMenuView _view;

        /// <inheritdoc/>
        protected override void OnViewLoaded()
        {
            _view.CharacterButton.onClick.AddListener(OnClickCharacter);
            _view.BattleButton.onClick.AddListener(OnClickBattle);
            _view.CloseButton.onClick.AddListener(OnClickClose);
            _view.TitleButton.onClick.AddListener(OnClickTitle);
        }

        /// <inheritdoc/>
        protected override void OnViewUnloaded()
        {
            _view.CharacterButton.onClick.RemoveListener(OnClickCharacter);
            _view.BattleButton.onClick.RemoveListener(OnClickBattle);
            _view.CloseButton.onClick.RemoveListener(OnClickClose);
            _view.TitleButton.onClick.RemoveListener(OnClickTitle);
        }

        /// <inheritdoc/>
        protected override async UniTask EnterAsync(EnterType enterType, CancellationToken cancellationToken)
        {
            Debug.Log("[MenuOverlay] メニュー画面を表示しました");
            if (enterType == EnterType.OnResume) return;

            await _view.AnimateInAsync(cancellationToken);
        }

        /// <inheritdoc/>
        protected override async UniTask ExitAsync(ExitType exitType, CancellationToken cancellationToken)
        {
            if (exitType == ExitType.OnPause) return;

            await _view.AnimateOutAsync(cancellationToken);
        }

        /// <summary> キャラ詳細ボタンクリック時 </summary>
        private void OnClickCharacter()
        {
            UniTask.Void(async () =>
            {
                Debug.Log("[MenuOverlay] キャラ詳細画面へ遷移します");
                await new MockMenuCharacterOverlay().CallAsync(default, DisposeCancellationToken);
            });
        }

        /// <summary> バトルボタンクリック時 </summary>
        private void OnClickBattle()
        {
            UniTask.Void(async () =>
            {
                Debug.Log("[MenuOverlay] バトル画面へ遷移します");
                await new MockBattleOverlay().CallAsync(default, DisposeCancellationToken);
            });
        }

        /// <summary> 閉じるボタンクリック時 </summary>
        private void OnClickClose()
        {
            Debug.Log("[MenuOverlay] メニュー画面を閉じます");
            Close(default);
        }

        /// <summary> タイトルボタンクリック時 </summary>
        private void OnClickTitle()
        {
            UniTask.Void(async () =>
            {
                // タイトルへ戻る前に確認ダイアログを表示
                var result = await new TestDialog().CallAsync(default, DisposeCancellationToken);
                if (!result)
                {
                    return;
                }

                Debug.Log("[MenuOverlay] タイトル画面へ遷移します");
                World.Switch(typeof(MockTitleWorld), new ValueTuple());
            });
        }
    }
}