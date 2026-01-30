using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// Screen の基底クラス
    /// </summary>
    public abstract class ScreenBase : IScreen
    {
        #region For Implementers

        /// <summary> 画面に入場する演出処理 </summary>
        protected virtual UniTask EnterAsync(EnterType enterType, CancellationToken cancellationToken) => UniTask.CompletedTask;

        /// <summary> 画面から退出する演出処理 </summary>
        protected virtual UniTask ExitAsync(ExitType exitType, CancellationToken cancellationToken) => UniTask.CompletedTask;

        /// <summary> Viewをロードした時の処理 </summary>
        protected virtual void OnViewLoaded() { }

        /// <summary> Viewをアンロードする直前の処理 </summary>
        protected virtual void OnViewUnloaded() { }

        /// <summary> 破棄時の処理 </summary>
        protected virtual void Dispose() { }

        /// <summary> 画面の寿命に紐づく CancellationToken </summary>
        protected CancellationToken DisposeCancellationToken => _disposeCts.Token;

        #endregion

        private readonly List<FieldInfo> _unityViewFields;
        private readonly CancellationTokenSource _disposeCts = new();
        private bool _disposed;

        /// <summary> コンストラクタ </summary>
        protected ScreenBase()
        {
            _unityViewFields = UnityViewUtility.GetFields(GetType());
        }

        /// <summary> ビューのハンドル </summary>
        protected abstract IViewHandle ViewHandle { get; }

        /// <summary> Canvas の sortOrder を決めるためのレイヤーインデックス </summary>
        protected abstract int LayerIndex { get; }

        /// <summary> UnityViewフィールドに参照を注入します </summary>
        protected void InjectUnityView()
        {
            UnityViewUtility.Inject(this, ViewHandle.RootObjects, _unityViewFields);
        }

        /// <summary> UnityViewフィールドにnullを代入します </summary>
        protected void NullifyUnityView()
        {
            UnityViewUtility.Nullify(this, _unityViewFields);
        }

        /// <summary> Canvasの描画順を設定します </summary>
        protected void ApplyCanvasOrder()
        {
            CanvasOrderUtility.ApplyOrder(ViewHandle.RootObjects, LayerIndex);
        }

        #region IScreen

        /// <inheritdoc />
        async UniTask IScreen.LoadViewAsync(CancellationToken cancellationToken)
        {
            await ViewHandle.LoadAsync(cancellationToken);
            ApplyCanvasOrder();
            InjectUnityView();
            OnViewLoaded();
        }

        /// <inheritdoc />
        void IScreen.UnloadView()
        {
            OnViewUnloaded();
            NullifyUnityView();
            ViewHandle.Unload();
        }

        /// <inheritdoc />
        UniTask IScreen.OpenAsync(CancellationToken cancellationToken) => EnterAsync(EnterType.OnOpen, cancellationToken);

        /// <inheritdoc />
        UniTask IScreen.CloseAsync(CancellationToken cancellationToken) => ExitAsync(ExitType.OnClose, cancellationToken);

        /// <inheritdoc />
        UniTask IScreen.ResumeAsync(ResumeContext context, CancellationToken cancellationToken) => EnterAsync(EnterType.OnResume, cancellationToken);

        /// <inheritdoc />
        UniTask IScreen.PauseAsync(PauseContext context, CancellationToken cancellationToken) => ExitAsync(ExitType.OnPause, cancellationToken);

        void IDisposable.Dispose()
        {
            if (_disposed) return;
            Dispose();
            _disposeCts.Cancel();
            _disposeCts.Dispose();
            _disposed = true;
        }

        #endregion
    }
}
