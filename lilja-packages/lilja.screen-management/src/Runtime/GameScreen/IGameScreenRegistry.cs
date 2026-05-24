using System;
using System.Collections.Generic;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// 画面グループが生成可能な画面を登録するためのインターフェース。
    /// </summary>
    public interface IGameScreenRegistry
    {
        /// <summary>
        /// 画面を作成するファクトリをキー名指定で登録します。
        /// </summary>
        /// <typeparam name="TScreen">画面の型</typeparam>
        /// <typeparam name="TArgs">引数の型</typeparam>
        /// <param name="key">一意なキー名</param>
        void Register<TScreen, TArgs>(string key)
            where TScreen : GameScreen<TArgs>;

        /// <summary>
        /// 画面を作成するファクトリを型名指定で登録します。
        /// </summary>
        /// <typeparam name="TScreen">画面の型</typeparam>
        /// <typeparam name="TArgs">引数の型</typeparam>
        void Register<TScreen, TArgs>()
            where TScreen : GameScreen<TArgs>;

        /// <summary>
        /// 画面を作成する外部ファクトリをキー名指定で登録します。
        /// </summary>
        /// <typeparam name="TArgs">引数の型</typeparam>
        /// <param name="key">一意なキー名</param>
        /// <param name="factory">生成ファクトリデリゲート</param>
        void Register<TArgs>(string key, Func<GameScreen<TArgs>> factory);

        /// <summary>
        /// 画面を作成する外部ファクトリを型名指定で登録します。
        /// </summary>
        /// <typeparam name="TScreen">画面の型</typeparam>
        /// <typeparam name="TArgs">引数の型</typeparam>
        /// <param name="factory">生成ファクトリデリゲート</param>
        void Register<TScreen, TArgs>(Func<TScreen> factory)
            where TScreen : GameScreen<TArgs>;
    }

    /// <summary>
    /// 登録された画面ファクトリを管理する内部レジストリクラス。
    /// </summary>
    internal sealed class GameScreenRegistry : IGameScreenRegistry
    {
        private readonly Dictionary<string, Func<object>> _factories = new();

        /// <summary>
        /// キーに紐づく画面オブジェクトを作成します。
        /// </summary>
        /// <param name="key">キー名</param>
        /// <returns>生成された画面オブジェクト</returns>
        internal object Create(string key)
        {
            if (!_factories.TryGetValue(key, out var factory))
            {
                throw new InvalidOperationException($"[Lilja.ScreenManagement] 画面キー '{key}' はこの GameScreenGroup に登録されていません。Configure(IGameScreenRegistry) で登録されているか確認してください。");
            }
            return factory.Invoke();
        }

        #region IGameScreenRegistry

        /// <inheritdoc />
        public void Register<TScreen, TArgs>(string key)
            where TScreen : GameScreen<TArgs>
        {
            _factories[key] = () =>
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
        }

        /// <inheritdoc />
        public void Register<TScreen, TArgs>()
            where TScreen : GameScreen<TArgs>
        {
            Register<TScreen, TArgs>(typeof(TScreen).FullName);
        }

        /// <inheritdoc />
        public void Register<TArgs>(string key, Func<GameScreen<TArgs>> factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }
            _factories[key] = () => factory.Invoke();
        }

        /// <inheritdoc />
        public void Register<TScreen, TArgs>(Func<TScreen> factory)
            where TScreen : GameScreen<TArgs>
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }
            Register(typeof(TScreen).FullName, () => factory.Invoke());
        }

        #endregion
    }
}
