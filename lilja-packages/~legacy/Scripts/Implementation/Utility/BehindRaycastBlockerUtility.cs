using System.Collections.Generic; // namespace短縮のため追加
using UnityEngine;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// 背後の Raycast ブロックを生成するユーティリティ
    /// </summary>
    internal static class BehindRaycastBlockerUtility
    {
        // キャパシティ指定でリストのリサイズコストを軽減（適当な初期値）
        // スレッドセーフを気にする場合はThreadStaticやプールを使用するが、
        // Unityメインスレッド前提ならstaticフィールドでバッファを使い回すのが最もGCが少ない
        private static readonly List<Canvas> CanvasBuffer = new List<Canvas>(16);

        /// <summary>
        /// 背景の入力ブロックを生成します
        /// </summary>
        public static void Create(GameObject[] rootObjects)
        {
            if (rootObjects == null || rootObjects.Length == 0) return;

            // バッファをクリアして再利用（GC Alloc回避）
            CanvasBuffer.Clear();

            // 1. 全てのCanvasを取得
            foreach (var root in rootObjects)
            {
                if (root == null) continue;
                // ノンアロケーション版のAPIを使用
                root.GetComponentsInChildren(true, CanvasBuffer);
            }

            if (CanvasBuffer.Count == 0) return;

            // 2. 最奥（SortingOrderが最小）のCanvasを探す
            Canvas targetCanvas = null;
            int minOrder = int.MaxValue;

            // SortingLayerについての考慮が必要な場合、ここでSortingLayerID等の比較も追加する
            foreach (var canvas in CanvasBuffer)
            {
                // WorldSpace除外、かつ 親Canvasを持たない(ルートに近い)Canvasを優先したい場合は
                // canvas.isRootCanvas などのチェックも検討の余地あり
                if (canvas.renderMode == RenderMode.WorldSpace) continue;

                if (canvas.sortingOrder < minOrder)
                {
                    minOrder = canvas.sortingOrder;
                    targetCanvas = canvas;
                }
            }

            // 参照を外す（staticリストがオブジェクトを掴み続けないようにするため）
            CanvasBuffer.Clear();

            if (targetCanvas == null) return;

            // 3. ブロッカーを生成して配置
            CreateBlocker(targetCanvas);
        }

        /// <summary>
        /// 実際の生成処理（メソッド分割で可読性向上）
        /// </summary>
        private static void CreateBlocker(Canvas targetCanvas)
        {
            var blocker = new GameObject("RaycastBlocker", typeof(RectTransform));

            // 親設定時に worldPositionStays: false にすると、自動的に親のローカル座標系にリセットされやすい
            blocker.transform.SetParent(targetCanvas.transform, false);
            blocker.transform.SetAsFirstSibling();

            var rect = blocker.GetComponent<RectTransform>();
            // Anchor/Offsetの一括設定（ストレッチ設定）
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero; // offsetMin/Max = zero と同義だがより直感的
            rect.anchoredPosition = Vector2.zero;

            // InvisibleGraphic は頂点を生成しない軽量クラスであることを想定
            // もし標準のImage(color.a = 0)を使うと描画負荷(Overdraw)がかかるため、InvisibleGraphicの使用はGood
            var graphic = blocker.AddComponent<InvisibleGraphic>();
            graphic.raycastTarget = true;
        }
    }
}