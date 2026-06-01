#if LILJA_SCREEN_MANAGEMENT_R3_SUPPORT
using System;
using R3;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// GameScreen の R3 サポート拡張メソッド。
    /// </summary>
    public static class GameScreenR3Extensions
    {
        /// <summary>
        /// 画面インスタンス全体の寿命（<see cref="GameScreenBase{TArgs}.Lifetime"/>）に IDisposable を登録します。
        /// 画面が破棄されるタイミングで自動的に破棄されます。
        /// </summary>
        /// <typeparam name="T">IDisposable を実装する型</typeparam>
        /// <typeparam name="TArgs">画面の初期化引数の型</typeparam>
        /// <param name="disposable">登録する Disposable</param>
        /// <param name="screen">登録先の画面</param>
        /// <returns>登録した Disposable（メソッドチェーン用）</returns>
        public static T AddTo<T, TArgs>(this T disposable, GameScreenBase<TArgs> screen)
            where T : IDisposable
        {
            screen.Lifetime.Add(disposable);
            return disposable;
        }
    }
}
#endif
