using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace Lilja.ScreenManagement
{
    public static partial class Procedures
    {
        /// <summary>
        /// GameScreenGroup（画面グループ）に関する手続きモジュール。
        /// </summary>
        internal static class Group
        {
            /// <summary>
            /// 画面グループを起動し、初期画面を表示してグループの寿命が終了するまで非同期待機します。
            /// </summary>
            public static async UniTask CallAsync<TArgs>(
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
                    var nextScreenType = calleeGroup.Registry.GetScreenType(initialScreenKey);
                    if (callerConnector.Owner is IGameScreenInternal callerScreen)
                    {
                        await callerScreen.PauseAsync(nextScreenType, cancellationToken);
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
                    Type previousScreenType = null;
                    if (
                        calleeConnector.Child != null
                        && calleeConnector.Child.Owner is IGameScreenInternal activeScreen
                    )
                    {
                        previousScreenType = activeScreen.GetType();
                    }

                    if (callerConnector.Owner != null)
                    {
                        if (callerConnector.Child == calleeConnector)
                        {
                            await Connector.DropSubtreeAsync(
                                calleeConnector,
                                callerConnector.Owner.GetType(),
                                CancellationToken.None
                            );
                        }
                    }
                    else
                    {
                        await Connector.DropSubtreeAsync(
                            calleeConnector,
                            null,
                            CancellationToken.None
                        );
                    }

                    if (callerConnector.Owner is IGameScreenInternal callerScreen)
                    {
                        await callerScreen.ResumeAsync(previousScreenType, cancellationToken);
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
            public static async UniTask SwitchAsync<TArgs>(
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
                        throw new InvalidOperationException(
                            "[Lilja.ScreenManagement] この画面グループはクローズ処理中です。"
                        );
                    }

                    var needsTempScene = SceneManager.sceneCount <= 1;
                    using var tempSceneScope = needsTempScene
                        ? TempSceneUtility.CreateTempSceneScope()
                        : default;

                    var nextScreenType = group.Registry.GetScreenType(key);

                    Type previousScreenType = null;
                    if (
                        groupConnector.Child != null
                        && groupConnector.Child.Owner is IGameScreenInternal oldScreen
                    )
                    {
                        previousScreenType = oldScreen.GetType();
                    }

                    if (groupConnector.Child != null)
                    {
                        await Connector.DropSubtreeAsync(
                            groupConnector.Child,
                            nextScreenType,
                            cancellationToken
                        );
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
                            previousScreenType,
                            cancellationToken
                        );
                    }
                    catch
                    {
                        if (groupConnector.Child == nextConnector)
                        {
                            await Connector.DropSubtreeAsync(
                                nextConnector,
                                null,
                                CancellationToken.None
                            );
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
