namespace Lilja.HierarchyDecorator
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    public class MissingScriptPingButtonDrawer : IHierarchyItemDrawer
    {
        public const string PrefKey = "Lilja.HierarchyDecorator.MissingScript.Enabled";
        private const float Width = 16f;

        public bool IsEnabled => EditorPrefs.GetBool(PrefKey, true);

        // Cache parent GameObject entity ID to the GameObject that has the missing script.
        private static readonly Dictionary<EntityId, GameObject> missingTargetCache = new();

        static MissingScriptPingButtonDrawer()
        {
            EditorApplication.hierarchyChanged += ClearCache;
        }

        private static void ClearCache()
        {
            missingTargetCache.Clear();
        }

        private GameObject GetMissingScriptTarget(GameObject gameObject)
        {
            if (gameObject == null) return null;
            EntityId id = gameObject.GetEntityId();
            if (missingTargetCache.TryGetValue(id, out var target))
            {
                return target;
            }

            target = FindMissingScriptGameObject(gameObject);
            missingTargetCache[id] = target;
            return target;
        }

        private GameObject FindMissingScriptGameObject(GameObject parent)
        {
            if (parent == null) return null;
            
            // Get all transforms in children (including inactive ones)
            var transforms = parent.GetComponentsInChildren<Transform>(true);
            foreach (var t in transforms)
            {
                if (t != null)
                {
                    // Use GameObjectUtility.GetMonoBehavioursWithMissingScriptCount as a fast C++ side scan
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0)
                    {
                        return t.gameObject;
                    }
                }
            }
            return null;
        }

        public float GetWidth(GameObject gameObject)
        {
            var target = GetMissingScriptTarget(gameObject);
            return target != null ? Width : 0f;
        }

        public void Draw(GameObject gameObject, Rect rect)
        {
            var target = GetMissingScriptTarget(gameObject);
            if (target == null) return;

            // Retrieve a warning icon from Unity's built-in editor resources
            GUIContent iconContent = EditorGUIUtility.IconContent("console.warnicon.sml");
            if (iconContent == null || iconContent.image == null)
            {
                iconContent = new GUIContent("!");
            }
            iconContent.tooltip = string.Empty;

            float buttonSize = 14f;
            Rect buttonRect = new Rect(
                rect.x + (rect.width - buttonSize) / 2f,
                rect.y + (rect.height - buttonSize) / 2f,
                buttonSize,
                buttonSize
            );

            // Draw as a transparent icon button using GUIStyle.none
            if (GUI.Button(buttonRect, iconContent, GUIStyle.none))
            {
                EditorGUIUtility.PingObject(target);
                Selection.activeGameObject = target;
            }
        }
    }
}
