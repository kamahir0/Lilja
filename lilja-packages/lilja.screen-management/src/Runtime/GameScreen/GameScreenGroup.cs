using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.ScreenManagement
{

    public class GameScreenGroup
    {
        private bool _configured;

        public GameScreenGroup()
        {
            var connector = new GameScreenConnector { Owner = this };
            Context = new GameScreenContext(connector);
        }

        protected internal GameScreenContext Context { get; }

        internal GameScreenRegistry Registry { get; } = new();

        internal SemaphoreSlim Gate { get; } = new(1, 1);

        internal UniTaskCompletionSource<ValueTuple> CompletionSource { get; } = new();

        protected virtual void Configure(IGameScreenRegistry registry) { }

        internal void ConfigureInternal()
        {
            if (_configured)
            {
                return;
            }

            Configure(Registry);
            _configured = true;
        }

        public UniTask CallAsync<TArgs>(
            GameScreenContext callerContext,
            string initialScreenKey,
            TArgs initialScreenArgs,
            CancellationToken cancellationToken = default
        )
        {
            if (callerContext == null)
            {
                throw new ArgumentNullException(nameof(callerContext));
            }

            return Procedures.Group.CallAsync(
                callerContext,
                this,
                initialScreenKey,
                initialScreenArgs,
                cancellationToken
            );
        }

        public UniTask CallAsync<TScreen, TArgs>(
            GameScreenContext callerContext,
            TArgs initialScreenArgs,
            CancellationToken cancellationToken = default
        )
            where TScreen : GameScreen<TArgs>
        {
            return CallAsync(
                callerContext,
                typeof(TScreen).FullName,
                initialScreenArgs,
                cancellationToken
            );
        }

        public UniTask SwitchAsync<TArgs>(
            string key,
            TArgs args,
            CancellationToken cancellationToken = default
        )
        {
            return Procedures.Group.SwitchAsync(this, key, args, cancellationToken);
        }

        public UniTask SwitchAsync<TScreen, TArgs>(
            TArgs args,
            CancellationToken cancellationToken = default
        )
            where TScreen : GameScreen<TArgs>
        {
            return SwitchAsync(typeof(TScreen).FullName, args, cancellationToken);
        }

        public void Complete()
        {
            CompletionSource.TrySetResult(new ValueTuple());
        }

        public void Fail(Exception exception)
        {
            CompletionSource.TrySetException(exception);
        }

        public void Cancel()
        {
            CompletionSource.TrySetCanceled();
        }

        public static GameScreenGroup Create(Action<IGameScreenRegistry> configure)
        {
            return new ConfiguredGameScreenGroup(configure);
        }

        private sealed class ConfiguredGameScreenGroup : GameScreenGroup
        {
            private readonly Action<IGameScreenRegistry> _configure;

            public ConfiguredGameScreenGroup(Action<IGameScreenRegistry> configure)
            {
                _configure = configure ?? throw new ArgumentNullException(nameof(configure));
            }

            protected override void Configure(IGameScreenRegistry registry)
            {
                _configure.Invoke(registry);
            }
        }
    }
}
