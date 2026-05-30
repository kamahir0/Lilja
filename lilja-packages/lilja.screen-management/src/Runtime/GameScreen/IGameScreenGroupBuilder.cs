using System;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// 画面グループに画面や一時差し替えトランジションを登録するためのビルダーインターフェース。
    /// </summary>
    public interface IGameScreenGroupBuilder
    {
        /// <summary>
        /// 画面を作成するファクトリをキー名指定で登録します。
        /// </summary>
        /// <typeparam name="TScreen">画面の型</typeparam>
        /// <typeparam name="TArgs">引数の型</typeparam>
        /// <param name="key">一意なキー名</param>
        /// <returns>メソッドチェーン用のビルダーインターフェース</returns>
        IGameScreenGroupBuilder Register<TScreen, TArgs>(string key)
            where TScreen : GameScreen<TArgs>;

        /// <summary>
        /// 画面を作成するファクトリを型名指定で登録します。
        /// </summary>
        /// <typeparam name="TScreen">画面の型</typeparam>
        /// <typeparam name="TArgs">引数の型</typeparam>
        /// <returns>メソッドチェーン用のビルダーインターフェース</returns>
        IGameScreenGroupBuilder Register<TScreen, TArgs>()
            where TScreen : GameScreen<TArgs>;

        /// <summary>
        /// 画面を作成する外部ファクトリをキー名指定で登録します。
        /// </summary>
        /// <typeparam name="TArgs">引数の型</typeparam>
        /// <param name="key">一意なキー名</param>
        /// <param name="factory">生成ファクトリデリゲート</param>
        /// <returns>メソッドチェーン用のビルダーインターフェース</returns>
        IGameScreenGroupBuilder Register<TArgs>(string key, Func<GameScreen<TArgs>> factory);

        /// <summary>
        /// 画面を作成する外部ファクトリをキー名指定で登録します（具体的な型情報を正確に記録します）。
        /// </summary>
        /// <typeparam name="TScreen">画面の型</typeparam>
        /// <typeparam name="TArgs">引数の型</typeparam>
        /// <param name="key">一意なキー名</param>
        /// <param name="factory">生成ファクトリデリゲート</param>
        /// <returns>メソッドチェーン用のビルダーインターフェース</returns>
        IGameScreenGroupBuilder Register<TScreen, TArgs>(string key, Func<TScreen> factory)
            where TScreen : GameScreen<TArgs>;

        /// <summary>
        /// 画面を作成する外部ファクトリを型名指定で登録します。
        /// </summary>
        /// <typeparam name="TScreen">画面の型</typeparam>
        /// <typeparam name="TArgs">引数の型</typeparam>
        /// <param name="factory">生成ファクトリデリゲート</param>
        /// <returns>メソッドチェーン用のビルダーインターフェース</returns>
        IGameScreenGroupBuilder Register<TScreen, TArgs>(Func<TScreen> factory)
            where TScreen : GameScreen<TArgs>;

        /// <summary>
        /// 画面遷移元と遷移先の組み合わせに応じた一時差し替えトランジションを登録します。
        /// </summary>
        /// <typeparam name="TFrom">遷移元の画面の型</typeparam>
        /// <typeparam name="TTo">遷移先の画面の型</typeparam>
        /// <param name="transition">一時差し替え用のトランジション演出</param>
        /// <returns>メソッドチェーン用のビルダーインターフェース</returns>
        IGameScreenGroupBuilder OverrideTransition<TFrom, TTo>(ITransition transition)
            where TFrom : IGameScreen
            where TTo : IGameScreen;
    }
}
