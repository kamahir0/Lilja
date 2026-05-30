using System;
using System.Collections.Generic;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// 登録されたデータを保持する、一時的に利用される内部的な具象ビルダー実装。
    /// </summary>
    internal sealed class GameScreenGroupBuilder : IGameScreenGroupBuilder
    {
        internal readonly Dictionary<string, Func<object>> Factories = new();
        internal readonly Dictionary<string, Type> Types = new();
        internal readonly Dictionary<(Type From, Type To), ITransition> OverrideTransitionMap =
            new();

        /// <inheritdoc />
        public IGameScreenGroupBuilder Register<TScreen, TArgs>(string key)
            where TScreen : GameScreen<TArgs>
        {
            Types[key] = typeof(TScreen);
            Factories[key] = () =>
            {
                var instance = Activator.CreateInstance(typeof(TScreen));
                if (instance == null)
                {
                    throw new InvalidOperationException(
                        $"[Lilja.ScreenManagement] {typeof(TScreen).Name} を生成できませんでした。"
                    );
                }
                return instance;
            };
            return this;
        }

        /// <inheritdoc />
        public IGameScreenGroupBuilder Register<TArgs>(string key, Func<GameScreen<TArgs>> factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }
            Types[key] = typeof(GameScreen<TArgs>);
            Factories[key] = () =>
            {
                var screen = factory.Invoke();
                if (screen != null)
                {
                    Types[key] = screen.GetType();
                }
                return screen;
            };
            return this;
        }

        /// <inheritdoc />
        public IGameScreenGroupBuilder Register<TScreen, TArgs>(string key, Func<TScreen> factory)
            where TScreen : GameScreen<TArgs>
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }
            Types[key] = typeof(TScreen);
            Factories[key] = () => factory.Invoke();
            return this;
        }

        /// <inheritdoc />
        public IGameScreenGroupBuilder OverrideTransition<TFrom, TTo>(ITransition transition)
            where TFrom : IGameScreen
            where TTo : IGameScreen
        {
            OverrideTransitionMap[(typeof(TFrom), typeof(TTo))] = transition;
            return this;
        }
    }
}
