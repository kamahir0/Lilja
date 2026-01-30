using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Lilja.ScreenManagement.Mock
{
    /// <summary>
    /// 探索パートの World
    /// </summary>
    public class MockExploreWorld : WorldBase<ValueTuple>
    {
        [UnityView] private MockExploreView _view;

        private MockMenuOverlay _menuOverlay;

        /// <inheritdoc/>
        protected override void OnViewLoaded()
        {
            _view.MenuButton.onClick.AddListener(OnClickMenu);
            _view.BattleButton.onClick.AddListener(OnClickBattle);
        }

        /// <inheritdoc/>
        protected override void OnViewUnloaded()
        {
            _view.MenuButton.onClick.RemoveListener(OnClickMenu);
            _view.BattleButton.onClick.RemoveListener(OnClickBattle);
        }

        /// <inheritdoc/>
        protected override UniTask EnterAsync(EnterType enterType, CancellationToken cancellationToken)
        {
            _menuOverlay = new MockMenuOverlay();
            _menuOverlay.PreloadViewAsync(cancellationToken).Forget();

            Debug.Log("[ExploreWorld] 探索パートへ遷移しました");
            if (enterType == EnterType.OnResume)
            {
                return _view.AnimateInAsync(cancellationToken);
            }

            return UniTask.CompletedTask;
        }

        /// <inheritdoc/>
        protected override UniTask ExitAsync(ExitType exitType, CancellationToken cancellationToken)
        {
            if (exitType == ExitType.OnPause)
            {
                return _view.AnimateOutAsync(cancellationToken);
            }

            return UniTask.CompletedTask;
        }

        /// <summary> メニューボタンクリック時 </summary>
        private void OnClickMenu()
        {
            UniTask.Void(async () =>
            {
                Debug.Log("[ExploreWorld] メニュー画面へ遷移します");
                await _menuOverlay.CallAsync(default, DisposeCancellationToken);
            });
        }

        /// <summary> 戦闘ボタンクリック時 </summary>
        private void OnClickBattle()
        {
            UniTask.Void(async () =>
            {
                Debug.Log("[ExploreWorld] 戦闘画面へ遷移します");
                await new MockBattleOverlay().CallAsync(default, DisposeCancellationToken);
            });
        }
    }
}