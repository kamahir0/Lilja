using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Lilja.CustomProjectWindow
{
    internal enum QuickAddModifier
    {
        Alt,
        Shift,
        ControlOrCommand,
    }

    [InitializeOnLoad]
    internal static class CustomProjectQuickAdd
    {
        private const string ModifierPrefKey = "CustomProjectView_QuickAddModifier";

        private static QuickAddModifier _modifier;

        static CustomProjectQuickAdd()
        {
            _modifier = (QuickAddModifier)EditorPrefs.GetInt(ModifierPrefKey, (int)QuickAddModifier.Alt);
            EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
        }

        internal static QuickAddModifier Modifier
        {
            get => _modifier;
            set
            {
                _modifier = value;
                EditorPrefs.SetInt(ModifierPrefKey, (int)value);
            }
        }

        internal static bool IsModifierActive(Event evt)
        {
            if (evt == null)
            {
                return false;
            }

            return _modifier switch
            {
                QuickAddModifier.Alt => evt.alt,
                QuickAddModifier.Shift => evt.shift,
                QuickAddModifier.ControlOrCommand => evt.control || evt.command,
                _ => evt.alt,
            };
        }

        private static void OnProjectWindowItemGUI(string guid, Rect selectionRect)
        {
            var evt = Event.current;
            if (evt == null || evt.type != EventType.ContextClick)
            {
                return;
            }

            if (!IsModifierActive(evt))
            {
                return;
            }

            if (!selectionRect.Contains(evt.mousePosition))
            {
                return;
            }

            if (!EditorWindow.HasOpenInstances<CustomProjectWindow>())
            {
                return;
            }

            var window = EditorWindow.GetWindow<CustomProjectWindow>(false, null, false);
            if (window == null || window.Model == null)
            {
                return;
            }

            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            evt.Use();

            if (AssetDatabase.IsValidFolder(assetPath))
            {
                ShowFolderTypeMenu(assetPath, window);
            }
            else
            {
                ShowGroupMenu(window, node => PerformAddAsset(guid, node, window));
            }
        }

        private static void ShowFolderTypeMenu(string assetPath, CustomProjectWindow window)
        {
            var menu = new GenericMenu();
            const string pointerPrefix = "フォルダポインターとして追加";
            const string refPrefix = "フォルダ参照として追加";

            BuildFolderGroupMenu(menu, pointerPrefix, window,
                node => PerformAddFolderPointer(assetPath, node, window));
            BuildFolderGroupMenu(menu, refPrefix, window,
                node => PerformAddFolderRef(assetPath, node, window));

            menu.ShowAsContext();
        }

        private static void BuildFolderGroupMenu(
            GenericMenu menu,
            string typePrefix,
            CustomProjectWindow window,
            Action<CustomProjectNode> addAction)
        {
            menu.AddItem(new GUIContent(typePrefix + "/ルート（グループなし）"), false, () => addAction(null));

            var roots = window.Model.Roots;
            var hasGroups = roots.Any(n => n.IsManualGroup);
            if (hasGroups)
            {
                menu.AddSeparator(typePrefix + "/");
                AddGroupsToMenu(menu, roots, typePrefix, addAction);
            }
        }

        private static void ShowGroupMenu(CustomProjectWindow window, Action<CustomProjectNode> addAction)
        {
            BuildGroupMenu(window, addAction).ShowAsContext();
        }

        private static GenericMenu BuildGroupMenu(CustomProjectWindow window, Action<CustomProjectNode> addAction)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("ルート（グループなし）"), false, () => addAction(null));

            var roots = window.Model.Roots;
            var hasGroups = roots.Any(n => n.IsManualGroup);
            if (hasGroups)
            {
                menu.AddSeparator(string.Empty);
                AddGroupsToMenu(menu, roots, string.Empty, addAction);
            }

            return menu;
        }

        private static void AddGroupsToMenu(
            GenericMenu menu,
            List<CustomProjectNode> nodes,
            string prefix,
            Action<CustomProjectNode> addAction)
        {
            foreach (var node in nodes)
            {
                if (!node.IsManualGroup)
                {
                    continue;
                }

                var safeLabel = node.Label.Replace("/", "\u2215");
                var nodePath = string.IsNullOrEmpty(prefix) ? safeLabel : prefix + "/" + safeLabel;
                var capturedNode = node;

                var hasChildGroups = node.Children != null
                    && node.Children.Any(c => c.IsManualGroup);

                if (hasChildGroups)
                {
                    menu.AddItem(new GUIContent(nodePath + "/ここに追加"), false, () => addAction(capturedNode));
                    AddGroupsToMenu(menu, node.Children, nodePath, addAction);
                }
                else
                {
                    menu.AddItem(new GUIContent(nodePath), false, () => addAction(capturedNode));
                }
            }
        }

        private static void PerformAddAsset(string guid, CustomProjectNode parent, CustomProjectWindow window)
        {
            var node = window.Model.AddAssetRef(guid, parent);
            if (node != null)
            {
                window.ReloadAndRevealNode(node);
            }
        }

        private static void PerformAddFolderPointer(string assetPath, CustomProjectNode parent, CustomProjectWindow window)
        {
            var node = window.Model.AddFolderPointer(assetPath, parent);
            if (node != null)
            {
                window.ReloadAndRevealNode(node);
            }
        }

        private static void PerformAddFolderRef(string assetPath, CustomProjectNode parent, CustomProjectWindow window)
        {
            var node = window.Model.AddFolderRef(assetPath, parent);
            if (node != null)
            {
                window.ReloadAndRevealNode(node);
            }
        }
    }
}
