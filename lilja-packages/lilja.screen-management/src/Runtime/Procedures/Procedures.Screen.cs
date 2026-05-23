using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.ScreenManagement
{
    internal static partial class Procedures
    {
        /// <summary>
        /// 単一の画面（Screen）に対する物理的なインフラ処理を司るモジュール。
        /// </summary>
        internal static class Screen
        {
            /// <summary>
            /// 画面アセットをロードし、依存注入、ソート順、入力遮断を適用して画面を物理的に使用可能な状態にします。
            /// </summary>
            internal static async UniTask PrepareAsync(
                IGameScreenInternal screen,
                CancellationToken cancellationToken
            )
            {
                var viewHandle = screen.GetViewHandle();
                if (viewHandle == null)
                {
                    throw new InvalidOperationException(
                        $"ViewHandle is null in screen '{screen.GetType().Name}'."
                    );
                }

                viewHandle.Initialize(screen.GetType());

                if (viewHandle.UnloadsAncestors)
                {
                    UnloadAncestors(screen.Context.Connector);
                }

                await viewHandle.LoadAsync(screen.Context, cancellationToken);

                var rootObjects = viewHandle.RootObjects;

                CanvasOrderUtility.ApplyCanvasOrder(rootObjects, screen.Context.Layer);

                ViewInjectUtility.Inject(screen, rootObjects);

                screen.OnViewLoaded();

                if (screen.Context.Layer > 0)
                {
                    CanvasOrderUtility.CreateBehindRaycastBlocker(rootObjects);
                }
            }

            /// <summary>
            /// ビューアセットをアンロードし、注入された参照フィールドを null でクリアした上で、画面を破棄します。
            /// </summary>
            internal static void Teardown(IGameScreenInternal screen)
            {
                try
                {
                    screen.OnViewUnloaded();
                    ViewInjectUtility.Nullify(screen);
                }
                finally
                {
                    try
                    {
                        screen.GetViewHandle()?.Unload();
                    }
                    finally
                    {
                        screen.Dispose();
                    }
                }
            }

            private static void UnloadAncestors(GameScreenConnector startConnector)
            {
                for (var parent = startConnector.Parent; parent != null; parent = parent.Parent)
                {
                    if (parent.Owner is IGameScreenInternal screen)
                    {
                        var handle = screen.GetViewHandle();
                        if (handle != null && handle.IsLoaded)
                        {
                            screen.OnViewUnloaded();

                            ViewInjectUtility.Nullify(screen);

                            handle.Unload();

                            handle.IsUnloadedTemporarily = true;
                        }
                    }
                }
            }

            /// <summary>
            /// 一時アンロードされていた先祖のビューを、並列ロードと直列インスタンス化のハイブリッドで安全かつ超高速に復元します。
            /// </summary>
            internal static async UniTask RestoreAncestorsAsync(
                GameScreenConnector startConnector,
                CancellationToken cancellationToken
            )
            {
                var pendingScreens = new List<IGameScreenInternal>();
                for (var parent = startConnector; parent != null; parent = parent.Parent)
                {
                    if (parent.Owner is IGameScreenInternal screen)
                    {
                        var handle = screen.GetViewHandle();
                        if (handle != null)
                        {
                            if (handle.IsUnloadedTemporarily)
                            {
                                pendingScreens.Add(screen);
                            }

                            if (handle.UnloadsAncestors)
                            {
                                break;
                            }
                        }
                    }
                }

                if (pendingScreens.Count == 0)
                {
                    return;
                }

                var preloadTasks = new List<UniTask>(pendingScreens.Count);
                foreach (var screen in pendingScreens)
                {
                    var handle = screen.GetViewHandle();
                    preloadTasks.Add(handle.PreloadAsync(screen.Context, cancellationToken));
                }
                await UniTask.WhenAll(preloadTasks);

                for (var i = pendingScreens.Count - 1; i >= 0; i--)
                {
                    var screen = pendingScreens[i];
                    var handle = screen.GetViewHandle();

                    await PrepareAsync(screen, cancellationToken);
                    handle.IsUnloadedTemporarily = false;
                }
            }
        }
    }
}
