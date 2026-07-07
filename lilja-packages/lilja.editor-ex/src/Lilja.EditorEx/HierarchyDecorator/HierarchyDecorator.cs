using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Lilja.HierarchyDecorator
{
    [InitializeOnLoad]
    public static class HierarchyDecorator
    {
        private static readonly List<IHierarchyItemDrawer> drawers = new();

        static HierarchyDecorator()
        {
            // Register drawers. The first added drawer will be placed closest to the right (next to the built-in Prefab arrow button).
            drawers.Add(new ActiveToggleDrawer());
            drawers.Add(new MissingScriptPingButtonDrawer());

#if UNITY_6000_0_4_OR_NEWER || UNITY_6000_4_OR_NEWER
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI += OnHierarchyWindowItemGUI;
#else
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyWindowItemGUI;
#endif
        }

#if UNITY_6000_0_4_OR_NEWER || UNITY_6000_4_OR_NEWER
        private static void OnHierarchyWindowItemGUI(EntityId entityId, Rect selectionRect)
        {
            var gameObject = EditorUtility.EntityIdToObject(entityId) as GameObject;
            if (gameObject == null) return;
            DrawHierarchyItem(gameObject, selectionRect);
        }
#else
        private static void OnHierarchyWindowItemGUI(int instanceID, Rect selectionRect)
        {
            var gameObject = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            if (gameObject == null) return;
            DrawHierarchyItem(gameObject, selectionRect);
        }
#endif

        private static void DrawHierarchyItem(GameObject gameObject, Rect selectionRect)
        {
            // selectionRect.xMax is already offset from the window's right edge by Unity (leaving space for the scrollbar/prefab button).
            // Thus, we start drawing directly from selectionRect.xMax.
            float currentX = selectionRect.xMax;

            for (int i = 0; i < drawers.Count; i++)
            {
                var drawer = drawers[i];
                if (!drawer.IsEnabled) continue;

                float width = drawer.GetWidth(gameObject);
                if (width <= 0) continue;

                currentX -= width;
                Rect rect = new Rect(currentX, selectionRect.y, width, selectionRect.height);
                drawer.Draw(gameObject, rect);
            }
        }
    }
}
