using UnityEditor;
using UnityEngine;

namespace Lilja.HierarchyDecorator
{
    public class ActiveToggleDrawer : IHierarchyItemDrawer
    {
        public const string PrefKey = "Lilja.HierarchyDecorator.ActiveToggle.Enabled";
        private const float Width = 16f;

        public bool IsEnabled => EditorPrefs.GetBool(PrefKey, true);

        public float GetWidth(GameObject gameObject)
        {
            return Width;
        }

        public void Draw(GameObject gameObject, Rect rect)
        {
            float toggleSize = 14f;
            Rect toggleRect = new Rect(
                rect.x + (rect.width - toggleSize) / 2f,
                rect.y + (rect.height - toggleSize) / 2f,
                toggleSize,
                toggleSize
            );

            EditorGUI.BeginChangeCheck();
            bool isActive = GUI.Toggle(toggleRect, gameObject.activeSelf, GUIContent.none);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(gameObject, isActive ? "Activate GameObject" : "Deactivate GameObject");
                gameObject.SetActive(isActive);
                EditorUtility.SetDirty(gameObject);
            }
        }
    }
}
