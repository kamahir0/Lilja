namespace Lilja.HierarchyDecorator
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    [InitializeOnLoad]
    public static class HierarchyDecorator
    {
        private static readonly List<IHierarchyItemDrawer> drawers = new();

        static HierarchyDecorator()
        {
            // Register drawers. The first added drawer will be placed closest to the right (next to the built-in Prefab arrow button).
            drawers.Add(new ActiveToggleDrawer());
            drawers.Add(new MissingScriptPingButtonDrawer());

            EditorApplication.hierarchyWindowItemByEntityIdOnGUI += OnHierarchyWindowItemGUI;
        }

        private static void OnHierarchyWindowItemGUI(EntityId entityId, Rect selectionRect)
        {
            var gameObject = EditorUtility.EntityIdToObject(entityId) as GameObject;
            if (gameObject == null) return;

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
