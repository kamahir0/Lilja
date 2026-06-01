using UnityEngine;
using UnityEngine.UI;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// メッシュの生成や描画処理を一切行わず、物理的なレイキャスト衝突検知（入力遮断）のみを提供する不可視の UI グラフィックコンポーネント。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class InvisibleGraphic : Graphic
    {
        /// <inheritdoc />
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
        }

        /// <inheritdoc />
        protected override void Awake()
        {
            base.Awake();
            useLegacyMeshGeneration = false;
        }
    }
}
