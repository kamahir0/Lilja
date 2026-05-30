using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// 起動された画面グループの制御や待機状態を細かく管理するためのハンドルオブジェクト。
    /// </summary>
    public sealed class GameScreenGroupHandle
    {
        private readonly UniTaskCompletionSource _initialScreenEnterSource = new();
        private readonly UniTaskCompletionSource _groupLifetimeSource = new();

        internal GameScreenGroupHandle()
        {
        }

        /// <summary>
        /// 最初の画面のロードと入場演出（EnterAsync）が完了するまで非同期待機します。
        /// </summary>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>非同期タスク</returns>
        public UniTask WaitForInitialScreenEnterAsync(CancellationToken cancellationToken = default)
        {
            return _initialScreenEnterSource.Task.AttachExternalCancellation(cancellationToken);
        }

        /// <summary>
        /// 画面グループ全体の寿命（グループが終了するまで）を非同期待機します。
        /// </summary>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>非同期タスク</returns>
        public UniTask WaitForGroupLifetimeAsync(CancellationToken cancellationToken = default)
        {
            return _groupLifetimeSource.Task.AttachExternalCancellation(cancellationToken);
        }

        /// <summary>
        /// C# の await 構文を直接このハンドルに対して使用した際、デフォルトでグループの寿命を待機するようにします。
        /// これにより、従来の CallAsync を直接 await するコードとの完全な互換性が維持されます。
        /// </summary>
        /// <returns>Awaiter</returns>
        public UniTask.Awaiter GetAwaiter()
        {
            return _groupLifetimeSource.Task.GetAwaiter();
        }

        internal void SignalInitialScreenEntered()
        {
            _initialScreenEnterSource.TrySetResult();
        }

        internal void SignalGroupLifetimeCompleted()
        {
            // 最初の画面の表示がまだシグナルされていなければ安全のために完了させておく
            _initialScreenEnterSource.TrySetResult();
            _groupLifetimeSource.TrySetResult();
        }

        internal void SignalGroupLifetimeFailed(Exception exception)
        {
            _initialScreenEnterSource.TrySetException(exception);
            _groupLifetimeSource.TrySetException(exception);
        }

        internal void SignalGroupLifetimeCanceled()
        {
            _initialScreenEnterSource.TrySetCanceled();
            _groupLifetimeSource.TrySetCanceled();
        }
    }
}
