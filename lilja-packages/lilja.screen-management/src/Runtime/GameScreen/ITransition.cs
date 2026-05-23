using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// 画面遷移時のトランジション（フェード演出など）を制御するインターフェース。
    /// </summary>
    public interface ITransition
    {
        /// <summary>
        /// 画面が覆い隠される演出を行います。
        /// </summary>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>非同期タスク</returns>
        UniTask OutAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 画面の覆いが解除される演出を行います。
        /// </summary>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>非同期タスク</returns>
        UniTask InAsync(CancellationToken cancellationToken);
    }
}
