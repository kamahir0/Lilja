using System;
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
        private static readonly GUILayoutOption[] EmptyLayoutOption = new GUILayoutOption[0];

        private RepositoryTrackerTreeView _treeView;
        private object _splitterState;
        private RepositoryTracker.RepositoryType _currentType;
        private RepositoryTracker.RepositoryType[] _availableTypes = Array.Empty<RepositoryTracker.RepositoryType>();
        private bool _enableAutoReload;
        private bool _isDirty = true;
        private Vector2 _tableScroll;
        private GUIStyle _tableListStyle;
        private static GUIStyle _detailsStyle;
        private static GUIStyle _detailsHeaderStyle;
        private Vector2 _detailsScroll;

        [MenuItem("Lilja/Repository/Repository Viewer")]
        public static void ShowWindow()
        {
            if (_window != null)
            {
                _window.Close();
            }

            GetWindow<RepositoryViewer>("Repository Viewer").Show();
        }

        private void OnEnable()
        {
            _window = this;
            _splitterState = SplitterGUILayout.CreateSplitterState(new[] { 70f, 30f }, new[] { 100, 100 }, null);

            _availableTypes = RepositoryTrackerTreeView.GetAvailableRepositoryTypes();
            _currentType = (RepositoryTracker.RepositoryType)EditorPrefs.GetInt(RepositoryTypeKey, 0);
            if (_availableTypes.Length == 0)
            {
                _availableTypes = new[] { RepositoryTracker.RepositoryType.InMemory, RepositoryTracker.RepositoryType.Json };
            }

            if (!_availableTypes.Contains(_currentType))
            {
                _currentType = _availableTypes[0];
            }

            _enableAutoReload = EditorPrefs.GetBool(EnableAutoReloadKey, false);
            _treeView = new RepositoryTrackerTreeView(_currentType);
        }

        private void OnGUI()
        {
            RenderToolbar();

            if (!Application.isPlaying && _currentType == RepositoryTracker.RepositoryType.InMemory)
            {
                EditorGUILayout.HelpBox("InMemory repositories are only available in Play Mode.", MessageType.Info);
            }

            SplitterGUILayout.BeginVerticalSplit(_splitterState, EmptyLayoutOption);
            {
                RenderTable();
                RenderDetailsPanel();
            }
            SplitterGUILayout.EndVerticalSplit();
        }

        private void RenderToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, EmptyLayoutOption);

            var labels = _availableTypes.Select(type => type.ToString()).ToArray();
            var currentIndex = Array.IndexOf(_availableTypes, _currentType);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            var newIndex = EditorGUILayout.Popup(currentIndex, labels, EditorStyles.toolbarDropDown, GUILayout.Width(140));
            var newType = _availableTypes[newIndex];
            if (newType != _currentType)
            {
                _currentType = newType;
                EditorPrefs.SetInt(RepositoryTypeKey, (int)_currentType);
                _treeView.SetRepositoryType(_currentType);
                _isDirty = true;
            }

            var newAutoReload = GUILayout.Toggle(
                _enableAutoReload,
                EditorGUIUtility.TrTextContent("Auto Reload", "Reload automatically every 2 seconds."),
                EditorStyles.toolbarButton,
                EmptyLayoutOption);
            if (newAutoReload != _enableAutoReload)
            {
                _enableAutoReload = newAutoReload;
                EditorPrefs.SetBool(EnableAutoReloadKey, _enableAutoReload);
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(
                    EditorGUIUtility.TrTextContent("Reload", "Reload repositories now."),
                    EditorStyles.toolbarButton,
                    EmptyLayoutOption))
            {
                _isDirty = true;
            }

            if (_currentType != RepositoryTracker.RepositoryType.InMemory &&
                GUILayout.Button("Open Directory", EditorStyles.toolbarButton, EmptyLayoutOption))
            {
                EditorUtility.RevealInFinder(Application.persistentDataPath);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void RenderTable()
        {
            if (_tableListStyle == null)
            {
                _tableListStyle = new GUIStyle("CN Box");
                _tableListStyle.margin.top = 0;
                _tableListStyle.padding.left = 3;
            }

            EditorGUILayout.BeginVertical(_tableListStyle, EmptyLayoutOption);
            _tableScroll = EditorGUILayout.BeginScrollView(
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

        private void Update()
        {
            if (_enableAutoReload && Application.isPlaying && _interval++ % 120 == 0)
            {
                _isDirty = true;
            }

            if (_isDirty)
            {
                _isDirty = false;
                _treeView.ReloadAndSort();
                Repaint();
            }
        }

        private void RenderDetailsPanel()
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

            var headerText = "Details";
            var detailsText = "Select an item to view details.";
            object selectedValue = null;

            var selected = _treeView.state.selectedIDs;
            if (selected.Count > 0)
            {
                var item = _treeView.FindItemById(selected[0]);
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

            EditorGUILayout.LabelField(headerText, _detailsHeaderStyle);
            _detailsScroll = EditorGUILayout.BeginScrollView(_detailsScroll, EmptyLayoutOption);

            var textSize = _detailsStyle.CalcSize(new GUIContent(detailsText));
            EditorGUILayout.SelectableLabel(
                detailsText,
                _detailsStyle,
                GUILayout.ExpandHeight(true),
                GUILayout.ExpandWidth(true),
                GUILayout.MinWidth(textSize.x),
                GUILayout.MinHeight(textSize.y));

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }
    }
}
