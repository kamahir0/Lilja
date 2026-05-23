using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.ScreenManagement
{

    public interface IGameScreen : IDisposable { }

    internal interface IGameScreenInternal : IGameScreen
    {

        GameScreenContext Context { get; }

        IViewHandle GetViewHandle();

        UniTask CloseAsync(CancellationToken cancellationToken);

        UniTask ResumeAsync(CancellationToken cancellationToken);

        UniTask PauseAsync(CancellationToken cancellationToken);
    }

    internal interface IGameScreenInternal<in TArgs> : IGameScreenInternal
    {

        UniTask OpenAsync(TArgs args, CancellationToken cancellationToken);
    }

    public interface IGameScreenRegistry
    {

        void Register<TScreen, TArgs>(string key)
            where TScreen : GameScreen<TArgs>;

        void Register<TScreen, TArgs>()
            where TScreen : GameScreen<TArgs>;

        void Register<TArgs>(string key, Func<GameScreen<TArgs>> factory);

        void Register<TScreen, TArgs>(Func<TScreen> factory)
            where TScreen : GameScreen<TArgs>;
    }

    internal sealed class GameScreenRegistry : IGameScreenRegistry
    {
        private readonly Dictionary<string, Func<object>> _factories = new();

        public void Register<TScreen, TArgs>(string key)
            where TScreen : GameScreen<TArgs>
        {
            _factories[key] = () =>
            {
                var instance = Activator.CreateInstance(typeof(TScreen));
                if (instance == null)
                {
                    throw new InvalidOperationException(
                        $"{typeof(TScreen).Name} could not be created."
                    );
                }
                return instance;
            };
        }

        public void Register<TScreen, TArgs>()
            where TScreen : GameScreen<TArgs>
        {
            Register<TScreen, TArgs>(typeof(TScreen).FullName);
        }

        public void Register<TArgs>(string key, Func<GameScreen<TArgs>> factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }
            _factories[key] = () => factory.Invoke();
        }

        public void Register<TScreen, TArgs>(Func<TScreen> factory)
            where TScreen : GameScreen<TArgs>
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }
            Register(typeof(TScreen).FullName, () => factory.Invoke());
        }

        internal object Create(string key)
        {
            if (!_factories.TryGetValue(key, out var factory))
            {
                throw new InvalidOperationException($"Screen key '{key}' is not registered.");
            }
            return factory.Invoke();
        }
    }
}
