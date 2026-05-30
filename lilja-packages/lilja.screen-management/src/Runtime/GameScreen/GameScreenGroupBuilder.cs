using System;
using System.Collections.Generic;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// 画面グループが生成可能な画面や遷移のカスタムルールを登録するためのビルダー。
    /// </summary>
    public sealed class GameScreenGroupBuilder
    {
        private readonly Dictionary<string, Func<object>> _factories = new();
        private readonly Dictionary<string, Type> _types = new();
        internal Dictionary<(Type From, Type To), ITransition> OverrideTransitionMap { get; } = new();

        /// <summary>
        /// 指定されたキー名がレジストリに登録されているか判定します。
        /// </summary>
        /// <param name="key">キー名</param>
        /// <returns>登録されていれば true</returns>
        internal bool Contains(string key)
        {
            return _factories.ContainsKey(key);
        }

        /// <summary>
        /// キーに紐づく画面の型を取得します。
        /// </summary>
        /// <param name="key">キー名</param>
        /// <returns>画面の型</returns>
        internal Type GetScreenType(string key)
        {
            if (!_types.TryGetValue(key, out var type))
            {
                throw new InvalidOperationException(
                    $"[Lilja.ScreenManagement] 画面キー '{key}' は登録されていません。"
                );
            }
            return type;
        }

        /// <summary>
        /// キーに紐づく画面オブジェクトを作成します。
        /// </summary>
        /// <param name="key">キー名</param>
        /// <returns>生成された画面オブジェクト</returns>
        internal object Create(string key)
        {
            if (!_factories.TryGetValue(key, out var factory))
            {
                throw new InvalidOperationException(
                    $"[Lilja.ScreenManagement] 画面キー '{key}' はこの GameScreenGroup に登録されていません。Configure(GameScreenGroupBuilder) で登録されているか確認してください。"
                );
            }
            return factory.Invoke();
        }

        /// <summary>
        /// 画面を作成するファクトリをキー名指定で登録します。
        /// </summary>
        /// <typeparam name="TScreen">画面の型</typeparam>
        /// <typeparam name="TArgs">引数の型</typeparam>
        /// <param name="key">一意なキー名</param>
        /// <returns>メソッドチェーン用のビルダーインスタンス</returns>
        public GameScreenGroupBuilder Register<TScreen, TArgs>(string key)
            where TScreen : GameScreen<TArgs>
        {
            _types[key] = typeof(TScreen);
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
            return this;
        }

        /// <summary>
        /// 画面を作成するファクトリを型名指定で登録します。
        /// </summary>
        /// <typeparam name="TScreen">画面の型</typeparam>
        /// <typeparam name="TArgs">引数の型</typeparam>
        /// <returns>メソッドチェーン用のビルダーインスタンス</returns>
        public GameScreenGroupBuilder Register<TScreen, TArgs>()
            where TScreen : GameScreen<TArgs>
        {
            return Register<TScreen, TArgs>(typeof(TScreen).FullName);
        }

        /// <summary>
        /// 画面を作成する外部ファクトリをキー名指定で登録します。
        /// 具体的な型情報が失われるため、可能であれば <see cref="Register{TScreen,TArgs}(string,Func{TScreen})"/> の使用を推奨します。
        /// </summary>
        /// <typeparam name="TArgs">引数の型</typeparam>
        /// <param name="key">一意なキー名</param>
        /// <param name="factory">生成ファクトリデリゲート</param>
        /// <returns>メソッドチェーン用のビルダーインスタンス</returns>
        public GameScreenGroupBuilder Register<TArgs>(string key, Func<GameScreen<TArgs>> factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }
            // 初回は暫定的に基底型を登録するが、ファクトリが実行されたタイミングで具体的な型に遅延確定する。
            _types[key] = typeof(GameScreen<TArgs>);
            _factories[key] = () =>
            {
                var screen = factory.Invoke();
                if (screen != null)
                {
                    _types[key] = screen.GetType();
                }
                return screen;
            };
            return this;
        }

        /// <summary>
        /// 画面を作成する外部ファクトリをキー名指定で登録します（具体的な型情報を正確に記録します）。
        /// </summary>
        /// <typeparam name="TScreen">画面の型</typeparam>
        /// <typeparam name="TArgs">引数の型</typeparam>
        /// <param name="key">一意なキー名</param>
        /// <param name="factory">生成ファクトリデリゲート</param>
        /// <returns>メソッドチェーン用のビルダーインスタンス</returns>
        public GameScreenGroupBuilder Register<TScreen, TArgs>(string key, Func<TScreen> factory)
            where TScreen : GameScreen<TArgs>
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }
            // 具体型 TScreen を正確に記録する
            _types[key] = typeof(TScreen);
            _factories[key] = () => factory.Invoke();
            return this;
        }

        /// <summary>
        /// 画面を作成する外部ファクトリを型名指定で登録します。
        /// </summary>
        /// <typeparam name="TScreen">画面の型</typeparam>
        /// <typeparam name="TArgs">引数の型</typeparam>
        /// <param name="factory">生成ファクトリデリゲート</param>
        /// <returns>メソッドチェーン用のビルダーインスタンス</returns>
        public GameScreenGroupBuilder Register<TScreen, TArgs>(Func<TScreen> factory)
            where TScreen : GameScreen<TArgs>
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }
            _types[typeof(TScreen).FullName] = typeof(TScreen);
            return Register<TScreen, TArgs>(typeof(TScreen).FullName, factory);
        }

        /// <summary>
        /// 画面遷移元と遷移先の組み合わせに応じた一時差し替えトランジションを登録します。
        /// </summary>
        /// <typeparam name="TFrom">遷移元の画面の型</typeparam>
        /// <typeparam name="TTo">遷移先の画面の型</typeparam>
        /// <param name="transition">一時差し替え用のトランジション演出</param>
        /// <returns>メソッドチェーン用のビルダーインスタンス</returns>
        public GameScreenGroupBuilder OverrideTransition<TFrom, TTo>(ITransition transition)
            where TFrom : IGameScreen
            where TTo : IGameScreen
        {
            OverrideTransitionMap[(typeof(TFrom), typeof(TTo))] = transition;
            return this;
        }
    }
}
