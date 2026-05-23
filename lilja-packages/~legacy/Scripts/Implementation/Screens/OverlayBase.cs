using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.ScreenManagement
{
    /// <summary>
    ///  Overlay の基底クラス
    /// </summary>
    public abstract class OverlayBase<TArgs, TResult> : ScreenBase, IOverlay<TArgs, TResult>
    {
        #region For Implementers

        /// <summary> 初期化します </summary>
        protected virtual UniTask InitializeAsync(TArgs args, CancellationToken cancellationToken) => UniTask.CompletedTask;

        /// <summary>
        /// Overlay を呼び出します
        /// </summary>
        public UniTask<TResult> CallAsync(TArgs args, CancellationToken cancellationToken)
        {
            return OverlayService.CallOverlayAsync(this, args, cancellationToken);
        }

        /// <summary>
        /// Overlay を閉じます
        /// </summary>
        protected void Close(TResult result)
        {
            _completionSource.TrySetResult(result);
        }

        /// <summary>
        /// Overlay 呼び出し元に例外を送出します
        /// </summary>
        protected void Throw(Exception exception)
        {
            _completionSource.TrySetException(exception);
        }

        #endregion

        private int _layerIndex;
        private readonly UniTaskCompletionSource<TResult> _completionSource = new();

        #region ScreenBase

        /// <inheritdoc/>
        protected override int LayerIndex => _layerIndex;

        #endregion

        #region IOverlay

        /// <inheritdoc/>
        public virtual bool IsHeavy => false;

        /// <inheritdoc />
        void IOverlay.SetLayerIndex(int index) => _layerIndex = index;

        /// <inheritdoc />
        UniTask IOverlay<TArgs, TResult>.InitializeAsync(TArgs args, CancellationToken cancellationToken) => InitializeAsync(args, cancellationToken);

        /// <inheritdoc />
        async UniTask<TResult> IOverlay<TArgs, TResult>.WaitForResultAsync(CancellationToken cancellationToken)
        {
            await using var _ = cancellationToken.RegisterWithoutCaptureExecutionContext(() => _completionSource.TrySetCanceled());
            return await _completionSource.Task;
        }

        #endregion
    }
}