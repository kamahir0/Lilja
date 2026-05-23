using System.Linq;
using UnityEngine;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// Canvas の描画順の自動調整を提供するユーティリティ
    /// </summary>
    internal static class CanvasOrderUtility
    {
        // 各レイヤーのsortingOrder幅
        // World: 0〜999, Overlay1: 1000〜1999, Overlay2: 2000〜2999 ...
        private const int LayerOrderRange = 1000;

        /// <summary>
        /// 指定したGameObject配列内のCanvasにsortingOrderを適用します
        /// </summary>
        /// <param name="rootObjects">対象のルートGameObject配列</param>
        /// <param name="layerIndex">レイヤーインデックス (World=0, Overlay1=1, ...)</param>
        public static void ApplyOrder(GameObject[] rootObjects, int layerIndex)
        {
            if (rootObjects == null || rootObjects.Length == 0) return;

            // 全てのCanvasを取得（子階層も含む）
            var canvases = rootObjects
                .SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
                .ToArray();

            if (canvases.Length == 0) return;

            // ベースとなるsortingOrder
            var baseOrder = layerIndex * LayerOrderRange;

            // 既存の相対順序を維持しながらsortingOrderを設定
            // まず現在のsortingOrderでソート
            var sortedCanvases = canvases
                .Select((canvas, index) => new { Canvas = canvas, OriginalOrder = canvas.sortingOrder, Index = index })
                .OrderBy(x => x.OriginalOrder)
                .ThenBy(x => x.Index)
                .ToArray();

            for (int i = 0; i < sortedCanvases.Length; i++)
            {
                var canvas = sortedCanvases[i].Canvas;

                // WorldSpace は対象外（警告ログ出力）
                if (canvas.renderMode == RenderMode.WorldSpace)
                {
                    Debug.LogWarning($"[CanvasOrderService] WorldSpace Canvas はサポート対象外です: {canvas.gameObject.name}");
                    continue;
                }

                // sortingOrderを設定
                canvas.sortingOrder = baseOrder + i;
            }
        }
    }
}
