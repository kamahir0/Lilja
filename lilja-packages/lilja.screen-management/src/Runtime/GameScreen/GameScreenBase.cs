using System;
using System.Threading;
using Cysharp.Threading.Tasks;
#if LILJA_SCREEN_MANAGEMENT_R3_SUPPORT
using R3;
#endif

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// すべての画面クラスの共通基底クラス。
    /// </summary>
    /// <typeparam name="TArgs">画面の初期化に受け取る引数の型</typeparam>
    public abstract class GameScreenBase<TArgs> : IGameScreenInternal<TArgs>
    {
        #region Public / Protected Members

        // --- Fields ---
        // (No public or protected fields)

        // --- Properties ---

        /// <summary>
        /// この画面の表示と演出を担うビューハンドル。
        /// </summary>
        protected internal abstract IViewHandle ViewHandle { get; }

        /// <summary>
        /// この画面がビューを持たない論理画面であるかどうかを示す値を取得します。デフォルトは false です。
        /// </summary>
        public virtual bool IsViewless => false;

        /// <summary>
        /// この画面が所属する実行コンテキスト。
        /// </summary>
        public GameScreenContext Context { get; internal set; }

        /// <summary>
        /// この画面のソート順などの描画レイヤー。
        /// </summary>
        public int Layer { get; internal set; }

#if LILJA_SCREEN_MANAGEMENT_R3_SUPPORT
        /// <summary>
        /// 画面インスタンス全体の寿命に紐づく CompositeDisposable。
        /// Dispose 時に自動で破棄されます。
        /// </summary>
        public CompositeDisposable Lifetime { get; } = new();

        /// <summary>
        /// ビューの寿命（OnViewLoaded ～ OnViewUnloaded）に紐づく CompositeDisposable。
        /// OnViewUnloaded 時に自動でクリアされます。
        /// </summary>
        public CompositeDisposable ViewLifetime { get; } = new();
#endif

        // --- Constructors ---

        /// <summary>
        /// 新しい <see cref="GameScreenBase{TArgs}"/> インスタンスを初期化します。
        /// </summary>
        protected GameScreenBase() { }

        // --- Methods ---

        /// <summary>
        /// 画面遷移を事前に非同期ロードしてメモリにキャッシュします。
        /// </summary>
        public async UniTask PreloadViewAsync(
            GameScreenContext callerContext,
            CancellationToken cancellationToken = default
        )
        {
            var handle = ((IGameScreenInternal)this).GetViewHandle();
            Context = callerContext;

            handle.Initialize(GetType());
            await handle.PreloadAsync(Context, cancellationToken);
        }

        /// <summary>
        /// 画面がツリーに接続された後、入場演出が走る前に呼び出されます。
        /// </summary>
        protected virtual UniTask InitializeAsync(TArgs args, CancellationToken cancellationToken)
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 画面が活性化される際（新規入場または復帰時）に呼び出されます。
        /// </summary>
        protected virtual UniTask EnterAsync(
            EnterContext context,
            CancellationToken cancellationToken
        )
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 画面が非活性化される際（完全退出または一時停止時）に呼び出されます。
        /// </summary>
        protected virtual UniTask ExitAsync(
            ExitContext context,
            CancellationToken cancellationToken
        )
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 画面オブジェクトが所有するリソースを解放するためのクリーンアップ処理を記述します。
        /// </summary>
        protected virtual void DisposeCore() { }

        /// <summary>
        /// ビューアセットがロードされ、[View] 属性付きフィールドへの依存注入が完了した直後に呼び出されます。
        /// </summary>
        protected virtual void OnViewLoaded() { }

        /// <summary>
        /// ビューアセットが破棄され、[View] 属性付きフィールドが null クリアされる直前に呼び出されます。
        /// </summary>
        protected virtual void OnViewUnload() { }

        /// <summary>
        /// システム用の画面初期化フック。デフォルトではユーザーの <see cref="InitializeAsync"/> を呼び出します。
        /// </summary>
        protected virtual UniTask TriggerInitializeAsync(
            TArgs args,
            CancellationToken cancellationToken
        ) => InitializeAsync(args, cancellationToken);

        /// <summary>
        /// システム用の入場演出フック。デフォルトではユーザーの <see cref="EnterAsync"/> を呼び出します。
        /// </summary>
        protected virtual UniTask TriggerEnterAsync(
            EnterContext context,
            CancellationToken cancellationToken
        ) => EnterAsync(context, cancellationToken);

        /// <summary>
        /// システム用の退場演出フック。 デフォルトではユーザーの <see cref="ExitAsync"/> を呼び出します。
        /// </summary>
        protected virtual UniTask TriggerExitAsync(
            ExitContext context,
            CancellationToken cancellationToken
        ) => ExitAsync(context, cancellationToken);

        /// <summary>
        /// システム用のビューロード完了フック。デフォルトではユーザーの <see cref="OnViewLoaded"/> を呼び出します。
        /// </summary>
        protected virtual void TriggerOnViewLoaded()
        {
            OnViewLoaded();
        }

        /// <summary>
        /// システム用のビューアンロードフック。デフォルトではユーザーの <see cref="OnViewUnload"/> を呼び出します。
        /// </summary>
        protected virtual void TriggerOnViewUnload()
        {
            OnViewUnload();
        }

        /// <summary>
        /// 破棄前にツリー上の接続関係などの参照を外すための内部クリーンアップ処理。
        /// </summary>
        protected virtual void OnDispose()
        {
            _cachedViewHandle = null;
#if LILJA_SCREEN_MANAGEMENT_R3_SUPPORT
            ViewLifetime.Dispose();
            Lifetime.Dispose();
#endif
        }

        #endregion

        #region Internal / Private Members

        // --- Fields ---
        private bool _disposed;
        private IViewHandle _cachedViewHandle;

        // --- Properties ---
        /// <summary>
        /// この画面が現在クローズ処理中であるか。
        /// </summary>
        internal bool IsClosing { get; set; }

        // --- Methods ---
        // (No internal or private methods)

        #endregion

        #region IGameScreenInternal

        GameScreenContext IGameScreenInternal.Context => Context;

        bool IGameScreenInternal.IsViewless => IsViewless;

        int IGameScreenInternal.Layer
        {
            get => Layer;
            set => Layer = value;
        }

        bool IGameScreenInternal.IsClosing
        {
            get => IsClosing;
            set => IsClosing = value;
        }

        IViewHandle IGameScreenInternal.GetViewHandle()
        {
            if (_cachedViewHandle == null)
            {
                _cachedViewHandle = ViewHandle;
                if (_cachedViewHandle == null)
                {
                    throw new InvalidOperationException(
                        $"[Lilja.ScreenManagement] 画面 '{GetType().Name}' の ViewHandle プロパティが null を返しました。"
                    );
                }
            }
            return _cachedViewHandle;
        }

        UniTask IGameScreenInternal.ExecuteEnterAsync(
            EnterContext context,
            CancellationToken cancellationToken
        ) => TriggerEnterAsync(context, cancellationToken);

        UniTask IGameScreenInternal.ExecuteExitAsync(
            ExitContext context,
            CancellationToken cancellationToken
        ) => TriggerExitAsync(context, cancellationToken);

        void IGameScreenInternal.OnViewLoaded()
        {
            TriggerOnViewLoaded();
        }

        void IGameScreenInternal.OnViewUnload()
        {
            TriggerOnViewUnload();
#if LILJA_SCREEN_MANAGEMENT_R3_SUPPORT
            ViewLifetime.Clear();
#endif
        }

        #endregion

        #region IGameScreenInternal<TArgs>

        UniTask IGameScreenInternal<TArgs>.InitializeAsync(
            TArgs args,
            CancellationToken cancellationToken
        ) => TriggerInitializeAsync(args, cancellationToken);

        #endregion

        #region IDisposable

        void IDisposable.Dispose()
        {
            if (_disposed)
            {
                return;
            }

            OnDispose();
            DisposeCore();
            _disposed = true;
        }

        #endregion
    }
}
