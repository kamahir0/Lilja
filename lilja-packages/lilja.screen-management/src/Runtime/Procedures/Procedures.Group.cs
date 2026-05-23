using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace Lilja.ScreenManagement
{
    internal static partial class Procedures
    {
        /// <summary>
        /// GameScreenGroup（画面グループ）に関する手続きモジュール。
        /// </summary>
        internal static class Group
        {
            /// <summary>
            /// 画面グループを起動し、初期画面を表示してグループの寿命が終了するまで非同期待機します。
            /// </summary>
            internal static async UniTask CallAsync<TArgs>(
                GameScreenContext callerContext,
                GameScreenGroup calleeGroup,
                string initialScreenKey,
                TArgs initialScreenArgs,
                CancellationToken cancellationToken
            )
            {
                var callerConnector = callerContext.Connector;
                var calleeConnector = Connect(callerContext, calleeGroup);

                ExceptionDispatchInfo signalException = null;

                try
                {
                    if (callerConnector.Owner is IGameScreenInternal callerScreen)
                    {
                        await callerScreen.PauseAsync(cancellationToken);
                    }

                    await calleeGroup.SwitchAsync(
                        initialScreenKey,
                        initialScreenArgs,
                        cancellationToken
                    );

                    var completionSource = calleeGroup.CompletionSource;
                    using (
                        cancellationToken.RegisterWithoutCaptureExecutionContext(() =>
                            completionSource.TrySetCanceled()
                        )
                    )
                    {
                        await completionSource.Task;
                    }
                }
                catch (Exception exception)
                {
                    signalException = ExceptionDispatchInfo.Capture(exception);
                }

                try
                {
                    if (callerConnector.Owner != null)
                    {
                        if (callerConnector.Child == calleeConnector)
                        {
                            await Connector.DropSubtreeAsync(calleeConnector, CancellationToken.None);
                        }
                    }
                    else
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
            }

            /// <summary>
            /// 画面グループが排他所有している現在のアクティブ画面を破棄し、新しい画面へ切り替えます。
            /// </summary>
            internal static async UniTask SwitchAsync<TArgs>(
                GameScreenGroup group,
                string key,
                TArgs args,
                CancellationToken cancellationToken
            )
            {
                await group.Gate.WaitAsync(cancellationToken);

                try
                {
                    var groupConnector = group.Context.Connector;

                    if (groupConnector.IsClosing)
                    {
                        throw new InvalidOperationException("This group is closing.");
                    }

                    var needsTempScene = SceneManager.sceneCount <= 1;
                    using var tempSceneScope = needsTempScene
                        ? TempSceneUtility.CreateTempSceneScope()
                        : default;

                    if (groupConnector.Child != null)
                    {
                        await Connector.DropSubtreeAsync(groupConnector.Child, cancellationToken);
                    }

                    var nextScreenObj = group.Registry.Create(key);
                    var nextScreen = (GameScreen<TArgs>)nextScreenObj;
                    var nextConnector = nextScreen.Context.Connector;

                    Connector.Connect(groupConnector, nextConnector);
                    nextScreen.Context.Layer = group.Context.Layer;
                    nextScreen.Context.Options = group.Context.Options;
                    nextScreen.Group = group;

                    try
                    {
                        await Screen.PrepareAsync(nextScreen, cancellationToken);

                        await ((IGameScreenInternal<TArgs>)nextScreen).OpenAsync(
                            args,
                            cancellationToken
                        );
                    }
                    catch
                    {
                        if (groupConnector.Child == nextConnector)
                        {
                            await Connector.DropSubtreeAsync(nextConnector, CancellationToken.None);
                        }
                        throw;
                    }
                }
                finally
                {
                    group.Gate.Release();
                }
            }

            private static GameScreenConnector Connect(
                GameScreenContext callerContext,
                GameScreenGroup calleeGroup
            )
            {
                if (calleeGroup == null)
                {
                    throw new ArgumentNullException(nameof(calleeGroup));
                }

                var callerConnector = callerContext.Connector;
                var calleeConnector = calleeGroup.Context.Connector;

                if (callerConnector.Owner != null)
                {
                    Connector.Connect(callerConnector, calleeConnector);
                    calleeGroup.Context.Layer = callerContext.Layer + 1;
                }
                else
                {
                    calleeGroup.Context.Layer = callerContext.Layer;
                }

                calleeGroup.Context.Options = callerContext.Options;

                calleeGroup.ConfigureInternal();
                return calleeConnector;
            }
        }
    }
}
