using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// Overlay の I/F
    /// </summary>
    public interface IOverlay : IScreen
    {
        /// <summary> 重い Overlay であるかどうか（表示前に他のビューを破棄するか） </summary>
        bool IsHeavy { get; }

        /// <summary>
        /// レイヤーインデックスを設定します
        /// </summary>
        void SetLayerIndex(int index);
    }

    /// <summary>
    /// Overlay の I/F (Generic)
    /// </summary>
    public interface IOverlay<in TArgs, TResult> : IOverlay
    {
        /// <summary>
        /// 初期化します
        /// </summary>
        UniTask InitializeAsync(TArgs args, CancellationToken cancellationToken);

        /// <summary>
        /// 結果が返されるまで待機します
        /// </summary>
        UniTask<TResult> WaitForResultAsync(CancellationToken cancellationToken);
    }
}