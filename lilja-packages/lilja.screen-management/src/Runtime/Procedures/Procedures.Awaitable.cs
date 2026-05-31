using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.ScreenManagement
{
    public static partial class Procedures
    {
        /// <summary>
        /// AwaitableGameScreen（結果を待てる画面）に関する手続きモジュール。
        /// </summary>
        public static class Awaitable
        {
            /// <summary>
            /// AwaitableGameScreen を呼び出し元の階層に接続し、表示・演出を行い、結果の確定と破棄完了まで非同期待機します。
            /// </summary>
            public static async UniTask<TResult> CallAsync<TArgs, TResult>(
                GameScreenContext callerContext,
                AwaitableGameScreen<TArgs, TResult> calleeScreen,
                TArgs args,
                CancellationToken cancellationToken
            )
            {
                if (calleeScreen == null)
                {
                    throw new ArgumentNullException(nameof(calleeScreen));
                }

                calleeScreen.Context = callerContext;

                IGameScreenInternal callerScreen = null;
                var list = callerContext.ActiveScreensInternal;
                if (list.Count > 0)
                {
                    callerScreen = list[^1];
                }

                calleeScreen.Layer = callerScreen != null ? callerScreen.Layer + 1 : 0;

                var result = default(TResult);
                ExceptionDispatchInfo signalException = null;

                try
                {
                    if (callerScreen != null)
                    {
                        await callerScreen.PauseAsync(
                            calleeScreen.GetType(),
                            calleeScreen.OverrideTransition,
                            cancellationToken
                        );
                    }

                    await PrepareAndOpenAsync(
                        callerContext,
                        calleeScreen,
                        args,
                        callerScreen?.GetType(),
                        calleeScreen.OverrideTransition,
                        cancellationToken
                    );

                    var completionSource = calleeScreen.CompletionSource;
                    using (
                        cancellationToken.RegisterWithoutCaptureExecutionContext(() =>
                            completionSource.TrySetCanceled()
                        )
                    )
                    {
                        result = await completionSource.Task;
                    }
                }
                catch (Exception exception)
                {
                    signalException = ExceptionDispatchInfo.Capture(exception);
                }

                try
                {
                    // 一括破棄（一括クローズ）の最中でない場合のみ、個別でのクリーンアップや親画面の再開を実行する
                    if (!callerContext.IsClosing && !calleeScreen.IsClosing)
                    {
                        if (list.Contains(calleeScreen))
                        {
                            var nextType = callerScreen?.GetType();
                            await Group.DropSubtreeAsync(
                                callerContext,
                                calleeScreen,
                                nextType,
                                calleeScreen.OverrideTransition,
                                CancellationToken.None
                            );
                        }

                        if (callerScreen != null)
                        {
                            await callerScreen.ResumeAsync(
                                calleeScreen.GetType(),
                                calleeScreen.OverrideTransition,
                                CancellationToken.None
                            );
                        }
                    }
                }
                catch (Exception teardownException) when (signalException != null)
                {
                    throw new AggregateException(
                        signalException.SourceException,
                        teardownException
                    );
                }

                signalException?.Throw();
                return result;
            }
        }
    }
}
