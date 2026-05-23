using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// Dialog の基底クラス
    /// </summary>
    public abstract class DialogBase<TArgs, TResult, TFrame, TContent> : OverlayBase<TArgs, TResult>, IDialog, IOverlay<TArgs, TResult>
        where TFrame : MonoBehaviour, IDialogFrame
        where TContent : MonoBehaviour
    {
        #region For Implementers

        [UnityView] protected TFrame Frame;
        [UnityView] protected TContent Content;
        [UnityView] private OutsideButton _outsideButton;

        /// <summary> 表示時のアニメーション </summary>
        protected virtual IDialogAnimation Animation { get; } = new DefaultDialogAnimation();

        /// <summary> スタック時のアニメーション </summary>
        protected virtual IDialogStackAnimation StackAnimation { get; } = new DefaultStackAnimation();

        /// <summary> 枠外部分をクリック可能にするか </summary>
        protected virtual bool EnableOutsideButton => false;

        /// <summary> 枠外部分をクリックした時に返却する結果 </summary>
        protected virtual TResult OutsideButtonResult => default;

        /// <summary> Prefabが見つからなかった場合のデフォルトフレームを生成します </summary>
        protected virtual GameObject CreateFallbackFrame() => null;

        /// <summary> Prefabが見つからなかった場合のデフォルトコンテンツを生成します </summary>
        protected virtual GameObject CreateFallbackContent() => null;

        #endregion

        private DialogViewHandle _viewHandle;

        /// <summary> Resourcesの場合はパス、Addressableの場合はアドレスとして使われるキーを返します </summary>
        private static string GetFrameKey()
        {
            return $"DialogFrame/{typeof(TFrame).Name}";
        }

        /// <summary> Resourcesの場合はパス、Addressableの場合はアドレスとして使われるキーを返します </summary>
        private static string GetContentKey()
        {
            return $"DialogContent/{typeof(TContent).Name}";
        }

        #region IOverlay

        /// <inheritdoc />
        UniTask IOverlay<TArgs, TResult>.InitializeAsync(TArgs args, CancellationToken cancellationToken)
        {
            var framePrefabHandle = ScreenManagement.Repository.Instance.PrefabHandleFactory.Invoke(GetFrameKey());
            var contentPrefabHandle = ScreenManagement.Repository.Instance.PrefabHandleFactory.Invoke(GetContentKey());
            var useBackdrop = ScreenManagement.Repository.Instance.ScreenStack.TopScreen is not IDialog;
            _viewHandle = new DialogViewHandle(framePrefabHandle, contentPrefabHandle, useBackdrop, CreateFallbackFrame, CreateFallbackContent);
            return InitializeAsync(args, cancellationToken);
        }

        #endregion

        #region ScreenBase

        /// <inheritdoc/>
        protected override IViewHandle ViewHandle => _viewHandle;

        #endregion

        #region IScreen

        /// <inheritdoc />
        async UniTask IScreen.LoadViewAsync(CancellationToken cancellationToken)
        {
            await ViewHandle.LoadAsync(cancellationToken);
            ApplyCanvasOrder();
            InjectUnityView();
            _outsideButton.onClick.AddListener(OnClickOutside);
            OnViewLoaded();
            Animation?.OnViewInstanced(_viewHandle.FrameRectTransform);
            StackAnimation?.OnViewInstanced(_viewHandle.FrameRectTransform);
        }

        /// <inheritdoc />
        void IScreen.UnloadView()
        {
            OnViewUnloaded();
            _outsideButton.onClick.RemoveListener(OnClickOutside);
            Animation?.OnViewDestroy();
            StackAnimation?.OnViewDestroy();
            NullifyUnityView();
            ViewHandle.Unload();
        }

        /// <summary> 枠外クリック時の処理 </summary>
        private void OnClickOutside()
        {
            if (EnableOutsideButton)
            {
                Close(OutsideButtonResult);
            }
        }

        /// <inheritdoc />
        async UniTask IScreen.OpenAsync(CancellationToken cancellationToken)
        {
            if (Animation != null) await Animation.ShowAsync(cancellationToken);
            await EnterAsync(EnterType.OnOpen, cancellationToken);
        }

        /// <inheritdoc />
        async UniTask IScreen.CloseAsync(CancellationToken cancellationToken)
        {
            await ExitAsync(ExitType.OnClose, cancellationToken);
            if (Animation != null) await Animation.HideAsync(cancellationToken);
        }

        /// <inheritdoc />
        async UniTask IScreen.PauseAsync(PauseContext context, CancellationToken cancellationToken)
        {
            await ExitAsync(ExitType.OnPause, cancellationToken);

            // 次に開くのがDialogの場合のみStackAnimationを再生
            if (context.NextScreen is IDialog && StackAnimation != null)
            {
                await StackAnimation.PushAsync(cancellationToken);
            }
        }

        /// <inheritdoc />
        async UniTask IScreen.ResumeAsync(ResumeContext context, CancellationToken cancellationToken)
        {
            // 閉じたのがDialogの場合のみStackAnimationを再生
            if (context.PreviousScreen is IDialog && StackAnimation != null)
            {
                await StackAnimation.PopAsync(cancellationToken);
            }

            await EnterAsync(EnterType.OnResume, cancellationToken);
        }

        #endregion
    }
}
