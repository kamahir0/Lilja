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
        #region For Implementers

        /// <summary>
        /// この画面の表示と演出を担うビューハンドル。
        /// </summary>
        protected internal abstract IViewHandle ViewHandle { get; }

        /// <summary>
        /// 画面がツリーに接続された後、入場演出が走る前に呼び出されます。
        /// </summary>
        /// <param name="args">初期化用引数</param>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>非同期タスク</returns>
        protected virtual UniTask InitializeAsync(TArgs args, CancellationToken cancellationToken)
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 画面が活性化される際（新規入場または復帰時）に呼び出されます。
        /// </summary>
        /// <remarks>
        /// コンテキスト内の <see cref="EnterContext.Transition"/> を手動で再生・制御することができます。<br/>
        /// 手動再生（PlayAsync）を行う場合は、演出の完了を保証するために<b>必ず await してください。</b>（Forget() は避けてください）<br/>
        /// このメソッド内で手動再生されなかった場合は、メソッドを抜けた後にシステム側で自動フォールバック再生されます。
        /// </remarks>
        /// <param name="context">入場遷移のコンテキスト</param>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>非同期タスク</returns>
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
        /// <remarks>
        /// コンテキスト内の <see cref="ExitContext.Transition"/> を手動で再生・制御することができます。<br/>
        /// 手動再生（PlayAsync）を行う場合は、演出の完了を保証するために<b>必ず await してください。</b>（Forget() は避けてください）<br/>
        /// このメソッド内で手動再生されなかった場合は、メソッドを抜けた後にシステム側で自動フォールバック再生されます。
        /// </remarks>
        /// <param name="context">退場遷移のコンテキスト</param>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>非同期タスク</returns>
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
        protected virtual void OnViewUnloaded() { }

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

        #endregion

        #region Trigger Life Cycle

        /// <summary>
        /// システム用の画面初期化フック。デフォルトではユーザーの <see cref="InitializeAsync"/> を呼び出します。
        /// </summary>
        protected virtual async UniTask TriggerInitializeAsync(
            TArgs args,
            CancellationToken cancellationToken
        )
        {
            await InitializeAsync(args, cancellationToken);
        }

        /// <summary>
        /// システム用の入場演出フック。デフォルトではユーザーの <see cref="EnterAsync"/> を呼び出します。
        /// </summary>
        protected virtual async UniTask TriggerEnterAsync(
            EnterContext context,
            CancellationToken cancellationToken
        )
        {
            await EnterAsync(context, cancellationToken);
        }

        /// <summary>
        /// システム用の退場演出フック。 デフォルトではユーザーの <see cref="ExitAsync"/> を呼び出します。
        /// </summary>
        protected virtual async UniTask TriggerExitAsync(
            ExitContext context,
            CancellationToken cancellationToken
        )
        {
            await ExitAsync(context, cancellationToken);
        }

        /// <summary>
        /// システム用のビューロード完了フック。デフォルトではユーザーの <see cref="OnViewLoaded"/> を呼び出します。
        /// </summary>
        protected virtual void TriggerOnViewLoaded()
        {
            OnViewLoaded();
        }

        /// <summary>
        /// システム用のビューアンロードフック。デフォルトではユーザーの <see cref="OnViewUnloaded"/> を呼び出します。
        /// </summary>
        protected virtual void TriggerOnViewUnloaded()
        {
            OnViewUnloaded();
        }

        #endregion

        private bool _disposed;
        private IViewHandle _cachedViewHandle;

        /// <summary>
        /// 新しい <see cref="GameScreenBase{TArgs}"/> インスタンスを初期化します。
        /// </summary>
        protected GameScreenBase()
        {
        }

        /// <summary>
        /// この画面が所属する実行コンテキスト。
        /// </summary>
        public GameScreenContext Context { get; internal set; }

        /// <summary>
        /// この画面のソート順などの描画レイヤー。
        /// </summary>
        public int Layer { get; internal set; }

        /// <inheritdoc />
        int IGameScreenInternal.Layer
        {
            get => Layer;
            set => Layer = value;
        }

        /// <summary>
        /// この画面が現在クローズ処理中であるか。
        /// </summary>
        internal bool IsClosing { get; set; }

        /// <inheritdoc />
        bool IGameScreenInternal.IsClosing
        {
            get => IsClosing;
            set => IsClosing = value;
        }

        /// <summary>
        /// 画面がツリーに接続される前に、指定されたアセットプロバイダー等を用いてビューのアセットを事前に非同期ロードしてメモリにキャッシュします。
        /// </summary>
        /// <param name="prefabProvider">プレハブアセットプロバイダー</param>
        /// <param name="sceneLoader">シーンロードサービス</param>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>非同期タスク</returns>
        public async UniTask PreloadViewAsync(
            IPrefabProvider prefabProvider = null,
            ISceneLoader sceneLoader = null,
            CancellationToken cancellationToken = default
        )
        {
            var handle = ((IGameScreenInternal)this).GetViewHandle();
            if (handle == null)
            {
                return;
            }

            if (Context == null)
            {
                Context = new GameScreenContext();
            }
            if (prefabProvider != null) Context.PrefabProvider = prefabProvider;
            if (sceneLoader != null) Context.SceneLoader = sceneLoader;
            handle.Initialize(GetType());
            await handle.PreloadAsync(Context, cancellationToken);
        }

        #region IGameScreenInternal

        /// <inheritdoc />
        GameScreenContext IGameScreenInternal.Context => Context;

        /// <inheritdoc />
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

        /// <inheritdoc />
        async UniTask IGameScreenInternal<TArgs>.OpenAsync(
            TArgs args,
            Type previousScreenType,
            ITransition overrideTransition,
            CancellationToken cancellationToken
        )
        {
            await TriggerInitializeAsync(args, cancellationToken);

            var transition = overrideTransition ?? Context?.Transition;
            var transitionHandle = new TransitionHandle(transition, false);
            var enterContext = new EnterContext(
                EnterType.OnOpen,
                previousScreenType,
                transitionHandle
            );

            await TriggerEnterAsync(enterContext, cancellationToken);

            if (!transitionHandle.IsPlayed)
            {
                await transitionHandle.PlayAsync(cancellationToken);
            }
        }

        /// <inheritdoc />
        async UniTask IGameScreenInternal.CloseAsync(
            Type nextScreenType,
            ITransition overrideTransition,
            CancellationToken cancellationToken
        )
        {
            var transition = overrideTransition ?? Context?.Transition;
            var transitionHandle = new TransitionHandle(transition, true);
            var exitContext = new ExitContext(ExitType.OnClose, nextScreenType, transitionHandle);

            await TriggerExitAsync(exitContext, cancellationToken);

            if (!transitionHandle.IsPlayed)
            {
                await transitionHandle.PlayAsync(cancellationToken);
            }
        }

        /// <inheritdoc />
        async UniTask IGameScreenInternal.ResumeAsync(
            Type previousScreenType,
            ITransition overrideTransition,
            CancellationToken cancellationToken
        )
        {
            var transition = overrideTransition ?? Context?.Transition;
            var transitionHandle = new TransitionHandle(transition, false);
            var enterContext = new EnterContext(
                EnterType.OnResume,
                previousScreenType,
                transitionHandle
            );

            await TriggerEnterAsync(enterContext, cancellationToken);

            if (!transitionHandle.IsPlayed)
            {
                await transitionHandle.PlayAsync(cancellationToken);
            }
        }

        /// <inheritdoc />
        async UniTask IGameScreenInternal.PauseAsync(
            Type nextScreenType,
            ITransition overrideTransition,
            CancellationToken cancellationToken
        )
        {
            var transition = overrideTransition ?? Context?.Transition;
            var transitionHandle = new TransitionHandle(transition, true);
            var exitContext = new ExitContext(ExitType.OnPause, nextScreenType, transitionHandle);

            await TriggerExitAsync(exitContext, cancellationToken);

            if (!transitionHandle.IsPlayed)
            {
                await transitionHandle.PlayAsync(cancellationToken);
            }
        }

        /// <inheritdoc />
        void IGameScreenInternal.OnViewLoaded()
        {
            TriggerOnViewLoaded();
        }

        /// <inheritdoc />
        void IGameScreenInternal.OnViewUnloaded()
        {
            TriggerOnViewUnloaded();
#if LILJA_SCREEN_MANAGEMENT_R3_SUPPORT
            ViewLifetime.Clear();
#endif
        }

        #endregion

        #region IDisposable

        /// <inheritdoc />
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
    }
}
