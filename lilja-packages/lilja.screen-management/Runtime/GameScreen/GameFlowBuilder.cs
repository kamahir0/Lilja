using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// メソッドチェーンによって複数の画面遷移や変換ロジックを連結し、
    /// 最終的に1つのGameFlowとして実行可能にするためのビルダー。
    /// </summary>
    /// <typeparam name="TStart">開始引数の型</typeparam>
    /// <typeparam name="TCurrent">現在のフロー状態の型</typeparam>
    public sealed class GameFlowBuilder<TStart, TCurrent> : GameFlow<TStart, TCurrent>
    {
        private readonly Func<
            GameScreenContext,
            TStart,
            CancellationToken,
            UniTask<TCurrent>
        > _runAsync;

        internal GameFlowBuilder(
            Func<GameScreenContext, TStart, CancellationToken, UniTask<TCurrent>> runAsync
        )
        {
            _runAsync = runAsync ?? throw new ArgumentNullException(nameof(runAsync));
        }

        protected override UniTask<TCurrent> RunAsync(
            GameScreenContext context,
            TStart args,
            CancellationToken cancellationToken
        )
        {
            return _runAsync(context, args, cancellationToken);
        }

        /// <summary>
        /// 画面コンテキストおよびCancellationTokenを受け取り、非同期で次の型に変換・実行するステップを追加します。
        /// </summary>
        public GameFlowBuilder<TStart, TNext> Then<TNext>(
            Func<GameScreenContext, TCurrent, CancellationToken, UniTask<TNext>> next
        )
        {
            if (next == null)
                throw new ArgumentNullException(nameof(next));
            return new GameFlowBuilder<TStart, TNext>(
                async (context, startArgs, ct) =>
                {
                    var current = await _runAsync(context, startArgs, ct);
                    return await next(context, current, ct);
                }
            );
        }

        /// <summary>
        /// 非同期で次の型に変換・実行するステップを追加します。
        /// </summary>
        public GameFlowBuilder<TStart, TNext> Then<TNext>(Func<TCurrent, UniTask<TNext>> next)
        {
            if (next == null)
                throw new ArgumentNullException(nameof(next));
            return new GameFlowBuilder<TStart, TNext>(
                async (context, startArgs, ct) =>
                {
                    var current = await _runAsync(context, startArgs, ct);
                    return await next(current);
                }
            );
        }

        /// <summary>
        /// 同期的に値を変換・実行するステップを追加します。
        /// </summary>
        public GameFlowBuilder<TStart, TNext> Select<TNext>(Func<TCurrent, TNext> transform)
        {
            if (transform == null)
                throw new ArgumentNullException(nameof(transform));
            return new GameFlowBuilder<TStart, TNext>(
                async (context, startArgs, ct) =>
                {
                    var current = await _runAsync(context, startArgs, ct);
                    return transform(current);
                }
            );
        }

        /// <summary>
        /// 現在の値を開始引数として次の GameFlowBuilder を実行し、2つの結果から最終値を射影します。
        /// </summary>
        public GameFlowBuilder<TStart, TResult> SelectMany<TNext, TResult>(
            Func<TCurrent, GameFlowBuilder<TCurrent, TNext>> bind,
            Func<TCurrent, TNext, TResult> project
        )
        {
            if (bind == null)
                throw new ArgumentNullException(nameof(bind));
            if (project == null)
                throw new ArgumentNullException(nameof(project));

            return new GameFlowBuilder<TStart, TResult>(
                async (context, startArgs, ct) =>
                {
                    var current = await _runAsync(context, startArgs, ct);
                    var nextFlow =
                        bind(current)
                        ?? throw new InvalidOperationException(
                            "SelectMany bind returned null GameFlowBuilder."
                        );
                    var next = await nextFlow._runAsync(context, current, ct);
                    return project(current, next);
                }
            );
        }

        /// <summary>
        /// 画面コンテキストおよびCancellationTokenを受け取り、同期的に値を変換・実行するステップを追加します。
        /// </summary>
        public GameFlowBuilder<TStart, TNext> Then<TNext>(
            Func<GameScreenContext, TCurrent, CancellationToken, TNext> transform
        )
        {
            if (transform == null)
                throw new ArgumentNullException(nameof(transform));
            return new GameFlowBuilder<TStart, TNext>(
                async (context, startArgs, ct) =>
                {
                    var current = await _runAsync(context, startArgs, ct);
                    return transform(context, current, ct);
                }
            );
        }
    }
}
