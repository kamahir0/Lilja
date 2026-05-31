using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// 呼び出して結果を待機可能な画面の基底クラス。
    /// </summary>
    /// <typeparam name="TArgs">初期化引数の型</typeparam>
    /// <typeparam name="TResult">返却する結果の型</typeparam>
    public abstract class AwaitableGameScreen<TArgs, TResult> : GameScreenBase<TArgs>
    {

        /// <summary>
        /// この画面がコールされた際の一時差し替えトランジション。
        /// デフォルトは ITransition.None。
        /// </summary>
        public virtual ITransition OverrideTransition => ITransition.None;

        /// <summary>
        /// 呼び出し元へ返す結果を確定して画面を閉じます。
        /// </summary>
        /// <param name="result">返却する結果オブジェクト</param>
        protected void Complete(TResult result)
        {
            _completionSource?.TrySetResult(result);
        }

        /// <summary>
        /// 呼び出し元へ例外エラーを返して画面を閉じます。
        /// </summary>
        /// <param name="exception">スローする例外</param>
        protected void Fail(Exception exception)
        {
            _completionSource?.TrySetException(exception);
        }

        /// <summary>
        /// 画面遷移をキャンセルして閉じます。
        /// </summary>
        protected void Cancel()
        {
            _completionSource?.TrySetCanceled();
        }


        private UniTaskCompletionSource<TResult> _completionSource = new();

        /// <summary>
        /// ランタイムがこの画面の結果待機を行うための内部プロパティ。
        /// </summary>
        internal UniTaskCompletionSource<TResult> CompletionSource => _completionSource;

        /// <summary>
        /// 指定された呼び出し元のコンテキストの下でこの画面をロード・表示し、結果が確定するまで非同期で待機します。
        /// </summary>
        /// <param name="callerContext">呼び出し側の画面コンテキスト</param>
        /// <param name="args">画面に渡す引数</param>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>画面の結果を返す非同期タスク</returns>
        public UniTask<TResult> CallAsync(
            GameScreenContext callerContext,
            TArgs args,
            CancellationToken cancellationToken = default
        )
        {
            return callerContext == null
                ? throw new ArgumentNullException(nameof(callerContext))
                : Procedures.Awaitable.CallAsync(callerContext, this, args, cancellationToken);
        }

        /// <inheritdoc />
        protected override void OnDispose()
        {
            _completionSource?.TrySetCanceled();
            _completionSource = null;
        }
    }
}
