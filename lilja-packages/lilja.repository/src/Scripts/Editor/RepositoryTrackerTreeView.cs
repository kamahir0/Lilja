using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Lilja.Repository.Diagnostics;

namespace Lilja.Repository.Editor
{
    public class RepositoryTrackerTreeView : TreeView<int>
    {
        private static readonly RepositoryTracker.RepositoryType[] FallbackRepositoryTypes =
        {
            RepositoryTracker.RepositoryType.InMemory,
            RepositoryTracker.RepositoryType.Json,
        };

        public IReadOnlyList<TreeViewItem<int>> CurrentBindingItems;
        private RepositoryTracker.RepositoryType _currentType;

        public static RepositoryTracker.RepositoryType[] GetAvailableRepositoryTypes()
        {
            if (MessagePackReflectionBridge.IsAvailable)
            {
                return new[]
                {
                    RepositoryTracker.RepositoryType.InMemory,
                    RepositoryTracker.RepositoryType.Json,
                    RepositoryTracker.RepositoryType.MessagePack,
                };
            }

            return FallbackRepositoryTypes;
        }

        public RepositoryTrackerTreeView(RepositoryTracker.RepositoryType type)
            : this(new TreeViewState<int>(), new MultiColumnHeader(new MultiColumnHeaderState(new[]
            {
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Entity/Key"),
                    width = 250,
                    minWidth = 100,
                    autoResize = true,
                    canSort = false,
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Value Preview"),
                    width = 400,
                    minWidth = 100,
                    autoResize = true,
                    canSort = false,
                },
            })), type)
        {
        }

        private RepositoryTrackerTreeView(TreeViewState<int> state, MultiColumnHeader header, RepositoryTracker.RepositoryType type)
            : base(state, header)
        {
            _currentType = type;
            rowHeight = 20;
            showAlternatingRowBackgrounds = true;
            showBorder = true;
            useScrollView = true;
            header.ResizeToFit();
            Reload();
        }

        public void SetRepositoryType(RepositoryTracker.RepositoryType type)
        {
            _currentType = type;
        }

        public RepositoryTrackerViewItem FindItemById(int id)
        {
            return FindItemRecursive(CurrentBindingItems, id);
        }

        public void ReloadAndSort()
        {
            var currentSelected = state.selectedIDs;
            Reload();
            state.selectedIDs = currentSelected;
        }

        protected override TreeViewItem<int> BuildRoot()
        {
            var root = new TreeViewItem<int> { depth = -1 };
            var id = 0;
            var children = RepositoryTreeDataLoader.Load(_currentType, ref id);
            CurrentBindingItems = children;
            root.children = children;
            return root;
        }

        protected override bool CanMultiSelect(TreeViewItem<int> item)
        {
            return false;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            var item = args.item as RepositoryTrackerViewItem;
            if (item == null)
            {
                return;
            }

            for (var visibleColumnIndex = 0; visibleColumnIndex < args.GetNumVisibleColumns(); visibleColumnIndex++)
            {
                var rect = args.GetCellRect(visibleColumnIndex);
                var columnIndex = args.GetColumn(visibleColumnIndex);
                var labelStyle = args.selected ? EditorStyles.whiteLabel : EditorStyles.label;
                labelStyle.alignment = TextAnchor.MiddleLeft;

                switch (columnIndex)
                {
                    case 0:
                        if (item.IsRepository)
                        {
                            DrawRepositoryRow(rect, item, labelStyle);
                        }
                        else
                        {
                            rect.xMin += 15f;
                            EditorGUI.LabelField(rect, item.Key, labelStyle);
                        }

                        break;
                    case 1:
                        EditorGUI.LabelField(rect, item.ValuePreview, labelStyle);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(columnIndex), columnIndex, null);
                }
            }
        }

        private RepositoryTrackerViewItem FindItemRecursive(IEnumerable<TreeViewItem<int>> items, int id)
        {
            if (items == null)
            {
                return null;
            }

            foreach (var item in items)
            {
                if (item.id == id)
                {
                    return item as RepositoryTrackerViewItem;
                }

                if (item.hasChildren)
                {
                    var found = FindItemRecursive(item.children, id);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return null;
        }

        private void DrawRepositoryRow(Rect rect, RepositoryTrackerViewItem item, GUIStyle labelStyle)
        {
            var displayName = NormalizeRepositoryName(item.RepositoryName);
            displayName += $" ({item.ItemCount})";
            var expanded = IsExpanded(item.id);
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                SetExpanded(item.id, !expanded);
                Event.current.Use();
            }

            var foldoutRect = new Rect(rect.x, rect.y, 12f, rect.height);
            EditorGUI.Foldout(foldoutRect, expanded, GUIContent.none, true);

            var labelRect = new Rect(rect.x + 14f, rect.y, rect.width - 14f, rect.height);
            var boldStyle = new GUIStyle(labelStyle) { fontStyle = FontStyle.Bold };
            EditorGUI.LabelField(labelRect, displayName, boldStyle);
        }

        private string NormalizeRepositoryName(string repositoryName)
        {
            if (string.IsNullOrEmpty(repositoryName))
            {
                return repositoryName;
            }

            var displayName = Path.GetFileNameWithoutExtension(repositoryName);

            if (displayName.EndsWith("Repository", StringComparison.Ordinal))
            {
                displayName = displayName.Substring(0, displayName.Length - "Repository".Length);
            }

            var backendPrefix = GetRepositoryPrefix(_currentType);
            if (!string.IsNullOrEmpty(backendPrefix) &&
                displayName.StartsWith(backendPrefix, StringComparison.Ordinal))
            {
                displayName = displayName.Substring(backendPrefix.Length);
            }

            return displayName;
        }

        private static string GetRepositoryPrefix(RepositoryTracker.RepositoryType repositoryType)
        {
            switch (repositoryType)
            {
                case RepositoryTracker.RepositoryType.InMemory:
                    return "InMemory";
                case RepositoryTracker.RepositoryType.Json:
                    return "Json";
                case RepositoryTracker.RepositoryType.MessagePack:
                    return "MessagePack";
                default:
                    return string.Empty;
            }
        }
    }
}
