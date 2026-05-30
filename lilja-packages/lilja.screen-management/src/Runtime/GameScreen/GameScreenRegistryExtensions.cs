using System;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// <see cref="IGameScreenRegistry"/> の拡張メソッドクラス。
    /// </summary>
    public static class GameScreenRegistryExtensions
    {
        /// <summary>
        /// 画面遷移元と遷移先の組み合わせに応じた一時差し替えトランジションを登録します。
        /// </summary>
        /// <typeparam name="TFrom">遷移元の画面の型</typeparam>
        /// <typeparam name="TTo">遷移先の画面の型</typeparam>
        /// <param name="registry">登録先レジストリ</param>
        /// <param name="transition">一時差し替え用のトランジション演出</param>
        /// <returns>メソッドチェーン用のレジストリインスタンス</returns>
        public static IGameScreenRegistry OverrideTransition<TFrom, TTo>(
            this IGameScreenRegistry registry,
            ITransition transition
        )
            where TFrom : IGameScreen
            where TTo : IGameScreen
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            if (registry is GameScreenRegistry internalRegistry)
            {
                internalRegistry.OverrideTransitionMap[(typeof(TFrom), typeof(TTo))] = transition;
            }

            return registry;
        }
    }
}
