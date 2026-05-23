using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// トランジション の I/F
    /// </summary>
    public interface ITransition
    {
        /// <summary>
        /// トランジションで画面を表示します
        /// </summary>
        UniTask InAsync(CancellationToken cancellationToken);

        /// <summary>
        /// トランジションで画面を非表示します
        /// </summary>
        UniTask OutAsync(CancellationToken cancellationToken);
    }
}
