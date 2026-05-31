using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// 画面遷移とロードのインフラストラクチャおよび手続きを管理するモジュール。
    /// </summary>
    public static partial class Procedures
    {
        /// <summary>
        /// 画面をリストに登録し、アセット準備（Prepare）およびオープン演出（Open）をアトミックに実行します。
        /// エラー発生時は自動的にリストから安全に削除・ティアダウンして整合性を保ちます。
        /// </summary>
        internal static async UniTask PrepareAndOpenAsync<TArgs>(
            GameScreenContext context,
            IGameScreenInternal<TArgs> screen,
            TArgs args,
            Type previousScreenType,
            ITransition transition,
            CancellationToken cancellationToken
        )
        {
            var list = context.ActiveScreensInternal;
            list.Add(screen);

            try
            {
                // 先にInitializeAsyncを実行
                await screen.InitializeAsync(args, cancellationToken);

                // アセットのロード＆配置（低レイヤーインフラへの委譲）
                await Screen.PrepareAsync(screen, cancellationToken);

                // オープン演出（フェードイン等）
                await ExecuteEnterWithTransitionAsync(
                    screen,
                    EnterType.OnOpen,
                    previousScreenType,
                    transition,
                    false,
                    cancellationToken
                );
            }
            catch
            {
                if (list.Contains(screen))
                {
                    list.Remove(screen);
                    await Screen.TeardownAsync(screen, CancellationToken.None);
                }
                throw;
            }
        }

        /// <summary>
        /// 画面への入場演出・処理を、トランジションハンドルとコンテキストを内部で自動生成し、フォールバック再生とあわせて非同期で実行します。
        /// </summary>
        /// <param name="screen">入場させる画面オブジェクト</param>
        /// <param name="enterType">入場遷移の種類</param>
        /// <param name="previousScreenType">遷移元（手前）の画面 of 型</param>
        /// <param name="transition">使用するトランジション演出</param>
        /// <param name="isReverse">トランジション演出を逆再生するかどうか</param>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>非同期タスク</returns>
        internal static async UniTask ExecuteEnterWithTransitionAsync(
            IGameScreenInternal screen,
            EnterType enterType,
            Type previousScreenType,
            ITransition transition,
            bool isReverse,
            CancellationToken cancellationToken
        )
        {
            var transitionHandle = new TransitionHandle(transition, isReverse);
            var context = new EnterContext(enterType, previousScreenType, transitionHandle);
            await screen.ExecuteEnterAsync(context, cancellationToken);
            if (!context.Transition.IsPlayed && !screen.IsViewless)
            {
                await context.Transition.PlayAsync(cancellationToken);
            }
        }

        /// <summary>
        /// 画面からの退場演出・処理を、トランジションハンドルとコンテキストを内部で自動生成し、フォールバック再生とあわせて非同期で実行します。
        /// </summary>
        /// <param name="screen">退場させる画面オブジェクト</param>
        /// <param name="exitType">退場遷移の種類</param>
        /// <param name="nextScreenType">遷移先（次）の画面 of 型</param>
        /// <param name="transition">使用するトランジション演出</param>
        /// <param name="isReverse">トランジション演出を逆再生するかどうか</param>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>非同期タスク</returns>
        internal static async UniTask ExecuteExitWithTransitionAsync(
            IGameScreenInternal screen,
            ExitType exitType,
            Type nextScreenType,
            ITransition transition,
            bool isReverse,
            CancellationToken cancellationToken
        )
        {
            var transitionHandle = new TransitionHandle(transition, isReverse);
            var context = new ExitContext(exitType, nextScreenType, transitionHandle);
            await screen.ExecuteExitAsync(context, cancellationToken);
            if (!context.Transition.IsPlayed && !screen.IsViewless)
            {
                await context.Transition.PlayAsync(cancellationToken);
            }
        }
    }
}
