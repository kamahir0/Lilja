using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// World の I/F
    /// </summary>
    public interface IWorld : IScreen
    {
        /// <summary>
        /// 引数を受け取って初期化します
        /// </summary>
        UniTask InitializeAsync(object args, CancellationToken cancellationToken);
    }
}
