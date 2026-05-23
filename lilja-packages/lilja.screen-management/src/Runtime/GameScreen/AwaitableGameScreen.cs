using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.ScreenManagement
{

    public abstract class AwaitableGameScreen<TArgs, TResult> : GameScreenBase<TArgs>
    {
        private UniTaskCompletionSource<TResult> _completionSource = new();

        internal UniTaskCompletionSource<TResult> CompletionSource => _completionSource;

        public UniTask<TResult> CallAsync(
            GameScreenContext callerContext,
            TArgs args,
            CancellationToken cancellationToken = default
        )
        {
            if (callerContext == null)
            {
                throw new ArgumentNullException(nameof(callerContext));
            }

            return Procedures.Awaitable.CallAsync(callerContext, this, args, cancellationToken);
        }

        protected void Complete(TResult result)
        {
            _completionSource?.TrySetResult(result);
        }

        protected void Fail(Exception exception)
        {
            _completionSource?.TrySetException(exception);
        }

        protected void Cancel()
        {
            _completionSource?.TrySetCanceled();
        }

        protected override void OnDispose()
        {
            _completionSource?.TrySetCanceled();
            _completionSource = null;
        }
    }
}
