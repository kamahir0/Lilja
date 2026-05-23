using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// 画面システムに参加するユーザー定義画面オブジェクトの共通マーカーインターフェース。
    /// </summary>
    public interface IGameScreen : IDisposable { }

    /// <summary>
    /// 画面のクローズ、再開、一時停止など、ランタイムが汎用的に操作するための内部インターフェース。
    /// </summary>
    internal interface IGameScreenInternal : IGameScreen
    {
        /// <summary>
        /// 伝播された画面遷移コンテキストを取得します。
        /// </summary>
        GameScreenContext Context { get; }

        /// <summary>
        /// この画面が所有するビューハンドルを取得または遅延解決します。
        /// </summary>
        /// <returns>ビューハンドルインスタンス</returns>
        IViewHandle GetViewHandle();

        /// <summary>
        /// 画面を閉じます。
        /// </summary>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>非同期タスク</returns>
        UniTask CloseAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 画面を再開します。
        /// </summary>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>非同期タスク</returns>
        UniTask ResumeAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 画面を一時停止します。
        /// </summary>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>非同期タスク</returns>
        UniTask PauseAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    /// 引数を伴う画面の初期化・オープンを行うための内部インターフェース。
    /// </summary>
    /// <typeparam name="TArgs">初期化引数の型</typeparam>
    internal interface IGameScreenInternal<in TArgs> : IGameScreenInternal
    {
        /// <summary>
        /// 画面を初期化してオープンします。
        /// </summary>
        /// <param name="args">初期化引数</param>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>非同期タスク</returns>
        UniTask OpenAsync(TArgs args, CancellationToken cancellationToken);
    }

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
                throw new InvalidOperationException($"Screen key '{key}' is not registered.");
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
                        $"{typeof(TScreen).Name} could not be created."
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
