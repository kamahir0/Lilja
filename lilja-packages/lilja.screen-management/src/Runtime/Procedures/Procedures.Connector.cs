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
        /// ランタイムツリー上の双方向ノード（コネクタ）の接続・切断、および再帰破棄の手続きモジュール。
        /// </summary>
        internal static class Connector
        {
            public static void Connect(GameScreenConnector parent, GameScreenConnector child)
            {
                if (parent == null)
                    throw new ArgumentNullException(nameof(parent));
                if (child == null)
                    throw new ArgumentNullException(nameof(child));
                if (parent.Owner == null)
                    throw new InvalidOperationException(
                        "[Lilja.ScreenManagement] 親コネクタが実行されていません。"
                    );
                if (parent.IsClosing)
                    throw new InvalidOperationException(
                        "[Lilja.ScreenManagement] 親コネクタはクローズ処理中です。"
                    );
                if (parent.Child != null)
                    throw new InvalidOperationException(
                        "[Lilja.ScreenManagement] 親コネクタには既に子コネクタが接続されています。"
                    );
                if (ReferenceEquals(parent, child))
                    throw new InvalidOperationException(
                        "[Lilja.ScreenManagement] コネクタを自分自身に接続することはできません。"
                    );
                if (child.Parent != null)
                    throw new InvalidOperationException(
                        "[Lilja.ScreenManagement] 子コネクタは既に接続されています。"
                    );
                if (child.IsClosing)
                    throw new InvalidOperationException(
                        "[Lilja.ScreenManagement] 子コネクタはクローズ処理中です。"
                    );
                if (child.Owner == null)
                    throw new InvalidOperationException(
                        "[Lilja.ScreenManagement] 子コネクタが実行されていません。"
                    );

                child.Parent = parent;
                child.IsClosing = false;
                parent.Child = child;
            }

            public static void Disconnect(GameScreenConnector parent, GameScreenConnector child)
            {
                if (child == null)
                    throw new ArgumentNullException(nameof(child));

                if (parent == null)
                {
                    child.Parent = null;
                    return;
                }

                if (parent.Child != child)
                {
                    throw new InvalidOperationException(
                        "[Lilja.ScreenManagement] 子コネクタが親コネクタに接続されていません。"
                    );
                }

                parent.Child = null;
                child.Parent = null;
            }

            /// <summary>
            /// 指定されたコネクタ以下のサブツリーを安全に再帰破棄します。
            /// </summary>
            public static async UniTask DropSubtreeAsync(
                GameScreenConnector root,
                Type nextScreenType,
                CancellationToken cancellationToken
            )
            {
                if (root == null)
                {
                    throw new ArgumentNullException(nameof(root));
                }

                var needsTempScene = SceneManager.sceneCount <= 1;
                using var tempSceneScope = needsTempScene
                    ? TempSceneUtility.CreateTempSceneScope()
                    : default;

                var front = MarkClosingAndGetFront(root);

                ExceptionDispatchInfo closeException = null;
                IGameScreenInternal frontScreen = null;

                try
                {
                    frontScreen = FindFrontScreen(root, front);
                    if (frontScreen != null)
                    {
                        await frontScreen.CloseAsync(nextScreenType, cancellationToken);
                    }
                }
                catch (Exception exception)
                {
                    closeException = ExceptionDispatchInfo.Capture(exception);
                }

                var parentToRestore = root.Parent;
                var previousScreenType = frontScreen?.GetType();

                try
                {
                    await CleanupDropChainAsync(root, front, cancellationToken);
                }
                catch (Exception cleanupException) when (closeException != null)
                {
                    throw new AggregateException(closeException.SourceException, cleanupException);
                }

                closeException?.Throw();

                if (parentToRestore != null)
                {
                    await Screen.RestoreAncestorsAsync(
                        parentToRestore,
                        previousScreenType,
                        cancellationToken
                    );
                }
            }

            private static GameScreenConnector MarkClosingAndGetFront(GameScreenConnector root)
            {
                var front = root;
                for (var connector = root; connector != null; connector = connector.Child)
                {
                    connector.IsClosing = true;
                    front = connector;
                }
                return front;
            }

            private static IGameScreenInternal FindFrontScreen(
                GameScreenConnector root,
                GameScreenConnector front
            )
            {
                for (var connector = front; connector != null; connector = connector.Parent)
                {
                    if (connector.Owner is IGameScreenInternal screen)
                    {
                        return screen;
                    }

                    if (connector == root)
                    {
                        break;
                    }
                }
                return null;
            }

            private static async UniTask CleanupDropChainAsync(
                GameScreenConnector root,
                GameScreenConnector front,
                CancellationToken cancellationToken
            )
            {
                Disconnect(root.Parent, root);

                for (var connector = front; connector != null; )
                {
                    var parent = connector.Parent;

                    await CleanupOwnerAsync(connector, cancellationToken);
                    ClearConnector(connector);

                    if (connector == root)
                    {
                        break;
                    }

                    connector = parent;
                }
            }

            private static async UniTask CleanupOwnerAsync(
                GameScreenConnector connector,
                CancellationToken cancellationToken
            )
            {
                switch (connector.Owner)
                {
                    case GameScreenGroup group:
                        group.CompletionSource.TrySetCanceled();
                        break;
                    case IGameScreenInternal screen:
                        await Screen.TeardownAsync(screen, cancellationToken);
                        break;
                }
            }

            private static void ClearConnector(GameScreenConnector connector)
            {
                connector.Parent = null;
                connector.Child = null;
                connector.Owner = null;
                connector.IsClosing = false;
            }
        }
    }
}
