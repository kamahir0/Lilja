using UnityEngine;
using UnityEngine.UI;

namespace Lilja.ScreenManagement
{

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class InvisibleGraphic : Graphic
    {

        public InvisibleGraphic()
        {
            useLegacyMeshGeneration = false;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
        }
    }
}
