using UnityEngine;
using UnityEngine.UI;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// 頂点（メッシュ）を一切生成せず、描画処理の負荷（Overdraw等）を発生させない、軽量な透明クリック遮断用 UI グラフィックコンポーネント。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class InvisibleGraphic : Graphic
    {
        /// <summary>
        /// コンストラクタで、初期状態として無駄な頂点レンダリング（マテリアル描画）を防ぐ設定を補完します。
        /// </summary>
        public InvisibleGraphic()
        {
            useLegacyMeshGeneration = false;
        }

        /// <summary>
        /// メッシュ生成のバッファをクリアし、描画ジオメトリの生成処理を完全にスキップさせます。
        /// </summary>
        /// <param name="vh">メッシュ生成ヘルパー</param>
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
        }
    }
}
