using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Lilja.Repository.Diagnostics;
using MessagePack;
using MessagePack.Resolvers;
using MessagePack.Formatters;

namespace Lilja.Repository.Editor
{
    public class RepositoryViewer : EditorWindow
    {
        const string EnableAutoReloadKey = "RepositoryViewer_EnableAutoReload";
        const string RepositoryTypeKey = "RepositoryViewer_RepositoryType";

        static int interval;
        static RepositoryViewer window;

        [MenuItem("Lilja/Repository/Repository Viewer")]
        public static void ShowWindow()
        {
            if (window != null)
            {
                window.Close();
            }

            GetWindow<RepositoryViewer>("Repository Viewer").Show();
        }

        static readonly GUILayoutOption[] EmptyLayoutOption = new GUILayoutOption[0];

        RepositoryTrackerTreeView treeView;
        object splitterState;
        RepositoryTracker.RepositoryType currentType;
        bool enableAutoReload;
        bool isDirty = true;

        void OnEnable()
        {
            window = this;
            splitterState =
                SplitterGUILayout.CreateSplitterState(new float[] { 70f, 30f }, new int[] { 100, 100 }, null);

            currentType = (RepositoryTracker.RepositoryType)EditorPrefs.GetInt(RepositoryTypeKey, 0);
            enableAutoReload = EditorPrefs.GetBool(EnableAutoReloadKey, false);

            treeView = new RepositoryTrackerTreeView(currentType);
        }

        void OnGUI()
        {
            RenderToolbar();

            // Status message
            if (!Application.isPlaying && currentType == RepositoryTracker.RepositoryType.InMemory)
            {
                EditorGUILayout.HelpBox("InMemory repositories are only available in Play Mode.", MessageType.Info);
            }

            SplitterGUILayout.BeginVerticalSplit(this.splitterState, EmptyLayoutOption);
            {
                RenderTable();
                RenderDetailsPanel();
            }
            SplitterGUILayout.EndVerticalSplit();
        }

        #region Toolbar

        static readonly GUIContent EnableAutoReloadContent =
            EditorGUIUtility.TrTextContent("Auto Reload", "Reload automatically every 2 seconds.", (Texture)null);

        static readonly GUIContent ReloadContent =
            EditorGUIUtility.TrTextContent("Reload", "Reload repositories now.", (Texture)null);

        static readonly GUIContent RepositoryTypeContent =
            EditorGUIUtility.TrTextContent("Repository Type", "Select repository storage type.", (Texture)null);

        void RenderToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, EmptyLayoutOption);

            // Repository Type Dropdown
            var newType = (RepositoryTracker.RepositoryType)EditorGUILayout.EnumPopup(
                currentType,
                EditorStyles.toolbarDropDown,
                GUILayout.Width(120)
            );

            if (newType != currentType)
            {
                currentType = newType;
                EditorPrefs.SetInt(RepositoryTypeKey, (int)currentType);
                treeView.SetRepositoryType(currentType);
                isDirty = true;
            }

            // Auto Reload Toggle
            var newAutoReload = GUILayout.Toggle(
                enableAutoReload,
                EnableAutoReloadContent,
                EditorStyles.toolbarButton,
                EmptyLayoutOption
            );

            if (newAutoReload != enableAutoReload)
            {
                enableAutoReload = newAutoReload;
                EditorPrefs.SetBool(EnableAutoReloadKey, enableAutoReload);
            }

            GUILayout.FlexibleSpace();

            // Reload Button
            if (GUILayout.Button(ReloadContent, EditorStyles.toolbarButton, EmptyLayoutOption))
            {
                isDirty = true;
            }

            // Item Count
            var itemCount = treeView?.CurrentBindingItems?.Count ?? 0;
            var totalItems = 0;
            if (treeView?.CurrentBindingItems != null)
            {
                foreach (var item in treeView.CurrentBindingItems)
                {
                    if (item is RepositoryTrackerViewItem repoItem && repoItem.IsRepository)
                    {
                        totalItems += repoItem.ItemCount;
                    }
                }
            }

