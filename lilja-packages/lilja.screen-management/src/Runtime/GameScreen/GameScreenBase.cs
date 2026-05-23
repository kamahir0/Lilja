using System;
using System.Threading;
using Cysharp.Threading.Tasks;

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
        /// <param name="enterType">入場遷移の種類</param>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>非同期タスク</returns>
        protected virtual UniTask EnterAsync(
            EnterType enterType,
            CancellationToken cancellationToken
        )
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 画面が非活性化される際（完全退出または一時停止時）に呼び出されます。
        /// </summary>
        /// <param name="exitType">退場遷移の種類</param>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>非同期タスク</returns>
        protected virtual UniTask ExitAsync(ExitType exitType, CancellationToken cancellationToken)
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 画面オブジェクトが所有するリソースを解放するためのクリーンアップ処理を記述します。
        /// </summary>
        protected virtual void DisposeCore() { }

        #endregion

        private bool _disposed;
        private IViewHandle _cachedViewHandle;

        /// <summary>
        /// 新しい <see cref="GameScreenBase{TArgs}"/> インスタンスを初期化します。
        /// </summary>
        protected GameScreenBase()
        {
            var connector = new GameScreenConnector { Owner = this };
            Context = new GameScreenContext(connector);
        }

        /// <summary>
        /// この画面が所属する実行コンテキスト。
        /// </summary>
        protected internal GameScreenContext Context { get; }

        /// <summary>
        /// 画面がツリーに接続される前に、指定されたカスタムオプションを用いてビューのアセットを事前に非同期ロードしてメモリにキャッシュします。
        /// </summary>
        /// <param name="options">アセットロードに使用するカスタムオプション</param>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>非同期タスク</returns>
        public async UniTask PreloadViewAsync(
            GameScreenOptions options,
            CancellationToken cancellationToken = default
        )
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var handle = ((IGameScreenInternal)this).GetViewHandle();
            if (handle == null)
            {
                return;
            }

            Context.Options = options;
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
                        $"ViewHandle property returned null in screen '{GetType().Name}'."
                    );
                }
            }
            return _cachedViewHandle;
        }

        /// <inheritdoc />
        async UniTask IGameScreenInternal<TArgs>.OpenAsync(
            TArgs args,
            CancellationToken cancellationToken
        )
        {
            await InitializeAsync(args, cancellationToken);
            await EnterAsync(EnterType.OnOpen, cancellationToken);
        }

        /// <inheritdoc />
        UniTask IGameScreenInternal.CloseAsync(CancellationToken cancellationToken)
        {
            return ExitAsync(ExitType.OnClose, cancellationToken);
        }

        /// <inheritdoc />
        UniTask IGameScreenInternal.ResumeAsync(CancellationToken cancellationToken)
        {
            return EnterAsync(EnterType.OnResume, cancellationToken);
        }

        /// <inheritdoc />
        UniTask IGameScreenInternal.PauseAsync(CancellationToken cancellationToken)
        {
            return ExitAsync(ExitType.OnPause, cancellationToken);
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
        }
    }
}
