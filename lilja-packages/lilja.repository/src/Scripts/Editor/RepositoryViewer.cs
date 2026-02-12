using System.Linq;
using UnityEditor;
using UnityEngine;
using Lilja.Repository.Diagnostics;

namespace Lilja.Repository.Editor
{
    public class RepositoryViewer : EditorWindow
    {
        private const string EnableAutoReloadKey = "RepositoryViewer_EnableAutoReload";
        private const string RepositoryTypeKey = "RepositoryViewer_RepositoryType";

        private static int _interval;
        private static RepositoryViewer _window;

        [MenuItem("Lilja/Repository/Repository Viewer")]
        public static void ShowWindow()
        {
            if (_window != null)
            {
                _window.Close();
            }

            GetWindow<RepositoryViewer>("Repository Viewer").Show();
        }

        private static readonly GUILayoutOption[] EmptyLayoutOption = new GUILayoutOption[0];

        private RepositoryTrackerTreeView _treeView;
        private object _splitterState;
        private RepositoryTracker.RepositoryType _currentType;
        private bool _enableAutoReload;
        private bool _isDirty = true;

        void OnEnable()
        {
            _window = this;
            _splitterState = SplitterGUILayout.CreateSplitterState(new[] { 70f, 30f }, new[] { 100, 100 }, null);

            _currentType = (RepositoryTracker.RepositoryType)EditorPrefs.GetInt(RepositoryTypeKey, 0);
            _enableAutoReload = EditorPrefs.GetBool(EnableAutoReloadKey, false);

            _treeView = new RepositoryTrackerTreeView(_currentType);
        }

        void OnGUI()
        {
            RenderToolbar();

            // Status message
            if (!Application.isPlaying && _currentType == RepositoryTracker.RepositoryType.InMemory)
            {
                EditorGUILayout.HelpBox("InMemory repositories are only available in Play Mode.", MessageType.Info);
            }

            SplitterGUILayout.BeginVerticalSplit(this._splitterState, EmptyLayoutOption);
            {
                RenderTable();
                RenderDetailsPanel();
            }
            SplitterGUILayout.EndVerticalSplit();
        }

        #region Toolbar

        static readonly GUIContent EnableAutoReloadContent =
            EditorGUIUtility.TrTextContent("Auto Reload", "Reload automatically every 2 seconds.");

        static readonly GUIContent ReloadContent =
            EditorGUIUtility.TrTextContent("Reload", "Reload repositories now.");

        static readonly GUIContent RepositoryTypeContent =
            EditorGUIUtility.TrTextContent("Repository Type", "Select repository storage type.");

        void RenderToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, EmptyLayoutOption);

            // Repository Type Dropdown
            var newType = (RepositoryTracker.RepositoryType)EditorGUILayout.EnumPopup(
                _currentType,
                EditorStyles.toolbarDropDown,
                GUILayout.Width(120)
            );

            if (newType != _currentType)
            {
                _currentType = newType;
                EditorPrefs.SetInt(RepositoryTypeKey, (int)_currentType);
                _treeView.SetRepositoryType(_currentType);
                _isDirty = true;
            }

            // Auto Reload Toggle
            var newAutoReload = GUILayout.Toggle(
                _enableAutoReload,
                EnableAutoReloadContent,
                EditorStyles.toolbarButton,
                EmptyLayoutOption
            );

            if (newAutoReload != _enableAutoReload)
            {
                _enableAutoReload = newAutoReload;
                EditorPrefs.SetBool(EnableAutoReloadKey, _enableAutoReload);
            }

            GUILayout.FlexibleSpace();

            // Reload Button
            if (GUILayout.Button(ReloadContent, EditorStyles.toolbarButton, EmptyLayoutOption))
            {
                _isDirty = true;
            }

            // Item Count
            var itemCount = _treeView?.CurrentBindingItems?.Count ?? 0;
            var totalItems = 0;
            if (_treeView?.CurrentBindingItems != null)
            {
                foreach (var item in _treeView.CurrentBindingItems)
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

        private Vector2 _tableScroll;
        private GUIStyle _tableListStyle;

        void RenderTable()
        {
            if (_tableListStyle == null)
            {
                _tableListStyle = new GUIStyle("CN Box");
                _tableListStyle.margin.top = 0;
                _tableListStyle.padding.left = 3;
            }

            EditorGUILayout.BeginVertical(_tableListStyle, EmptyLayoutOption);

            this._tableScroll = EditorGUILayout.BeginScrollView(
                _tableScroll,
                GUILayout.ExpandWidth(true),
                GUILayout.MaxWidth(2000f));

            var controlRect = EditorGUILayout.GetControlRect(
                GUILayout.ExpandHeight(true),
                GUILayout.ExpandWidth(true));

            _treeView?.OnGUI(controlRect);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void Update()
        {
            if (_enableAutoReload && Application.isPlaying)
            {
                if (_interval++ % 120 == 0) // Every 2 seconds at 60fps
                {
                    _isDirty = true;
                }
            }

            if (_isDirty)
            {
                _isDirty = false;
                _treeView.ReloadAndSort();
                Repaint();
            }
        }

        #endregion

        #region Details Panel

        private static GUIStyle _detailsStyle;
        private static GUIStyle _detailsHeaderStyle;
        private Vector2 _detailsScroll;

        void RenderDetailsPanel()
        {
            if (_detailsStyle == null)
            {
                _detailsStyle = new GUIStyle("CN Message");
                _detailsStyle.wordWrap = true;
                _detailsStyle.stretchHeight = true;
                _detailsStyle.margin.right = 15;
                _detailsStyle.padding = new RectOffset(10, 10, 10, 10);
            }

            if (_detailsHeaderStyle == null)
            {
                _detailsHeaderStyle = new GUIStyle(EditorStyles.boldLabel);
                _detailsHeaderStyle.padding = new RectOffset(10, 10, 5, 5);
            }

            EditorGUILayout.BeginVertical(EmptyLayoutOption);

            string headerText = "Details";
            string detailsText = "Select an item to view details.";
            object selectedValue;

            var selected = _treeView.state.selectedIDs;
            if (selected.Count > 0)
            {
                var first = selected[0];
                var item =
                    _treeView.CurrentBindingItems?.FirstOrDefault(x => x.id == first) as RepositoryTrackerViewItem;
                if (item != null)
                {
                    if (item.IsRepository)
                    {
                        headerText = $"{item.RepositoryName} - {item.ItemCount} items";
                        detailsText =
                            $"Repository Type: {item.Type}\nStorage Type: {_currentType}\n\nSelect a child item to view its data.";
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
            EditorGUILayout.LabelField(headerText, _detailsHeaderStyle);

            // Scrollable details
            _detailsScroll = EditorGUILayout.BeginScrollView(this._detailsScroll, EmptyLayoutOption);

            var vector = _detailsStyle.CalcSize(new GUIContent(detailsText));
            EditorGUILayout.SelectableLabel(
                detailsText,
                _detailsStyle,
                GUILayout.ExpandHeight(true),
                GUILayout.ExpandWidth(true),
                GUILayout.MinWidth(vector.x),
                GUILayout.MinHeight(vector.y)
            );

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