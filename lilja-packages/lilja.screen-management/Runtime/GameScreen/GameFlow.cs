using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// ビューを持たない論理画面として機能し、複数の画面遷移シナリオをひとまとめに制御するための基底クラス。
    /// </summary>
    /// <typeparam name="TArgs">フローの開始に必要な引数の型</typeparam>
    /// <typeparam name="TResult">フローが最終的に返却する結果の型</typeparam>
    public abstract class GameFlow<TArgs, TResult> : GameScreenBase<TArgs>
    {
        /// <inheritdoc />
        protected internal sealed override IViewHandle ViewHandle => ViewlessViewHandle.Instance;

        /// <inheritdoc />
        public sealed override bool IsViewless => true;

        /// <summary>
        /// 指定された呼び出し元のコンテキストの下でこの論理フローを起動し、内部遷移を実行して結果が返るまで非同期待機します。
        /// </summary>
        /// <param name="callerContext">呼び出し側の画面コンテキスト</param>
        /// <param name="args">開始引数</param>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>フロー結果オブジェクト</returns>
        public async UniTask<TResult> CallAsync(
            GameScreenContext callerContext,
            TArgs args,
            CancellationToken cancellationToken = default
        )
        {
            Context = callerContext ?? throw new ArgumentNullException(nameof(callerContext));

            IGameScreenInternal callerScreen = null;
            var list = callerContext.ActiveScreensInternal;
            if (list.Count > 0)
            {
                callerScreen = list[^1];
            }

            Layer = callerScreen != null ? callerScreen.Layer + 1 : 0;

            // 1. スタックのロールバック境界インデックスを記録
            var startIndex = list.Count;

            try
            {
                // 2. 親画面を一時停止（Pause演出）
                if (callerScreen != null)
                {
                    await Procedures.Screen.ExecuteExitWithTransitionAsync(
                        callerScreen,
                        ExitType.OnPause,
                        GetType(),
                        null,
                        true,
                        cancellationToken
                    );
                }

                // 3. 自分（ヘッドレス画面）をスタックに追加して準備
                list.Add(this);
                await ((IGameScreenInternal<TArgs>)this).InitializeAsync(args, cancellationToken);
                await Procedures.Screen.PrepareAsync(this, cancellationToken);

                // 4. オープン処理の実行（ビューレスのため演出はスキップされる）
                await Procedures.Screen.ExecuteEnterWithTransitionAsync(
                    (IGameScreenInternal)this,
                    EnterType.OnOpen,
                    callerScreen?.GetType(),
                    null,
                    false,
                    cancellationToken
                );

                // 5. ユーザーの定義したフローシナリオを呼び出し
                var result = await RunAsync(callerContext, args, cancellationToken);
                return result;
            }
            catch (Exception)
            {
                // 6. ロールバック保護：例外・キャンセル発生時、開始後に積み上げられた画面を逆順で強制物理破棄してクリーンアップ
                while (list.Count > startIndex)
                {
                    var screen = list[^1];
                    try
                    {
                        await Procedures.Screen.TeardownAsync(screen, CancellationToken.None);
                    }
                    catch (Exception teardownEx)
                    {
                        Debug.LogException(
                            new Exception(
                                $"[Lilja.ScreenManagement] GameFlow ロールバック中の画面破棄において例外が発生しました。画面型: '{screen.GetType().Name}'",
                                teardownEx
                            )
                        );
                    }
                    finally
                    {
                        list.RemoveAt(list.Count - 1);
                    }
                }

                throw;
            }
            finally
            {
                // 8. 正常・異常に関わらず、フロー画面自身をスタックから破棄して終了
                if (list.Contains(this))
                {
                    var nextType = callerScreen?.GetType();
                    try
                    {
                        await Procedures.Group.DropSubtreeAsync(
                            callerContext,
                            this,
                            nextType,
                            null,
                            CancellationToken.None
                        );
                    }
                    catch (Exception dropEx)
                    {
                        Debug.LogException(
                            new Exception(
                                $"[Lilja.ScreenManagement] GameFlow 正常終了に伴うサブツリー破棄において例外が発生しました。",
                                dropEx
                            )
                        );
                    }
                }

                // 9. 親画面が存在し、かつロールバックで既に復元されていない場合は正常復旧
                // ロールバック時・正常時を問わず、必ず CancellationToken.None を用いて安全かつ確実に復旧させる
                if (callerScreen != null && list.Count > 0 && list[^1] == callerScreen)
                {
                    try
                    {
                        await Procedures.Screen.ExecuteEnterWithTransitionAsync(
                            callerScreen,
                            EnterType.OnResume,
                            GetType(),
                            null,
                            false,
                            CancellationToken.None
                        );
                    }
                    catch (Exception resumeEx)
                    {
                        Debug.LogException(
                            new Exception(
                                $"[Lilja.ScreenManagement] GameFlow 終了後の親画面復帰において例外が発生しました。親画面型: '{callerScreen.GetType().Name}'",
                                resumeEx
                            )
                        );
                    }
                }
            }
        }

        /// <summary>
        /// ユーザー定義の画面遷移シーケンス（フロー）の実行ロジックを記述します。
        /// </summary>
        /// <param name="context">遷移スタックを操作する共通コンテキスト</param>
        /// <param name="args">開始引数</param>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>フローが完了した時の返却タスク</returns>
        protected abstract UniTask<TResult> RunAsync(
            GameScreenContext context,
            TArgs args,
            CancellationToken cancellationToken
        );

        /// <inheritdoc />
        protected override void OnDispose()
        {
            base.OnDispose();
        }

        /// <inheritdoc />
        protected sealed override UniTask InitializeAsync(
            TArgs args,
            CancellationToken cancellationToken
        ) => UniTask.CompletedTask;

        /// <inheritdoc />
        protected sealed override UniTask EnterAsync(
            EnterContext context,
            CancellationToken cancellationToken
        ) => UniTask.CompletedTask;

        /// <inheritdoc />
        protected sealed override UniTask ExitAsync(
            ExitContext context,
            CancellationToken cancellationToken
        ) => UniTask.CompletedTask;
    }

    /// <summary>
    /// GameFlowの作成・構築を行うヘルパークラス。
    /// </summary>
    public static class GameFlow
    {
        /// <summary>
        /// 継承せずにラムダ式からGameFlowを作成します。
        /// </summary>
        public static GameFlow<TArgs, TResult> Create<TArgs, TResult>(
            Func<GameScreenContext, TArgs, CancellationToken, UniTask<TResult>> runAsync
        )
        {
            return new LambdaGameFlow<TArgs, TResult>(runAsync);
        }

        /// <summary>
        /// GameFlowBuilderを初期化します。
        /// </summary>
        public static GameFlowBuilder<TStart, TStart> CreateBuilder<TStart>()
        {
            return new GameFlowBuilder<TStart, TStart>((ctx, arg, ct) => UniTask.FromResult(arg));
        }

        /// <summary>
        /// 最初のステップを指定してGameFlowBuilderを初期化します。
        /// </summary>
        public static GameFlowBuilder<TStart, TNext> CreateBuilder<TStart, TNext>(
            Func<GameScreenContext, TStart, CancellationToken, UniTask<TNext>> firstStep
        )
        {
            return new GameFlowBuilder<TStart, TNext>(firstStep);
        }
    }

    internal sealed class LambdaGameFlow<TArgs, TResult> : GameFlow<TArgs, TResult>
    {
        private readonly Func<GameScreenContext, TArgs, CancellationToken, UniTask<TResult>> _runAsync;

        public LambdaGameFlow(Func<GameScreenContext, TArgs, CancellationToken, UniTask<TResult>> runAsync)
        {
            _runAsync = runAsync ?? throw new ArgumentNullException(nameof(runAsync));
        }

        protected override UniTask<TResult> RunAsync(
            GameScreenContext context,
            TArgs args,
            CancellationToken cancellationToken
        )
        {
            return _runAsync(context, args, cancellationToken);
        }
    }
}
