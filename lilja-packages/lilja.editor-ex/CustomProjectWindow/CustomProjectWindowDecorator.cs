using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Lilja.CustomProjectWindow
{
    [InitializeOnLoad]
    internal static class CustomProjectWindowDecorator
    {
        private static readonly HashSet<string> RegisteredGuids = new(System.StringComparer.Ordinal);
        private static readonly GUIContent StarButtonContent = new("★", "カスタム Project ウィンドウで表示");
        private static GUIStyle _starStyle;
        private static GUIStyle _starButtonStyle;

        static CustomProjectWindowDecorator()
        {
            EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
            EditorApplication.delayCall += TryInitializeFromOpenWindow;
        }

        internal static void RefreshFromModel(CustomProjectTreeModel model)
        {
            RegisteredGuids.Clear();
            if (model != null)
            {
                CollectRegisteredGuids(model.Roots, RegisteredGuids);
            }

            EditorApplication.RepaintProjectWindow();
        }

        internal static void Clear()
        {
            RegisteredGuids.Clear();
            EditorApplication.RepaintProjectWindow();
        }

        private static void TryInitializeFromOpenWindow()
        {
            if (!EditorWindow.HasOpenInstances<CustomProjectWindow>())
            {
                return;
            }

            var window = EditorWindow.GetWindow<CustomProjectWindow>(false, null, false);
            if (window?.Model != null)
            {
                RefreshFromModel(window.Model);
            }
        }

        private static void CollectRegisteredGuids(List<CustomProjectNode> nodes, HashSet<string> guids)
        {
            if (nodes == null)
            {
                return;
            }

            foreach (var node in nodes)
            {
                if (node.Source == ProjectNodeSource.FolderRefSynced)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(node.AssetGuid))
                {
                    guids.Add(node.AssetGuid);
                }

                CollectRegisteredGuids(node.Children, guids);
            }
        }

        private static void OnProjectWindowItemGUI(string guid, Rect selectionRect)
        {
            if (string.IsNullOrEmpty(guid) || !RegisteredGuids.Contains(guid))
            {
                return;
            }

            if (selectionRect.height > EditorGUIUtility.singleLineHeight + 4f)
            {
                return;
            }

            EnsureStyle();

            var starRect = new Rect(
                selectionRect.xMax - selectionRect.height,
                selectionRect.y,
                selectionRect.height,
                selectionRect.height);

            EditorGUIUtility.AddCursorRect(starRect, MouseCursor.Link);

            if (GUI.Button(starRect, StarButtonContent, _starButtonStyle))
            {
                CustomProjectWindow.FocusAsset(guid);
            }
        }

        private static void EnsureStyle()
        {
            if (_starStyle != null)
            {
                return;
            }

            _starStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.82f, 0f) },
                fontSize = 11,
                padding = new RectOffset(0, 0, 0, 0),
            };

            _starButtonStyle = new GUIStyle(_starStyle);
            _starButtonStyle.normal.background = null;
            _starButtonStyle.hover.background = null;
            _starButtonStyle.hover.textColor = new Color(1f, 0.95f, 0.4f);
            _starButtonStyle.active.background = null;
            _starButtonStyle.active.textColor = new Color(1f, 1f, 0.6f);
            _starButtonStyle.focused.background = null;
        }
    }
}
