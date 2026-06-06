using System;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Lilja.CustomProjectWindow
{
    public sealed class CustomProjectWindow : EditorWindow, IHasCustomMenu
    {
        private static readonly Type SceneHierarchyWindowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.SceneHierarchyWindow");
        private static readonly GUIContent RootCreateButtonContent = new(string.Empty, "ルートに項目を追加");
        private const float RootCreateButtonWidth = 38f;
        private const float ToolbarIconSize = 16f;
        private const float DropdownIconSize = 12f;
        private const float ToolbarIconSpacing = 1f;

        [SerializeField] private TreeViewState<int> _treeViewState;
        [SerializeField] private string _searchQuery = string.Empty;
        [SerializeField] private bool _autoSyncSelection;

        private CustomProjectTreeView _treeView;
        private SearchField _searchField;
        private bool _needsRefresh;
        private Rect _treeViewRect;

        internal CustomProjectTreeModel Model { get; private set; }
        internal bool AutoSyncSelection => _autoSyncSelection;

        private const string UserSettingsFileMenuPath = "Lilja/EditorEx/Custom Project Window/Save Mode/UserSettings";
        private const string EditorPrefsMenuPath = "Lilja/EditorEx/Custom Project Window/Save Mode/EditorPrefs";

        [MenuItem("Lilja/EditorEx/Custom Project Window/Open Window")]
        public static void Open()
        {
            var window = GetWindow<CustomProjectWindow>();
            window.titleContent = new GUIContent("Project (Custom)", CustomProjectViewIcons.Project);
            window.Show();
        }

        [MenuItem(UserSettingsFileMenuPath, false)]
        private static void SetSaveModeToFile()
        {
            SetSaveMode(SaveMode.UserSettingsFile);
        }

        [MenuItem(UserSettingsFileMenuPath, true)]
        private static bool SetSaveModeToFileValidate()
        {
            Menu.SetChecked(UserSettingsFileMenuPath, GetCurrentSaveMode() == SaveMode.UserSettingsFile);
            return true;
        }

        [MenuItem(EditorPrefsMenuPath, false)]
        private static void SetSaveModeToPrefs()
        {
            SetSaveMode(SaveMode.EditorPrefs);
        }

        [MenuItem(EditorPrefsMenuPath, true)]
        private static bool SetSaveModeToPrefsValidate()
        {
            Menu.SetChecked(EditorPrefsMenuPath, GetCurrentSaveMode() == SaveMode.EditorPrefs);
            return true;
        }

        private static SaveMode GetCurrentSaveMode()
        {
            var window = HasOpenInstances<CustomProjectWindow>() ? GetWindow<CustomProjectWindow>(false, null, false) : null;
            if (window != null && window.Model != null)
            {
                return window.Model.CurrentSaveMode;
            }

            var saveModePrefKey = "CustomProjectView_SaveMode_" + Application.dataPath.GetHashCode();
            return (SaveMode)EditorPrefs.GetInt(saveModePrefKey, (int)SaveMode.UserSettingsFile);
        }

        private static void SetSaveMode(SaveMode mode)
        {
            var window = HasOpenInstances<CustomProjectWindow>() ? GetWindow<CustomProjectWindow>(false, null, false) : null;
            if (window != null && window.Model != null)
            {
                window.SwitchSaveMode(mode);
            }
            else
            {
                var model = new CustomProjectTreeModel();
                model.SwitchSaveMode(mode);
            }
        }

        internal static void FocusAsset(string guid)
        {
            var window = GetWindow<CustomProjectWindow>();
            window.titleContent = new GUIContent("Project (Custom)", CustomProjectViewIcons.Project);
            window.Show();
            window.Focus();

            if (window.Model == null)
            {
                return;
            }

            CustomProjectNode node = null;
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(assetPath))
            {
                node = window.Model.FindNodeByAssetPath(assetPath);
            }

            if (node == null)
            {
                node = window.Model.FindManualAssetRefByGuid(guid);
            }

            window.ReloadAndRevealNode(node);
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("クイック追加 キー設定..."), false, CustomProjectKeyConfigWindow.Open);
            if (Model != null)
            {
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("保存先/UserSettings ファイル"), Model.CurrentSaveMode == SaveMode.UserSettingsFile, () => SwitchSaveMode(SaveMode.UserSettingsFile));
                menu.AddItem(new GUIContent("保存先/EditorPrefs (レジストリ)"), Model.CurrentSaveMode == SaveMode.EditorPrefs, () => SwitchSaveMode(SaveMode.EditorPrefs));
            }
        }

        private void SwitchSaveMode(SaveMode mode)
        {
            if (Model != null)
            {
                Model.SwitchSaveMode(mode);
                RequestRefresh();
            }
        }

        internal void ReloadAndRevealNode(CustomProjectNode node)
        {
            if (node == null)
            {
                RequestRefresh();
                return;
            }

            _treeView?.ReloadAndSelectNode(node);
        }

        private void OnEnable()
        {
            wantsMouseMove = true;

            _treeViewState ??= new TreeViewState<int>();

            Model = new CustomProjectTreeModel();
            Model.Load();
            Model.OnSaved += OnModelSaved;
            CustomProjectWindowDecorator.RefreshFromModel(Model);

            _searchField ??= new SearchField();
            _treeView = new CustomProjectTreeView(_treeViewState, Model, this);
            _searchField.downOrUpArrowKeyPressed -= _treeView.SetFocusAndEnsureSelectedItem;
            _searchField.downOrUpArrowKeyPressed += _treeView.SetFocusAndEnsureSelectedItem;
            _treeView.Reload();

            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;

            EditorApplication.focusChanged -= OnEditorFocusChanged;
            EditorApplication.focusChanged += OnEditorFocusChanged;
        }

        private void OnDisable()
        {
            Model?.FlushPendingSave();
            if (Model != null)
            {
                Model.OnSaved -= OnModelSaved;
            }

            CustomProjectWindowDecorator.Clear();
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.focusChanged -= OnEditorFocusChanged;
        }

        private void OnEditorFocusChanged(bool isFocused)
        {
            if (isFocused && Model != null)
            {
                if (Model.CheckExternalUpdate())
                {
                    RequestRefresh();
                }
            }
        }

        private void OnModelSaved()
        {
            CustomProjectWindowDecorator.RefreshFromModel(Model);
        }

        private void OnSelectionChanged()
        {
            if (ShouldClearSelectionFromHierarchySelection())
            {
                _treeView?.ClearSelectionAndPing();
                Repaint();
                return;
            }

            if (!_autoSyncSelection)
            {
                return;
            }

            if (_treeView != null && _treeView.SyncSelectionFromUnity())
            {
                Repaint();
            }
        }

        public void RequestRefresh()
        {
            _needsRefresh = true;
            Repaint();
        }

        private static bool IsHierarchyWindow(EditorWindow window)
        {
            if (window == null)
            {
                return false;
            }

            if (SceneHierarchyWindowType != null)
            {
                return SceneHierarchyWindowType.IsInstanceOfType(window);
            }

            return string.Equals(window.GetType().Name, "SceneHierarchyWindow", StringComparison.Ordinal);
        }

        private bool ShouldClearSelectionFromHierarchySelection()
        {
            if (_treeView == null || !_treeView.HasSelection())
            {
                return false;
            }

            var activeWindow = (focusedWindow != null) ? focusedWindow : mouseOverWindow;
            if (!IsHierarchyWindow(activeWindow))
            {
                return false;
            }

            var selectedObject = Selection.activeObject;
            if (selectedObject == null)
            {
                return true;
            }

            if (EditorUtility.IsPersistent(selectedObject))
            {
                return false;
            }

            return string.IsNullOrEmpty(AssetDatabase.GetAssetPath(selectedObject));
        }

        private void OnGUI()
        {
            if (_needsRefresh)
            {
                _needsRefresh = false;
                _treeView?.Reload();
            }

            DrawToolbar();
            DrawSearchBar();
            DrawTreeView();

            if (Event.current.type == EventType.MouseMove || Event.current.type == EventType.MouseLeaveWindow)
            {
                Repaint();
            }

            if (Event.current.type == EventType.MouseDown
                && _treeViewRect.width > 0
                && !_treeViewRect.Contains(Event.current.mousePosition))
            {
                _treeView?.ClearSelectionAndPing();
                Event.current.Use();
                Repaint();
            }
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            DrawRootCreateButton();

            GUILayout.FlexibleSpace();

            _autoSyncSelection = GUILayout.Toggle(_autoSyncSelection,
                new GUIContent(" Sync", CustomProjectViewIcons.Sync, "選択時に Project ビューと同期"),
                EditorStyles.toolbarButton,
                GUILayout.Width(70));

            if (GUILayout.Button(new GUIContent(
                " Expand",
                CustomProjectViewIcons.Expand,
                "すべて展開"),
                EditorStyles.toolbarButton,
                GUILayout.Width(84)))
            {
                _treeView?.ExpandAll(true);
            }

            if (GUILayout.Button(new GUIContent(
                " Collapse",
                CustomProjectViewIcons.Collapse,
                "すべて折りたたむ"),
                EditorStyles.toolbarButton,
                GUILayout.Width(90)))
            {
                _treeView?.ExpandAll(false);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawRootCreateButton()
        {
            var buttonRect = GUILayoutUtility.GetRect(
                RootCreateButtonWidth,
                EditorGUIUtility.singleLineHeight + 2f,
                EditorStyles.toolbarButton,
                GUILayout.Width(RootCreateButtonWidth));

            if (GUI.Button(buttonRect, RootCreateButtonContent, EditorStyles.toolbarButton))
            {
                ShowRootCreateMenu(buttonRect);
            }

            DrawRootCreateButtonIcons(buttonRect);
        }

        private void ShowRootCreateMenu(Rect buttonRect)
        {
            var anchorScreenPosition = GetCurrentPointerScreenPosition();

            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("New Group"), false, () => AddRootGroup(anchorScreenPosition));
            menu.AddItem(new GUIContent("Folder Pointer"), false, () => AddFolderPointer());
            menu.DropDown(new Rect(0f, buttonRect.yMax, buttonRect.width, 0f));
        }

        private static void DrawRootCreateButtonIcons(Rect buttonRect)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            var totalWidth = ToolbarIconSize + DropdownIconSize + ToolbarIconSpacing;
            var x = buttonRect.x + Mathf.Floor((buttonRect.width - totalWidth) * 0.5f);
            var y = buttonRect.y + Mathf.Floor((buttonRect.height - ToolbarIconSize) * 0.5f);

            if (CustomProjectViewIcons.AddGroup != null)
            {
                GUI.DrawTexture(new Rect(x, y, ToolbarIconSize, ToolbarIconSize), CustomProjectViewIcons.AddGroup, ScaleMode.ScaleToFit, true);
            }

            if (CustomProjectViewIcons.Dropdown != null)
            {
                var dropdownY = buttonRect.y + Mathf.Floor((buttonRect.height - DropdownIconSize) * 0.5f);
                GUI.DrawTexture(
                    new Rect(x + ToolbarIconSize + ToolbarIconSpacing, dropdownY, DropdownIconSize, DropdownIconSize),
                    CustomProjectViewIcons.Dropdown,
                    ScaleMode.ScaleToFit,
                    true);
            }
        }

        private void DrawSearchBar()
        {
            _searchField ??= new SearchField();

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            try
            {
                var newQuery = _searchField.OnToolbarGUI(_searchQuery);
                if (newQuery != _searchQuery)
                {
                    _searchQuery = newQuery;
                    _treeView?.SetSearch(_searchQuery);
                }
            }
            finally
            {
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawTreeView()
        {
            _treeViewRect = GUILayoutUtility.GetRect(
                GUIContent.none,
                GUIStyle.none,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));
            _treeView?.BeginFrame(_treeViewRect);
            if (Event.current.type == EventType.ContextClick)
            {
                _treeView?.BeginContextMenuEvent();
            }
            _treeView?.OnGUI(_treeViewRect);
            HandleExternalDrop(_treeViewRect);
            _treeView?.TryShowContextMenuAtPointer(_treeViewRect);
            _treeView?.TryClearSelectionFromEmptySpace(_treeViewRect);
        }

        private void HandleExternalDrop(Rect rect)
        {
            const string dragSourceKey = "CustomProjectViewDragSource";

            var evt = Event.current;
            if (evt.type == EventType.Used)
            {
                return;
            }

            if (!rect.Contains(evt.mousePosition))
            {
                return;
            }

            if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)
            {
                return;
            }

            if (DragAndDrop.objectReferences == null || DragAndDrop.objectReferences.Length == 0)
            {
                return;
            }

            if (Equals(DragAndDrop.GetGenericData(dragSourceKey), dragSourceKey))
            {
                return;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (var obj in DragAndDrop.objectReferences)
                {
                    var path = AssetDatabase.GetAssetPath(obj);
                    if (string.IsNullOrEmpty(path))
                    {
                        continue;
                    }

                    if (AssetDatabase.IsValidFolder(path))
                    {
                        Model.AddFolderRef(path);
                    }
                    else
                    {
                        Model.AddAssetRef(AssetDatabase.AssetPathToGUID(path));
                    }
                }
                _treeView?.Reload();
            }

            evt.Use();
        }

        internal void AddRootGroup()
        {
            AddRootGroup(GetCurrentPointerScreenPosition());
        }

        internal void AddRootGroup(Vector2 anchorScreenPosition)
        {
            ShowGroupNameDialog(null, anchorScreenPosition, "グループを追加");
        }

        internal void AddSubGroup(CustomProjectNode parent, Vector2 anchorScreenPosition)
        {
            if (parent == null)
            {
                AddRootGroup(anchorScreenPosition);
                return;
            }

            ShowGroupNameDialog(parent, anchorScreenPosition, "サブグループを追加");
        }

        private void ShowGroupNameDialog(CustomProjectNode parent, Vector2 anchorScreenPosition, string title)
        {
            PopupNameDialog.Show(title, "グループ名を入力してください", "New Group", anchorScreenPosition, position, name =>
            {
                var node = Model.AddGroup(name, parent);
                _treeView?.ReloadAndSelectNode(node);
            });
        }

        internal void AddFolderPointer(CustomProjectNode parent = null)
        {
            var selectedFolderPath = EditorUtility.OpenFolderPanel("Folder Pointer を追加", Application.dataPath, string.Empty);
            if (string.IsNullOrEmpty(selectedFolderPath))
            {
                return;
            }

            var assetPath = ToProjectFolderPath(selectedFolderPath);
            if (string.IsNullOrEmpty(assetPath) || !AssetDatabase.IsValidFolder(assetPath))
            {
                EditorUtility.DisplayDialog("エラー", "Assets フォルダ外のフォルダは追加できません。", "OK");
                return;
            }

            var node = Model.AddFolderPointer(assetPath, parent);
            if (node != null)
            {
                _treeView?.ReloadAndSelectNode(node);
            }
        }

        private static string ToProjectFolderPath(string folderPath)
        {
            var normalizedFolderPath = CustomProjectNode.NormalizeAssetPath(folderPath);
            var normalizedDataPath = CustomProjectNode.NormalizeAssetPath(Application.dataPath);
            if (string.IsNullOrEmpty(normalizedFolderPath) || string.IsNullOrEmpty(normalizedDataPath))
            {
                return string.Empty;
            }

            if (string.Equals(normalizedFolderPath, normalizedDataPath, StringComparison.OrdinalIgnoreCase))
            {
                return "Assets";
            }

            if (!normalizedFolderPath.StartsWith(normalizedDataPath + "/", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return "Assets" + normalizedFolderPath[normalizedDataPath.Length..];
        }

        private Vector2 GetCurrentPointerScreenPosition()
        {
            var evt = Event.current;
            if (evt != null)
            {
                return GUIUtility.GUIToScreenPoint(evt.mousePosition);
            }

            return new Vector2(position.x + position.width * 0.5f, position.y + 24f);
        }
    }
}
