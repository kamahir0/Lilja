using System.Collections.Generic;
using UnityEngine;

namespace Lilja.ScreenManagement
{

    internal static class CanvasOrderUtility
    {
        private const int LayerOrderRange = 1000;
        private static readonly List<Canvas> _canvasBuffer = new(16);

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

        internal static void CreateBehindRaycastBlocker(GameObject[] rootObjects)
        {
            if (rootObjects == null || rootObjects.Length == 0)
            {
                return;
            }

            _canvasBuffer.Clear();

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

            var blocker = new GameObject("RaycastBlocker", typeof(RectTransform));
            blocker.transform.SetParent(targetCanvas.transform, false);
            blocker.transform.SetAsFirstSibling();

            var rect = blocker.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;

            var graphic = blocker.AddComponent<InvisibleGraphic>();
            graphic.raycastTarget = true;
        }
    }
}
