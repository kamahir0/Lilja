using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.ScreenManagement
{
    internal static partial class Procedures
    {
        /// <summary>
        /// AwaitableGameScreen（結果を待める画面）に関する手続きモジュール。
        /// </summary>
        internal static class Awaitable
        {
            /// <summary>
            /// AwaitableGameScreen を呼び出し元の階層に接続し、表示・演出を行い、結果の確定と破棄完了まで非同期待機します。
            /// </summary>
            internal static async UniTask<TResult> CallAsync<TArgs, TResult>(
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

                var callerConnector = callerContext.Connector;
                var calleeConnector = calleeScreen.Context.Connector;

                Connector.Connect(callerConnector, calleeConnector);

                calleeScreen.Context.Layer = callerContext.Layer + 1;
                calleeScreen.Context.Options = callerContext.Options;

                var result = default(TResult);
                ExceptionDispatchInfo signalException = null;

                try
                {
                    if (callerConnector.Owner is IGameScreenInternal callerScreen)
                    {
                        await callerScreen.PauseAsync(cancellationToken);
                    }

                    await Screen.PrepareAsync(calleeScreen, cancellationToken);

                    await ((IGameScreenInternal<TArgs>)calleeScreen).OpenAsync(
                        args,
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
                    if (callerConnector.Child == calleeConnector)
                    {
                        await Connector.DropSubtreeAsync(calleeConnector, CancellationToken.None);
                    }

                    if (callerConnector.Owner is IGameScreenInternal callerScreen)
                    {
                        await callerScreen.ResumeAsync(cancellationToken);
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
