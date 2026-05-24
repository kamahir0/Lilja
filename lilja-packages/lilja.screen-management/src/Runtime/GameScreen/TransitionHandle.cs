using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// 画面遷移時にトランジション演出を実行・await するためのI/F。
    /// </summary>
    public interface ITransitionHandle
    {
        /// <summary>
        /// トランジションが既に再生（実行）されたかどうかを示す値を取得します。
        /// </summary>
        bool IsPlayed { get; }

        /// <summary>
        /// 設定されているトランジション演出を実行し、完了まで非同期待機します。
        /// </summary>
        /// <remarks>
        /// 画面の入場・退場ライフサイクル（EnterAsync/ExitAsync）内でこのメソッドを手動呼び出しする場合は、
        /// 演出の完了と同期させるため、<b>必ず await してください。</b>（Forget() による呼び出しは避けてください）
        /// </remarks>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>非同期タスク</returns>
        UniTask PlayAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    /// トランジション演出の実行と冪等性を管理するハンドルの具象クラス。
    /// </summary>
    internal sealed class TransitionHandle : ITransitionHandle
    {
        private readonly ITransition _transition;
        private readonly bool _isOut;
        private int _played;

        /// <inheritdoc />
        public bool IsPlayed => _played != 0;

        /// <summary>
        /// 新しい <see cref="TransitionHandle"/> インスタンスを初期化します。
        /// </summary>
        /// <param name="transition">トランジション演出の実体</param>
        /// <param name="isOut">退場（Out）演出かどうか</param>
        public TransitionHandle(ITransition transition, bool isOut)
        {
            _transition = transition;
            _isOut = isOut;
        }

        /// <inheritdoc />
        public async UniTask PlayAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.CompareExchange(ref _played, 1, 0) != 0)
            {
                return;
            }

            if (_transition == null)
            {
                return;
            }

            if (_isOut)
            {
                await _transition.OutAsync(cancellationToken);
            }
            else
            {
                await _transition.InAsync(cancellationToken);
            }
        }
    }
}
