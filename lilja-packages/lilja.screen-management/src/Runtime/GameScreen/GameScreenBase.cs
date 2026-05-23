using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.ScreenManagement
{

    public abstract class GameScreenBase<TArgs> : IGameScreenInternal<TArgs>
    {
        private bool _disposed;

        protected GameScreenBase()
        {
            var connector = new GameScreenConnector { Owner = this };
            Context = new GameScreenContext(connector);
        }

        private IViewHandle _cachedViewHandle;

        protected internal GameScreenContext Context { get; }

        GameScreenContext IGameScreenInternal.Context => Context;

        protected internal abstract IViewHandle ViewHandle { get; }

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

        protected virtual UniTask InitializeAsync(TArgs args, CancellationToken cancellationToken)
        {
            return UniTask.CompletedTask;
        }

        protected virtual UniTask EnterAsync(
            EnterType enterType,
            CancellationToken cancellationToken
        )
        {
            return UniTask.CompletedTask;
        }

        protected virtual UniTask ExitAsync(ExitType exitType, CancellationToken cancellationToken)
        {
            return UniTask.CompletedTask;
        }

        protected virtual void DisposeCore() { }

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

        async UniTask IGameScreenInternal<TArgs>.OpenAsync(
            TArgs args,
            CancellationToken cancellationToken
        )
        {
            await InitializeAsync(args, cancellationToken);
            await EnterAsync(EnterType.OnOpen, cancellationToken);
        }

        UniTask IGameScreenInternal.CloseAsync(CancellationToken cancellationToken)
        {
            return ExitAsync(ExitType.OnClose, cancellationToken);
        }

        UniTask IGameScreenInternal.ResumeAsync(CancellationToken cancellationToken)
        {
            return EnterAsync(EnterType.OnResume, cancellationToken);
        }

        UniTask IGameScreenInternal.PauseAsync(CancellationToken cancellationToken)
        {
            return ExitAsync(ExitType.OnPause, cancellationToken);
        }

        protected virtual void OnDispose()
        {
            _cachedViewHandle = null;
        }
    }
}