            GUILayout.Label($"Repositories: {itemCount} | Items: {totalItems}", EditorStyles.toolbarButton);

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Table

        Vector2 tableScroll;
        GUIStyle tableListStyle;

        void RenderTable()
        {
            if (tableListStyle == null)
            {
                tableListStyle = new GUIStyle("CN Box");
                tableListStyle.margin.top = 0;
                tableListStyle.padding.left = 3;
            }

            EditorGUILayout.BeginVertical(tableListStyle, EmptyLayoutOption);

            this.tableScroll = EditorGUILayout.BeginScrollView(this.tableScroll, new GUILayoutOption[]
            {
                GUILayout.ExpandWidth(true),
                GUILayout.MaxWidth(2000f)
            });

            var controlRect = EditorGUILayout.GetControlRect(new GUILayoutOption[]
            {
                GUILayout.ExpandHeight(true),
                GUILayout.ExpandWidth(true)
            });

            treeView?.OnGUI(controlRect);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void Update()
        {
            if (enableAutoReload && Application.isPlaying)
            {
                if (interval++ % 120 == 0) // Every 2 seconds at 60fps
                {
                    isDirty = true;
                }
            }

            if (isDirty)
            {
                isDirty = false;
                treeView.ReloadAndSort();
                Repaint();
            }
        }

        #endregion

        #region Details Panel

        static GUIStyle detailsStyle;
        static GUIStyle detailsHeaderStyle;
        Vector2 detailsScroll;

        void RenderDetailsPanel()
        {
            if (detailsStyle == null)
            {
                detailsStyle = new GUIStyle("CN Message");
                detailsStyle.wordWrap = true;
                detailsStyle.stretchHeight = true;
                detailsStyle.margin.right = 15;
                detailsStyle.padding = new RectOffset(10, 10, 10, 10);
            }

            if (detailsHeaderStyle == null)
            {
                detailsHeaderStyle = new GUIStyle(EditorStyles.boldLabel);
                detailsHeaderStyle.padding = new RectOffset(10, 10, 5, 5);
            }

            EditorGUILayout.BeginVertical(EmptyLayoutOption);

            string headerText = "Details";
            string detailsText = "Select an item to view details.";
            object selectedValue = null;

            var selected = treeView.state.selectedIDs;
            if (selected.Count > 0)
            {
                var first = selected[0];
                var item =
                    treeView.CurrentBindingItems?.FirstOrDefault(x => x.id == first) as RepositoryTrackerViewItem;
                if (item != null)
                {
                    if (item.IsRepository)
                    {
                        headerText = $"{item.RepositoryName} - {item.ItemCount} items";
                        detailsText =
                            $"Repository Type: {item.Type}\nStorage Type: {currentType}\n\nSelect a child item to view its data.";
                    }
                    else
                    {
                        headerText = $"{item.Key} ({item.Type})";
                        selectedValue = item.FullValue;

                        if (selectedValue != null)
                        {
                            try
                            {
                                detailsText = JsonUtility.ToJson(selectedValue, true);
                            }
                            catch
                            {
                                detailsText = selectedValue.ToString();
                            }
                        }
                        else
                        {
                            detailsText = "null";
                        }
                    }
                }
            }

            // Header
            EditorGUILayout.LabelField(headerText, detailsHeaderStyle);

            // Scrollable details
            detailsScroll = EditorGUILayout.BeginScrollView(this.detailsScroll, EmptyLayoutOption);

            var vector = detailsStyle.CalcSize(new GUIContent(detailsText));
            EditorGUILayout.SelectableLabel(detailsText, detailsStyle, new GUILayoutOption[]
            {
                GUILayout.ExpandHeight(true),
                GUILayout.ExpandWidth(true),
                GUILayout.MinWidth(vector.x),
                GUILayout.MinHeight(vector.y)
            });

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        #endregion

        #region File Loading (for Edit Mode)

        // This section can be expanded later to support file-based loading in Edit Mode
        // similar to the original implementation's LoadFromFiles method

        #endregion
    }
}