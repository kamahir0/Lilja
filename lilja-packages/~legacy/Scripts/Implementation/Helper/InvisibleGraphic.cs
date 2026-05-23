using UnityEngine;
using UnityEngine.UI;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// 透明な Graphic コンポーネント (Raycast Target 用)
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class InvisibleGraphic : Graphic
    {
        /// <inheritdoc />
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
        }

        /// <inheritdoc />
        public override bool Raycast(Vector2 sp, Camera eventCamera)
        {
            return true;
        }
    }
}
