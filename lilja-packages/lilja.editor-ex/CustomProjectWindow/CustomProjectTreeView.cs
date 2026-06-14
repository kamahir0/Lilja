using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Lilja.CustomProjectWindow
{
    internal sealed class CustomProjectViewItem : TreeViewItem<int>
    {
        public CustomProjectNode Node;
        public string AssetPath;
        public string PathSuffix;
        public bool IsMissing;

        public CustomProjectViewItem(int id, int depth, CustomProjectNode node)
            : base(id, depth, node?.Label ?? string.Empty)
        {
            Node = node;
            AssetPath = node?.ResolveAssetPath();
            PathSuffix = ResolvePathSuffix(node, AssetPath);
            IsMissing = false;
            icon = ResolveIcon(node, AssetPath, ref IsMissing);

            if (IsMissing && node != null)
            {
                displayName = node.Label + " (Missing)";
            }
        }

        private static Texture2D ResolveIcon(CustomProjectNode node, string assetPath, ref bool isMissing)
        {
            if (node == null)
            {
                return null;
            }

            if (node.Kind == ProjectNodeKind.Asset)
            {
                if (CustomProjectAssetCache.IsMissingAsset(assetPath, node.AssetGuid))
                {
                    isMissing = true;
                    return CustomProjectViewIcons.MissingAsset;
                }

                return CustomProjectAssetCache.GetCachedIcon(assetPath);
            }

            if (node.IsFolderRefRoot)
            {
                isMissing = string.IsNullOrEmpty(assetPath) || !CustomProjectAssetCache.IsValidFolder(assetPath);
                return CustomProjectViewIcons.FolderRefRoot;
            }

            if (node.IsFolderPointer)
            {
                isMissing = string.IsNullOrEmpty(assetPath) || !CustomProjectAssetCache.IsValidFolder(assetPath);
                return CustomProjectViewIcons.FolderPointer;
            }

            if (node.Kind == ProjectNodeKind.Folder)
            {
                isMissing = string.IsNullOrEmpty(assetPath) || !CustomProjectAssetCache.IsValidFolder(assetPath);
                return CustomProjectViewIcons.Folder;
            }

            return CustomProjectViewIcons.Folder;
        }

        private static string ResolvePathSuffix(CustomProjectNode node, string assetPath)
        {
            if (node == null || string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            if (node.Source != ProjectNodeSource.Manual && !node.IsFolderRefRoot && !node.IsFolderPointer)
            {
                return null;
            }

            var dir = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
            if (string.IsNullOrEmpty(dir))
            {
                return null;
            }

            var parentDir = Path.GetFileName(dir.TrimEnd('/'));
            return string.IsNullOrEmpty(parentDir) ? null : $" ({parentDir}/)";
        }
    }

    internal sealed class CustomProjectTreeView : TreeView<int>
    {
        private const string DragSourceKey = "CustomProjectViewDragSource";
        private const string DragNodesKey = "CustomProjectViewNodes";
        private static readonly GUIContent SharedMeasureContent = new();
#if UNITY_EDITOR_OSX
        private const string RevealInOsMenuLabel = "Finder で表示";
#elif UNITY_EDITOR_WIN
        private const string RevealInOsMenuLabel = "エクスプローラーで表示";
#else
        private const string RevealInOsMenuLabel = "ファイルマネージャーで表示";
#endif

        private readonly CustomProjectTreeModel _model;
        private readonly CustomProjectWindow _window;

        private readonly Dictionary<int, CustomProjectNode> _idToNode = new();
        private readonly Dictionary<string, int> _nodeIdToItemId = new(StringComparer.Ordinal);
        private readonly Dictionary<int, Rect> _idToFoldoutRect = new();
        private readonly Dictionary<int, Rect> _idToRowRect = new();
        private GUIStyle _labelStyle;
        private GUIStyle _selectedLabelStyle;
        private GUIStyle _missingLabelStyle;
        private GUIStyle _missingSelectedLabelStyle;
        private GUIStyle _pathSuffixStyle;
        private GUIStyle _selectedPathSuffixStyle;
        private string _searchQuery = string.Empty;
        private int _nextId = 1;
        private int _selectionSyncFrame = -1;
        private int _contextRenameItemId = -1;
        private int _pendingToggleItemId = -1;
        private Vector2 _pendingToggleMouseDownPosition;
        private float _lastVisibleRowBottom;
        private bool _didShowContextMenu;
        private bool _restoringExpandedState;

        private const float ButtonW = 16f;
        private const float ButtonSpacing = 1f;
        private const float ToggleDragThreshold = 4f;
        private const float IconWidth = 16f;
        private const float IconTextSpacing = 2f;
        private static readonly Color HoverRowOverlayColor = EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, 0.05f)
            : new Color(0.22f, 0.44f, 0.85f, 0.08f);

        public CustomProjectTreeView(TreeViewState<int> state, CustomProjectTreeModel model, CustomProjectWindow window)
            : base(state)
        {
            _model = model;
            _window = window;
            showBorder = true;
            showAlternatingRowBackgrounds = false;
            rowHeight = EditorGUIUtility.singleLineHeight + 2f;
        }

        private void EnsureStyles()
        {
            if (_labelStyle != null)
            {
                return;
            }

            _labelStyle = CreateLabelStyle();
            _selectedLabelStyle = CreateLabelStyle(Color.white);
            _missingLabelStyle = CreateLabelStyle(new Color(0.85f, 0.2f, 0.2f));
            _missingSelectedLabelStyle = CreateLabelStyle(new Color(1f, 0.85f, 0.85f));
            _pathSuffixStyle = CreateLabelStyle(Color.gray);
            _selectedPathSuffixStyle = CreateLabelStyle(new Color(0.8f, 0.8f, 0.8f, 0.8f));
        }

        private static GUIStyle CreateLabelStyle()
        {
            return new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
            };
        }

        private static GUIStyle CreateLabelStyle(Color textColor)
        {
            var style = CreateLabelStyle();
            style.normal.textColor = textColor;
            return style;
        }

        private static GUIContent GetMeasureContent(string text)
        {
            SharedMeasureContent.text = text;
            SharedMeasureContent.image = null;
            SharedMeasureContent.tooltip = null;
            return SharedMeasureContent;
        }

        public void SetSearch(string query)
        {
            var previousIds = GetSelection()
                .Select(GetNodeForId)
                .Where(n => n != null)
                .Select(n => n.Id)
                .ToList();

            _searchQuery = query ?? string.Empty;
            Reload();

            if (previousIds.Count == 0)
            {
                return;
            }

            var restored = previousIds
                .Select(id => _model.FindNodeById(id))
                .Where(n => n != null)
                .Select(GetIdForNode)
                .Where(id => id >= 0)
                .ToList();

            if (restored.Count > 0)
            {
                SetSelection(restored, TreeViewSelectionOptions.RevealAndFrame);
            }
        }

        public void ClearSelectionAndPing()
        {
            SetSelection(new List<int>(), TreeViewSelectionOptions.FireSelectionChanged);
        }

        public void ReloadAndSelectNode(CustomProjectNode node)
        {
            Reload();
            var id = GetIdForNode(node);
            if (id >= 0)
            {
                SetSelection(new[] { id }, TreeViewSelectionOptions.RevealAndFrame);
            }
        }

        public new bool HasSelection()
        {
            return GetSelection().Count > 0;
        }

        public void BeginFrame(Rect treeRect)
        {
            _lastVisibleRowBottom = treeRect.yMin;
        }

        public void BeginContextMenuEvent()
        {
            _didShowContextMenu = false;
        }

        public bool TryClearSelectionFromEmptySpace(Rect treeRect)
        {
            var evt = Event.current;
            if (evt == null || evt.type != EventType.MouseDown || evt.button != 0)
            {
                return false;
            }

            if (!treeRect.Contains(evt.mousePosition))
            {
                return false;
            }

            if (evt.mousePosition.y <= _lastVisibleRowBottom)
            {
                return false;
            }

            if (GetSelection().Count == 0)
            {
                return false;
            }

            ClearSelectionAndPing();
            evt.Use();
            return true;
        }

        protected override TreeViewItem<int> BuildRoot()
        {
            _idToNode.Clear();
            _nodeIdToItemId.Clear();
            _idToFoldoutRect.Clear();
            _idToRowRect.Clear();
            _nextId = 1;

            var root = new TreeViewItem<int>(-1, -1, "root");

            if (string.IsNullOrEmpty(_searchQuery))
            {
                BuildTree(_model.Roots, root);
            }
            else
            {
                foreach (var node in _model.Search(_searchQuery))
                {
                    root.AddChild(CreateItem(node, 0));
                }
            }

            if (!root.hasChildren)
            {
                root.AddChild(new TreeViewItem<int>(0, 0, string.Empty));
            }

            SetupDepthsFromParentsAndChildren(root);

            if (string.IsNullOrEmpty(_searchQuery))
            {
                ApplyExpandedState(_model.Roots);
            }

            return root;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            var item = args.item as CustomProjectViewItem;
            if (item == null || item.Node == null)
            {
                if (_model.IsEmpty && string.IsNullOrEmpty(_searchQuery))
                {
                    GUI.Label(args.rowRect, "まだ項目がありません。左上の追加メニューから項目を追加してください。", EditorStyles.centeredGreyMiniLabel);
                }

                return;
            }

            EnsureStyles();
            DrawHoverBackground(args);

            var oldColor = GUI.color;
            if (item.IsMissing)
            {
                GUI.color = Color.red;
            }

            _lastVisibleRowBottom = Mathf.Max(_lastVisibleRowBottom, args.rowRect.yMax);
            _idToFoldoutRect[item.id] = CreateFoldoutRect(args.rowRect, item);
            _idToRowRect[item.id] = args.rowRect;
            HandlePendingToggleInteraction(args, item);
            var isRenamingItem = _contextRenameItemId == item.id;
            if (isRenamingItem)
            {
                base.RowGUI(args);
            }
            else
            {
                DrawRowContent(args, item);
            }

            GUI.color = oldColor;

            if (Event.current.type == EventType.Repaint)
            {
                DrawPathSuffix(args, item);
            }

            if (args.selected || args.rowRect.Contains(Event.current.mousePosition))
            {
                var width = CalcButtonAreaWidth(item.Node);
                if (width > 0f)
                {
                    var rect = new Rect(args.rowRect.xMax - width, args.rowRect.y, width, args.rowRect.height);
                    DrawInlineButtons(rect, item.Node, item.id);
                }
            }
        }

        private static void DrawHoverBackground(RowGUIArgs args)
        {
            var evt = Event.current;
            if (evt == null || evt.type != EventType.Repaint)
            {
                return;
            }

            if (args.selected || !args.rowRect.Contains(evt.mousePosition))
            {
                return;
            }

            EditorGUI.DrawRect(args.rowRect, HoverRowOverlayColor);
        }

        protected override void ContextClickedItem(int id)
        {
            var selectedIds = GetSelection();
            if (selectedIds.Count > 1)
            {
                var nodes = selectedIds.Select(GetNodeForId).Where(n => n != null && n.CanRemoveFromList).ToList();
                if (nodes.Count > 0)
                {
                    ShowMultiSelectionContextMenu(nodes);
                    _didShowContextMenu = true;
                }
                return;
            }

            var resolvedId = id;
            var node = GetNodeForId(resolvedId);
            if (node == null)
            {
                var hitId = GetItemIdAtPoint(Event.current.mousePosition);
                if (hitId >= 0)
                {
                    resolvedId = hitId;
                    node = GetNodeForId(resolvedId);
                }
            }

            if (node != null)
            {
                ShowContextMenu(node, resolvedId);
                _didShowContextMenu = true;
                return;
            }
        }

        public bool TryShowContextMenuAtPointer(Rect treeRect)
        {
            var evt = Event.current;
            if (evt == null || evt.type != EventType.ContextClick)
            {
                return false;
            }

            if (!treeRect.Contains(evt.mousePosition))
            {
                return false;
            }

            if (_didShowContextMenu)
            {
                return false;
            }

            if (GetItemIdAtPoint(evt.mousePosition) >= 0)
            {
                return false;
            }

            if (evt.mousePosition.y <= _lastVisibleRowBottom)
            {
                return false;
            }

            ShowEmptySpaceContextMenu();
            _didShowContextMenu = true;
            evt.Use();
            return true;
        }

        private void ShowEmptySpaceContextMenu()
        {
            var menu = new GenericMenu();
            var anchorScreenPosition = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
            menu.AddItem(new GUIContent("グループを作成"), false, () => _window.AddRootGroup(anchorScreenPosition));
            menu.ShowAsContext();
        }

        private int GetItemIdAtPoint(Vector2 point)
        {
            foreach (var kv in _idToRowRect)
            {
                if (kv.Value.Contains(point))
                {
                    return kv.Key;
                }
            }

            return -1;
        }

        protected override void SingleClickedItem(int id)
        {
        }

        protected override bool CanRename(TreeViewItem<int> item)
        {
            var node = GetNodeForId(item.id);
            return node != null && node.CanRenameInTree && item.id == _contextRenameItemId;
        }

        protected override void RenameEnded(RenameEndedArgs args)
        {
            _contextRenameItemId = -1;

            if (!args.acceptedRename)
            {
                return;
            }

            var node = GetNodeForId(args.itemID);
            if (node == null || !node.CanRenameInTree)
            {
                return;
            }

            if (_model.RenameNode(node, args.newName))
            {
                Reload();
            }
        }

        protected override void DoubleClickedItem(int id)
        {
            _pendingToggleItemId = -1;

            var node = GetNodeForId(id);
            if (node == null)
            {
                return;
            }

            if (node.IsFolderPointer)
            {
                SelectInProject(node);
                return;
            }

            if (node.CanOpenAsset)
            {
                OpenAsset(node);
            }
        }

        protected override void SelectionChanged(IList<int> selectedIds)
        {
            if (!_window.AutoSyncSelection || selectedIds == null || selectedIds.Count == 0)
            {
                return;
            }

            var node = GetNodeForId(selectedIds[0]);
            var assetPath = node?.ResolveAssetPath();
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            var currentSelectionPath = CustomProjectNode.NormalizeAssetPath(AssetDatabase.GetAssetPath(Selection.activeObject));
            if (currentSelectionPath == CustomProjectNode.NormalizeAssetPath(assetPath))
            {
                return;
            }

            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (obj == null)
            {
                return;
            }

            _selectionSyncFrame = Time.frameCount;
            Selection.activeObject = obj;
        }

        protected override void ExpandedStateChanged()
        {
            if (_restoringExpandedState)
            {
                return;
            }

            SyncExpandedState(_model.Roots);
            var needsReload = false;
            foreach (var itemId in GetExpanded())
            {
                var node = GetNodeForId(itemId);
                if (node == null || (!node.IsLazySyncedFolder && !node.IsFolderRefRoot) || node.SyncedChildrenLoaded)
                {
                    continue;
                }
                _model.EnsureSyncedFolderChildrenLoaded(node);
                needsReload = true;
            }
            _model.SaveDeferred();
            if (needsReload)
            {
                _window.RequestRefresh();
            }
        }

        protected override bool CanMultiSelect(TreeViewItem<int> item) => true;

        protected override bool CanStartDrag(CanStartDragArgs args)
        {
            var nodes = args.draggedItemIDs
                .Select(GetNodeForId)
                .Where(n => n != null)
                .ToList();

            if (nodes.Count == 0)
            {
                return false;
            }

            if (nodes.All(n => n.CanMoveInTree))
            {
                return true;
            }

            return GetDragObjects(nodes).Length > 0;
        }

        protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
        {
            DragAndDrop.PrepareStartDrag();

            var nodes = args.draggedItemIDs
                .Select(GetNodeForId)
                .Where(n => n != null)
                .ToList();

            DragAndDrop.SetGenericData(DragSourceKey, DragSourceKey);

            if (nodes.All(n => n.CanMoveInTree))
            {
                DragAndDrop.SetGenericData(DragNodesKey, nodes);
            }

            var objects = GetDragObjects(nodes);

            if (objects.Length > 0)
            {
                DragAndDrop.objectReferences = objects;
            }

            DragAndDrop.StartDrag("CustomProjectView");
        }

        protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
        {
            var targetParent = (args.parentItem as CustomProjectViewItem)?.Node;
            if (targetParent != null && !targetParent.CanAddChildren)
            {
                return DragAndDropVisualMode.Rejected;
            }

            var isCustomProjectDrag = Equals(DragAndDrop.GetGenericData(DragSourceKey), DragSourceKey);
            if (DragAndDrop.GetGenericData(DragNodesKey) is List<CustomProjectNode> internalNodes)
            {
                if (args.performDrop)
                {
                    foreach (var source in internalNodes)
                    {
                        _model.MoveNode(source, targetParent);
                    }

                    Reload();
                }
                return DragAndDropVisualMode.Move;
            }

            if (isCustomProjectDrag)
            {
                return DragAndDropVisualMode.Rejected;
            }

            if (DragAndDrop.objectReferences == null || DragAndDrop.objectReferences.Length == 0)
            {
                return DragAndDropVisualMode.None;
            }

            if (args.performDrop)
            {
                foreach (var obj in DragAndDrop.objectReferences)
                {
                    AddObjectReference(obj, targetParent);
                }

                Reload();
            }

            return DragAndDropVisualMode.Copy;
        }

        private UnityEngine.Object[] GetDragObjects(IEnumerable<CustomProjectNode> nodes)
        {
            return nodes
                .Select(n => n.ResolveAssetPath())
                .Where(p => !string.IsNullOrEmpty(p))
                .Select(p => AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(p))
                .Where(o => o != null)
                .ToArray();
        }

        protected override void KeyEvent()
        {
            if (Event.current.type != EventType.KeyDown)
            {
                return;
            }

            if (Event.current.keyCode != KeyCode.Delete && Event.current.keyCode != KeyCode.Backspace)
            {
                return;
            }

            var nodes = GetSelection()
                .Select(GetNodeForId)
                .Where(n => n != null && n.CanRemoveFromList)
                .ToList();

            if (nodes.Count == 0)
            {
                return;
            }

            Event.current.Use();

            if (nodes.Count == 1)
            {
                RemoveWithConfirm(nodes[0]);
            }
            else
            {
                RemoveMultipleWithConfirm(nodes);
            }
        }

        public void ExpandAll(bool expand)
        {
            if (_model.Roots == null || _model.Roots.Count == 0)
            {
                return;
            }

            foreach (var root in _model.Roots)
            {
                SetExpandedRecursiveOnModel(root, expand);
            }

            _model.SaveDeferred();
            Reload();
        }

        public bool SyncSelectionFromUnity()
        {
            if (Mathf.Abs(Time.frameCount - _selectionSyncFrame) <= 1)
            {
                return false;
            }

            var obj = Selection.activeObject;
            if (obj == null)
            {
                return false;
            }

            var assetPath = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            var best = _model.FindNodeByAssetPath(assetPath);
            if (best == null)
            {
                var guid = AssetDatabase.AssetPathToGUID(assetPath);
                best = _model.FindManualAssetRefByGuid(guid);
            }

            if (best == null)
            {
                return false;
            }

            var id = GetIdForNode(best);
            if (id < 0)
            {
                return false;
            }

            var selectedIds = GetSelection();
            if (selectedIds.Count == 1 && selectedIds[0] == id)
            {
                return false;
            }

            SetSelection(new[] { id }, TreeViewSelectionOptions.RevealAndFrame);
            return true;
        }

        private void BuildTree(List<CustomProjectNode> nodes, TreeViewItem<int> parent)
        {
            foreach (var node in nodes)
            {
                if ((node.IsLazySyncedFolder || node.IsFolderRefRoot) && node.IsExpanded && !node.SyncedChildrenLoaded)
                {
                    _model.EnsureSyncedFolderChildrenLoaded(node);
                }
                var item = CreateItem(node, parent.depth + 1);
                parent.AddChild(item);
                if (node.IsContainer && node.Children != null && node.Children.Count > 0)
                {
                    BuildTree(node.Children, item);
                }
            }
        }

        private CustomProjectViewItem CreateItem(CustomProjectNode node, int depth)
        {
            var id = _nextId++;
            _idToNode[id] = node;
            if (!string.IsNullOrEmpty(node.Id))
            {
                _nodeIdToItemId[node.Id] = id;
            }

            return new CustomProjectViewItem(id, depth, node);
        }

        private void ApplyExpandedState(List<CustomProjectNode> nodes)
        {
            if (nodes == null || nodes.Count == 0)
            {
                return;
            }

            _restoringExpandedState = true;
            try
            {
                ApplyExpandedStateRecursive(nodes);
            }
            finally
            {
                _restoringExpandedState = false;
            }
        }

        private void ApplyExpandedStateRecursive(List<CustomProjectNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (node.IsContainer)
                {
                    var id = GetIdForNode(node);
                    if (id >= 0)
                    {
                        SetExpanded(id, node.IsExpanded);
                    }
                }

                if (node.Children != null && node.Children.Count > 0)
                {
                    ApplyExpandedStateRecursive(node.Children);
                }
            }
        }

        private int GetIdForNode(CustomProjectNode node)
        {
            if (node == null || string.IsNullOrEmpty(node.Id))
            {
                return -1;
            }

            return _nodeIdToItemId.TryGetValue(node.Id, out var id) ? id : -1;
        }

        private CustomProjectNode GetNodeForId(int id)
        {
            _idToNode.TryGetValue(id, out var node);
            return node;
        }

        private void HandlePendingToggleInteraction(RowGUIArgs args, CustomProjectViewItem item)
        {
            var evt = Event.current;
            if (evt == null || evt.button != 0)
            {
                return;
            }

            if (evt.type == EventType.MouseDown)
            {
                if (!args.rowRect.Contains(evt.mousePosition))
                {
                    return;
                }

                if (TryToggleOnFastRepeatClick(args, item))
                {
                    return;
                }

                if (!CanPreparePendingToggle(args, item))
                {
                    _pendingToggleItemId = -1;
                    return;
                }

                _pendingToggleItemId = item.id;
                _pendingToggleMouseDownPosition = evt.mousePosition;
                return;
            }

            if (_pendingToggleItemId != item.id)
            {
                return;
            }

            if (evt.type == EventType.MouseDrag)
            {
                if ((evt.mousePosition - _pendingToggleMouseDownPosition).sqrMagnitude > ToggleDragThreshold * ToggleDragThreshold)
                {
                    _pendingToggleItemId = -1;
                }

                return;
            }

            if (evt.type != EventType.MouseUp)
            {
                return;
            }

            _pendingToggleItemId = -1;

            if (!CanCommitPendingToggle(args, item))
            {
                return;
            }

            ToggleExpandedState(item.id, item.Node);
        }

        private bool TryToggleOnFastRepeatClick(RowGUIArgs args, CustomProjectViewItem item)
        {
            var evt = Event.current;
            if (evt == null || evt.clickCount < 2)
            {
                return false;
            }

            if (!CanPreparePendingToggle(args, item))
            {
                return false;
            }

            _pendingToggleItemId = -1;
            ToggleExpandedState(item.id, item.Node);
            return true;
        }

        private bool CanPreparePendingToggle(RowGUIArgs args, CustomProjectViewItem item)
        {
            if (item.Node == null || !item.Node.IsContainer)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(_searchQuery))
            {
                return false;
            }

            var evt = Event.current;
            if (evt == null)
            {
                return false;
            }

            if (evt.shift || evt.control || evt.command)
            {
                return false;
            }

            if (!args.selected)
            {
                return false;
            }

            var selectedIds = GetSelection();
            if (selectedIds.Count != 1 || selectedIds[0] != item.id)
            {
                return false;
            }

            if (IsPointerOnFoldout(item.id) || IsPointerOnInlineButtons(args.rowRect, item.Node))
            {
                return false;
            }

            return true;
        }

        private bool CanCommitPendingToggle(RowGUIArgs args, CustomProjectViewItem item)
        {
            if (item.Node == null || !item.Node.IsContainer)
            {
                return false;
            }

            if (!args.rowRect.Contains(Event.current.mousePosition))
            {
                return false;
            }

            if (IsPointerOnFoldout(item.id) || IsPointerOnInlineButtons(args.rowRect, item.Node))
            {
                return false;
            }

            return true;
        }

        private Rect CreateFoldoutRect(Rect rowRect, TreeViewItem<int> item)
        {
            var foldoutX = rowRect.x + GetFoldoutIndent(item);
            var contentX = rowRect.x + GetContentIndent(item);
            var foldoutWidth = Mathf.Max(14f, contentX - foldoutX);
            return new Rect(foldoutX, rowRect.y, foldoutWidth, rowRect.height);
        }

        private bool IsPointerOnFoldout(int itemId)
        {
            var evt = Event.current;
            if (evt == null)
            {
                return false;
            }

            if (!_idToFoldoutRect.TryGetValue(itemId, out var foldoutRect))
            {
                return false;
            }

            return foldoutRect.Contains(evt.mousePosition);
        }

        private bool IsPointerOnInlineButtons(Rect rowRect, CustomProjectNode node)
        {
            var evt = Event.current;
            if (evt == null)
            {
                return false;
            }

            var width = CalcButtonAreaWidth(node);
            if (width <= 0f)
            {
                return false;
            }

            var buttonRect = new Rect(rowRect.xMax - width, rowRect.y, width, rowRect.height);
            return buttonRect.Contains(evt.mousePosition);
        }

        private void ToggleExpandedState(int itemId, CustomProjectNode node)
        {
            SetExpandedState(itemId, node, !IsExpanded(itemId));
        }

        private void DrawPathSuffix(RowGUIArgs args, CustomProjectViewItem item)
        {
            if (string.IsNullOrEmpty(item.PathSuffix))
            {
                return;
            }

            var size = _labelStyle.CalcSize(GetMeasureContent(item.displayName));
            var labelRect = GetCenteredLabelRect(args, item);
            var xOffset = labelRect.x - args.rowRect.x + size.x;
            var rect = new Rect(args.rowRect.x + xOffset, args.rowRect.y, args.rowRect.xMax - (args.rowRect.x + xOffset), args.rowRect.height);
            GUI.Label(rect, item.PathSuffix, args.selected ? _selectedPathSuffixStyle : _pathSuffixStyle);
        }

        private void DrawRowContent(RowGUIArgs args, CustomProjectViewItem item)
        {
            DrawFoldout(item);
            DrawIcon(args, item);
            DrawCenteredLabel(args, item);
        }

        private void DrawFoldout(CustomProjectViewItem item)
        {
            if (!item.Node.IsContainer)
            {
                return;
            }

            var foldoutRect = _idToFoldoutRect[item.id];
            var expanded = IsExpanded(item.id);
            var newExpanded = EditorGUI.Foldout(foldoutRect, expanded, GUIContent.none, false);
            if (newExpanded != expanded)
            {
                SetExpandedState(item.id, item.Node, newExpanded);
            }
        }

        private void DrawIcon(RowGUIArgs args, CustomProjectViewItem item)
        {
            if (item.icon == null)
            {
                return;
            }

            var iconRect = GetIconRect(args, item);
            GUI.DrawTexture(iconRect, item.icon, ScaleMode.ScaleToFit, true);
        }

        private void DrawCenteredLabel(RowGUIArgs args, CustomProjectViewItem item)
        {
            var labelRect = GetCenteredLabelRect(args, item);
            if (labelRect.width <= 0f)
            {
                return;
            }

            GUI.Label(labelRect, item.displayName, GetLabelStyle(item.IsMissing, args.selected));
        }

        private GUIStyle GetLabelStyle(bool isMissing, bool isSelected)
        {
            if (isMissing)
            {
                return isSelected ? _missingSelectedLabelStyle : _missingLabelStyle;
            }

            return isSelected ? _selectedLabelStyle : _labelStyle;
        }

        private Rect GetCenteredLabelRect(RowGUIArgs args, CustomProjectViewItem item)
        {
            var x = args.rowRect.x + GetContentIndent(item) + IconWidth + IconTextSpacing;
            var width = args.rowRect.xMax - x - CalcButtonAreaWidth(item.Node) - 6f;
            if (width <= 0f)
            {
                return Rect.zero;
            }

            return new Rect(x, args.rowRect.y, width, args.rowRect.height);
        }

        private Rect GetIconRect(RowGUIArgs args, CustomProjectViewItem item)
        {
            var x = args.rowRect.x + GetContentIndent(item);
            var y = args.rowRect.y + (args.rowRect.height - IconWidth) * 0.5f;
            return new Rect(x, y, IconWidth, IconWidth);
        }

        private void SetExpandedState(int itemId, CustomProjectNode node, bool expanded)
        {
            _model.SetExpanded(node, expanded);

            _restoringExpandedState = true;
            try
            {
                SetExpanded(itemId, expanded);
            }
            finally
            {
                _restoringExpandedState = false;
            }

            if (expanded && node != null && (node.IsLazySyncedFolder || node.IsFolderRefRoot) && !node.SyncedChildrenLoaded)
            {
                _model.EnsureSyncedFolderChildrenLoaded(node);
                _window.RequestRefresh();
            }

            _model.SaveDeferred();
        }

        private float CalcButtonAreaWidth(CustomProjectNode node)
        {
            var count = 0;

            if (node.IsManualGroup)
            {
                count += 2;
            }
            else if (node.IsFolderRefRoot || (node.Kind == ProjectNodeKind.Folder && node.IsSynced))
            {
                count += 2;
            }

            if (node.CanSelectInProject)
            {
                count += 1;
            }

            if (node.CanRemoveFromList)
            {
                count += 1;
            }

            if (count == 0)
            {
                return 0f;
            }

            return count * (ButtonW + ButtonSpacing) + 4f;
        }

        private void DrawInlineButtons(Rect rect, CustomProjectNode node, int itemId)
        {
            var x = rect.x + 2f;
            var y = rect.y + (rect.height - ButtonW) * 0.5f;
            var style = EditorStyles.iconButton;

            if (node.IsManualGroup)
            {
                if (GUI.Button(new Rect(x, y, ButtonW, ButtonW),
                    new GUIContent(CustomProjectViewIcons.Expand, "再帰的に展開"), style))
                {
                    ExpandRecursive(itemId, true);
                }
                x += ButtonW + ButtonSpacing;

                if (GUI.Button(new Rect(x, y, ButtonW, ButtonW),
                    new GUIContent(CustomProjectViewIcons.Collapse, "再帰的に折りたたむ"), style))
                {
                    ExpandRecursive(itemId, false);
                }
                x += ButtonW + ButtonSpacing;
            }
            else if (node.IsFolderRefRoot || (node.Kind == ProjectNodeKind.Folder && node.IsSynced))
            {
                if (GUI.Button(new Rect(x, y, ButtonW, ButtonW),
                    new GUIContent(CustomProjectViewIcons.Expand, "再帰的に展開"), style))
                {
                    ExpandRecursive(itemId, true);
                }
                x += ButtonW + ButtonSpacing;

                if (GUI.Button(new Rect(x, y, ButtonW, ButtonW),
                    new GUIContent(CustomProjectViewIcons.Collapse, "再帰的に折りたたむ"), style))
                {
                    ExpandRecursive(itemId, false);
                }
                x += ButtonW + ButtonSpacing;
            }

            if (node.CanSelectInProject)
            {
                if (GUI.Button(new Rect(x, y, ButtonW, ButtonW),
                    new GUIContent(CustomProjectViewIcons.Ping, "Project で選択"), style))
                {
                    SelectInProject(node);
                }
                x += ButtonW + ButtonSpacing;
            }

            if (node.CanRemoveFromList)
            {
                if (GUI.Button(new Rect(x, y, ButtonW, ButtonW),
                    new GUIContent(CustomProjectViewIcons.Remove, "取り除く"), style))
                {
                    RemoveWithConfirm(node);
                }
            }
        }

        private void ShowMultiSelectionContextMenu(List<CustomProjectNode> nodes)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent($"選択した {nodes.Count} 項目を取り除く"), false, () => RemoveMultipleWithConfirm(nodes));
            menu.ShowAsContext();
        }

        private void ShowContextMenu(CustomProjectNode node, int itemId)
        {
            var menu = new GenericMenu();
            var anchorScreenPosition = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);

            if (node.IsManualGroup)
            {
                menu.AddItem(new GUIContent("サブグループを追加"), false, () => AddSubGroup(node, anchorScreenPosition));
                menu.AddItem(new GUIContent("フォルダポインタを作成"), false, () => _window.AddFolderPointer(node));
                menu.AddItem(new GUIContent("項目を追加..."), false, () => AddAssetToGroup(node));
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("グループ名を変更"), false, () => BeginContextRename(itemId));
                menu.AddItem(new GUIContent("取り除く"), false, () => RemoveWithConfirm(node));
                menu.ShowAsContext();
                return;
            }

            if (node.IsFolderRefRoot)
            {
                AddPathActions(menu, node);
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("グループに変換"), false, () =>
                {
                    _model.ConvertFolderRefToSnapshotGroup(node);
                    Reload();
                });
                menu.AddItem(new GUIContent("取り除く"), false, () => RemoveWithConfirm(node));
                menu.ShowAsContext();
                return;
            }

            if (node.IsFolderPointer)
            {
                AddPathActions(menu, node);
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("取り除く"), false, () => RemoveWithConfirm(node));
                menu.ShowAsContext();
                return;
            }

            if (node.Kind == ProjectNodeKind.Folder && node.IsSynced)
            {
                AddPathActions(menu, node);
                menu.ShowAsContext();
                return;
            }

            if (node.Kind == ProjectNodeKind.Asset)
            {
                menu.AddItem(new GUIContent("開く"), false, () => OpenAsset(node));
                AddPathActions(menu, node);

                if (node.CanDeleteOnDisk)
                {
                    menu.AddSeparator(string.Empty);
                    menu.AddItem(new GUIContent("名前を変更"), false, () => BeginContextRename(itemId));
                    menu.AddItem(new GUIContent("削除"), false, () => DeleteAssetOnDisk(node));
                }

                if (node.CanRemoveFromList)
                {
                    menu.AddSeparator(string.Empty);
                    menu.AddItem(new GUIContent("取り除く"), false, () => RemoveWithConfirm(node));
                }

                menu.ShowAsContext();
            }
        }

        private void BeginContextRename(int itemId)
        {
            var item = FindItem(itemId, rootItem);
            if (item == null)
            {
                return;
            }

            var node = GetNodeForId(itemId);
            if (node == null || !node.CanRenameInTree)
            {
                return;
            }

            _contextRenameItemId = itemId;
            SetSelection(new[] { itemId }, TreeViewSelectionOptions.RevealAndFrame);
            BeginRename(item);
        }

        private void AddPathActions(GenericMenu menu, CustomProjectNode node)
        {
            if (node.CanRevealInFinder)
            {
                menu.AddItem(new GUIContent(RevealInOsMenuLabel), false, () => RevealInFinder(node));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent(RevealInOsMenuLabel));
            }

            if (node.CanCopyPath)
            {
                menu.AddItem(new GUIContent("パスのコピー"), false, () => CopyPath(node));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("パスのコピー"));
            }
        }

        private void ExpandRecursive(int rootItemId, bool expand)
        {
            var node = GetNodeForId(rootItemId);
            if (node == null)
            {
                return;
            }

            SetExpandedRecursiveOnModel(node, expand);
            _model.SaveDeferred();
            _window.RequestRefresh();
        }

        private void SetExpandedRecursiveOnModel(CustomProjectNode node, bool expand)
        {
            if (node == null)
            {
                return;
            }
            if (node.IsContainer)
            {
                node.IsExpanded = expand;
                if (expand && (node.IsFolderRefRoot || node.IsLazySyncedFolder))
                {
                    _model.EnsureSyncedFolderChildrenLoaded(node);
                }
            }

            if (node.Children == null || node.Children.Count == 0)
            {
                return;
            }

            foreach (var child in node.Children)
            {
                SetExpandedRecursiveOnModel(child, expand);
            }
        }

        private void SyncExpandedState(List<CustomProjectNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (node.IsContainer)
                {
                    var id = GetIdForNode(node);
                    if (id >= 0)
                    {
                        node.IsExpanded = IsExpanded(id);
                    }
                }

                SyncExpandedState(node.Children);
            }
        }

        private void AddSubGroup(CustomProjectNode parent, Vector2 anchorScreenPosition)
        {
            _window.AddSubGroup(parent, anchorScreenPosition);
        }

        private void AddAssetToGroup(CustomProjectNode parent)
        {
            var path = EditorUtility.OpenFilePanel("項目を追加", "Assets", string.Empty);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            if (path.StartsWith(Application.dataPath, StringComparison.OrdinalIgnoreCase))
            {
                path = "Assets" + path[Application.dataPath.Length..];
            }

            var guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
            {
                EditorUtility.DisplayDialog("エラー", "Assets フォルダ外のファイルは追加できません。", "OK");
                return;
            }

            _model.AddAssetRef(guid, parent);
            Reload();
        }

        private void AddObjectReference(UnityEngine.Object obj, CustomProjectNode parent)
        {
            if (obj == null)
            {
                return;
            }

            var path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            if (AssetDatabase.IsValidFolder(path))
            {
                _model.AddFolderRef(path, parent);
            }
            else
            {
                _model.AddAssetRef(AssetDatabase.AssetPathToGUID(path), parent);
            }
        }

        private void RemoveWithConfirm(CustomProjectNode node)
        {
            if (!node.CanRemoveFromList)
            {
                return;
            }

            _model.Remove(node);
            Reload();
        }

        private void RemoveMultipleWithConfirm(List<CustomProjectNode> nodes)
        {
            if (nodes == null || nodes.Count == 0)
            {
                return;
            }

            foreach (var node in nodes)
            {
                _model.Remove(node);
            }

            Reload();
        }

        private void OpenAsset(CustomProjectNode node)
        {
            if (!node.CanOpenAsset)
            {
                return;
            }

            var path = node.ResolveAssetPath();
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (obj != null)
            {
                AssetDatabase.OpenAsset(obj);
            }
        }

        private void SelectInProject(CustomProjectNode node)
        {
            var path = node.ResolveAssetPath();
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (obj == null)
            {
                return;
            }

            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
            // Projectウィンドウがタブなどで裏に隠れていた場合は前面に出す
            EditorApplication.ExecuteMenuItem("Window/General/Project");
        }

        private void RevealInFinder(CustomProjectNode node)
        {
            var path = node.ResolveAssetPath();
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var abs = Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
            EditorUtility.RevealInFinder(abs);
        }

        private void CopyPath(CustomProjectNode node)
        {
            var path = node.ResolveAssetPath();
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            GUIUtility.systemCopyBuffer = Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
        }

        private void DeleteAssetOnDisk(CustomProjectNode node)
        {
            var path = node.ResolveAssetPath();
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            if (!EditorUtility.DisplayDialog("確認", $"\"{node.Label}\" を削除しますか？\nこの操作は取り消せません。", "削除", "キャンセル"))
            {
                return;
            }

            AssetDatabase.DeleteAsset(path);
        }
    }
}
