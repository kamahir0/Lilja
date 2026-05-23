using System.Collections.Generic;
using UnityEngine;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// キャンバスの描画順や背面入力遮断オブジェクトの生成など、ビュー（肉体）の物理的・視覚的なレイアウト調整を担当するユーティリティ。
    /// </summary>
    internal static class CanvasOrderUtility
    {
        private const int LayerOrderRange = 1000;
        private static readonly List<Canvas> _canvasBuffer = new(16);

        /// <summary>
        /// 生成されたビューオブジェクトにレイヤーインデックスに基づく描画順を適用します。
        /// </summary>
        internal static void ApplyCanvasOrder(GameObject[] rootObjects, int layerIndex)
        {
            if (rootObjects == null || rootObjects.Length == 0)
            {
                return;
            }

            var canvases = new List<Canvas>();
            foreach (var root in rootObjects)
            {
                if (root == null)
                {
                    continue;
                }
                root.GetComponentsInChildren(true, canvases);
            }

            if (canvases.Count == 0)
            {
                return;
            }

            var baseOrder = layerIndex * LayerOrderRange;

            // 既存の相対順序を維持しながらソートして割り当て
            canvases.Sort((a, b) => a.sortingOrder.CompareTo(b.sortingOrder));

            for (var i = 0; i < canvases.Count; i++)
            {
                var canvas = canvases[i];
                if (canvas.renderMode == RenderMode.WorldSpace)
                {
                    Debug.LogWarning(
                        $"[CanvasLayoutUtility] WorldSpace Canvas はソート順制御のサポート対象外です: {canvas.gameObject.name}"
                    );
                    continue;
                }
                canvas.sortingOrder = baseOrder + i;
            }
        }

        /// <summary>
        /// 背面に重なった画面へのクリック入力を遮断するブロッカーを生成して挿入します。
        /// </summary>
        internal static void CreateBehindRaycastBlocker(GameObject[] rootObjects)
        {
            if (rootObjects == null || rootObjects.Length == 0)
            {
                return;
            }

            _canvasBuffer.Clear();

            // 全てのCanvasを取得
            foreach (var root in rootObjects)
            {
                if (root == null)
                {
                    continue;
                }
                root.GetComponentsInChildren(true, _canvasBuffer);
            }

            if (_canvasBuffer.Count == 0)
            {
                return;
            }

            // 最奥（SortingOrderが最小）のCanvasを探す
            Canvas targetCanvas = null;
            var minOrder = int.MaxValue;

            foreach (var canvas in _canvasBuffer)
            {
                if (canvas.renderMode == RenderMode.WorldSpace)
                {
                    continue;
                }

                if (canvas.sortingOrder < minOrder)
                {
                    minOrder = canvas.sortingOrder;
                    targetCanvas = canvas;
                }
            }

            _canvasBuffer.Clear();

            if (targetCanvas == null)
            {
                return;
            }

            // ブロッカーの生成と配置
            var blocker = new GameObject("RaycastBlocker", typeof(RectTransform));
            blocker.transform.SetParent(targetCanvas.transform, false);
            blocker.transform.SetAsFirstSibling();

            var rect = blocker.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;

            // 描画負荷のない InvisibleGraphic (MonoBehaviour) を追加して入力を遮断
            var graphic = blocker.AddComponent<InvisibleGraphic>();
            graphic.raycastTarget = true;
        }
    }
}
