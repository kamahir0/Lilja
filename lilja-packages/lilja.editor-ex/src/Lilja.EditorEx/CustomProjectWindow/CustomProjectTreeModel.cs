using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Lilja.CustomProjectWindow
{
    internal sealed class CustomProjectTreeModel
    {
        private const string PrefKeyPrefix = "CustomProjectView_";
        private SerializableModel _model = new();
        private readonly List<CustomProjectNode> _roots = new();
        private readonly Dictionary<string, CustomProjectNode> _nodeById = new(StringComparer.Ordinal);
        private readonly Dictionary<string, CustomProjectNode> _nodeByAssetPath = new(StringComparer.Ordinal);
        private readonly Dictionary<string, CustomProjectNode> _manualAssetRefByGuid = new(StringComparer.Ordinal);
        private bool _lookupCacheDirty = true;
        private bool _saveDeferredQueued;
        private SaveMode _saveMode = SaveMode.UserSettingsFile;
        private ISettingsStorage _storage;
        private DateTime _lastLoadedFileTimeUtc = DateTime.MinValue;

        internal event Action OnSaved;

        public List<CustomProjectNode> Roots => _roots;
        public bool IsEmpty => _roots.Count == 0;

        private string PrefKey => PrefKeyPrefix + Application.dataPath.GetHashCode();
        private string SaveModePrefKey => "CustomProjectView_SaveMode_" + Application.dataPath.GetHashCode();
        private string SettingsFilePath => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "UserSettings", "CustomProjectWindowSettings.json");

        public SaveMode CurrentSaveMode
        {
            get => _saveMode;
            set => SwitchSaveMode(value);
        }

        private void InitializeStorage()
        {
            _saveMode = (SaveMode)EditorPrefs.GetInt(SaveModePrefKey, (int)SaveMode.UserSettingsFile);
            _storage = CreateStorage(_saveMode);
        }

        private ISettingsStorage CreateStorage(SaveMode mode)
        {
            switch (mode)
            {
                case SaveMode.UserSettingsFile:
                    return new UserSettingsFileStorage(SettingsFilePath);
                case SaveMode.EditorPrefs:
                    return new EditorPrefsStorage(PrefKey);
                default:
                    return new UserSettingsFileStorage(SettingsFilePath);
            }
        }

        public void SwitchSaveMode(SaveMode newMode)
        {
            if (_storage == null)
            {
                InitializeStorage();
            }

            if (_saveMode == newMode)
            {
                return;
            }

            // 現在の状態を保存
            Save();

            var newStorage = CreateStorage(newMode);

            // 移行先に設定データが存在しない場合は、現在のデータを移行する
            if (!newStorage.Exists())
            {
                SortNodes(_roots);
                var persistentNodes = ClonePersistentNodes(_roots);
                var flatNodes = new List<SerializableNode>();
                foreach (var root in persistentNodes)
                {
                    FlattenNode(root, null, flatNodes);
                }
                var tempModel = new SerializableModel
                {
                    Version = 2,
                    Nodes = flatNodes,
                };
                var json = JsonUtility.ToJson(tempModel, newMode == SaveMode.UserSettingsFile);
                newStorage.Save(json);
            }

            _saveMode = newMode;
            _storage = newStorage;
            EditorPrefs.SetInt(SaveModePrefKey, (int)_saveMode);

            // 新しいストレージからロードし直す
            Load();
        }

        public void Load()
        {
            if (_storage == null)
            {
                InitializeStorage();
            }

            _roots.Clear();
            _model = new SerializableModel();
            MarkLookupCacheDirty();

            var loadedRoots = new List<CustomProjectNode>();
            if (_storage.Exists())
            {
                try
                {
                    var json = _storage.Load();
                    if (!string.IsNullOrEmpty(json))
                    {
                        _model = JsonUtility.FromJson<SerializableModel>(json) ?? new SerializableModel();
                        loadedRoots = ReconstructTree(_model.Nodes);
                    }
                    _lastLoadedFileTimeUtc = _storage.GetLastWriteTimeUtc();
                }
                catch
                {
                    _model = new SerializableModel();
                }
            }

            _roots.AddRange(loadedRoots);

            CustomProjectAssetCache.InvalidateAll();
            SanitizeTree(_roots);
            SyncAllFolderRefs();
            RebuildLookupCache();
        }

        public void Save()
        {
            if (_storage == null)
            {
                InitializeStorage();
            }

            CancelDeferredSave();
            SortNodes(_roots);

            var persistentNodes = ClonePersistentNodes(_roots);
            var flatNodes = new List<SerializableNode>();
            foreach (var root in persistentNodes)
            {
                FlattenNode(root, null, flatNodes);
            }

            _model = new SerializableModel
            {
                Version = 2,
                Nodes = flatNodes,
            };

            var json = JsonUtility.ToJson(_model, _saveMode == SaveMode.UserSettingsFile);
            _storage.Save(json);
            _lastLoadedFileTimeUtc = _storage.GetLastWriteTimeUtc();
            RebuildLookupCache();
            OnSaved?.Invoke();
        }

        public bool CheckExternalUpdate()
        {
            if (_storage == null)
            {
                InitializeStorage();
            }

            if (_saveMode != SaveMode.UserSettingsFile)
            {
                return false;
            }

            if (!_storage.Exists())
            {
                return false;
            }

            var currentWriteTime = _storage.GetLastWriteTimeUtc();
            if (currentWriteTime > _lastLoadedFileTimeUtc)
            {
                Load();
                return true;
            }

            return false;
        }

        public void SaveDeferred()
        {
            if (_saveDeferredQueued)
            {
                return;
            }

            _saveDeferredQueued = true;
            EditorApplication.delayCall -= FlushDeferredSave;
            EditorApplication.delayCall += FlushDeferredSave;
        }

        public void FlushPendingSave()
        {
            if (!_saveDeferredQueued)
            {
                return;
            }

            CancelDeferredSave();
            Save();
        }

        public CustomProjectNode AddGroup(string label, CustomProjectNode parent = null)
        {
            if (!CanAcceptChild(parent))
            {
                return null;
            }

            var node = CustomProjectNode.CreateManualGroup(label);
            AppendToParent(node, parent);
            Save();
            return node;
        }

        public CustomProjectNode AddAssetRef(string guid, CustomProjectNode parent = null)
        {
            if (!CanAcceptChild(parent))
            {
                return null;
            }

            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath) || AssetDatabase.IsValidFolder(assetPath))
            {
                return null;
            }

            if (HasManualAssetRef(guid))
            {
                return null;
            }

            var node = CustomProjectNode.CreateManualAssetRef(guid, Path.GetFileName(assetPath));
            AppendToParent(node, parent);
            Save();
            return node;
        }

        public CustomProjectNode AddFolderRef(string assetPath, CustomProjectNode parent = null)
        {
            if (!CanAcceptChild(parent))
            {
                return null;
            }

            var normalized = CustomProjectNode.NormalizeAssetPath(assetPath);
            if (string.IsNullOrEmpty(normalized) || !AssetDatabase.IsValidFolder(normalized))
            {
                return null;
            }

            if (HasFolderLink(normalized))
            {
                return null;
            }

            var node = CustomProjectNode.CreateFolderRefRoot(normalized);
            AppendToParent(node, parent);
            SyncFolderRef(node);
            Save();
            return node;
        }

        public CustomProjectNode AddFolderPointer(string assetPath, CustomProjectNode parent = null)
        {
            if (!CanAcceptChild(parent))
            {
                return null;
            }

            var normalized = CustomProjectNode.NormalizeAssetPath(assetPath);
            if (string.IsNullOrEmpty(normalized) || !AssetDatabase.IsValidFolder(normalized))
            {
                return null;
            }

            if (HasFolderLink(normalized))
            {
                return null;
            }

            var node = CustomProjectNode.CreateFolderPointer(normalized);
            AppendToParent(node, parent);
            RefreshFolderPointer(node);
            Save();
            return node;
        }

        public bool RenameNode(CustomProjectNode node, string newLabel)
        {
            if (node == null || !node.CanRenameInTree)
            {
                return false;
            }

            if (node.IsManualGroup)
            {
                node.Label = string.IsNullOrWhiteSpace(newLabel) ? node.Label : newLabel.Trim();
                Save();
                return true;
            }

            if (node.CanDeleteOnDisk)
            {
                return RenameSyncedAsset(node, newLabel);
            }

            return false;
        }

        public bool Remove(CustomProjectNode node)
        {
            if (node == null || !node.CanRemoveFromList)
            {
                return false;
            }

            var removed = RemoveRecursive(_roots, node.Id);
            if (removed)
            {
                Save();
            }
            return removed;
        }

        public bool MoveNode(CustomProjectNode source, CustomProjectNode targetParent)
        {
            if (source == null || !source.CanMoveInTree)
            {
                return false;
            }

            if (!CanAcceptChild(targetParent))
            {
                return false;
            }

            if (targetParent != null && IsDescendant(source, targetParent))
            {
                return false;
            }

            if (!RemoveRecursive(_roots, source.Id))
            {
                return false;
            }

            AppendToParent(source, targetParent);
            Save();
            return true;
        }

        public void ConvertFolderRefToSnapshotGroup(CustomProjectNode folderRefRoot)
        {
            if (folderRefRoot == null || !folderRefRoot.IsFolderRefRoot)
            {
                return;
            }

            var replacement = SnapshotAsManualGroup(folderRefRoot);
            if (replacement == null)
            {
                return;
            }

            ReplaceNode(folderRefRoot.Id, replacement, _roots);
            Save();
        }

        public void SyncAllFolderRefs()
        {
            MarkLookupCacheDirty();
            SyncFolderRefsRecursive(_roots);
            RebuildLookupCache();
        }

        public bool HandleAssetMoved(string oldPath, string newPath, bool save = true)
        {
            var guid = AssetDatabase.AssetPathToGUID(newPath);
            if (string.IsNullOrEmpty(guid))
            {
                return false;
            }

            var movedAny = HandleAssetMovedRecursive(_roots, guid, newPath);
            var syncedAny = SyncFolderRefsForPaths(new[] { oldPath, newPath });
            var changed = movedAny || syncedAny;
            if (changed && save)
            {
                Save();
            }

            return changed;
        }

        public bool HandleAssetDeleted(string deletedPath, bool save = true)
        {
            var removedAny = RemoveMissingManualAssetRefs(_roots);
            var syncedAny = SyncFolderRefsForPaths(new[] { deletedPath });
            var refreshedAny = RefreshFolderPointersForPaths(new[] { deletedPath });
            var changed = removedAny || syncedAny || refreshedAny;
            if (changed && save)
            {
                Save();
            }

            return changed;
        }

        public bool HandleAssetsImported(IEnumerable<string> importedPaths, bool save = true)
        {
            var syncedAny = SyncFolderRefsForPaths(importedPaths);
            var refreshedAny = RefreshFolderPointersForPaths(importedPaths);
            var changed = syncedAny || refreshedAny;
            if (!changed)
            {
                return false;
            }

            if (save)
            {
                Save();
            }
            return true;
        }

        public void SetExpanded(CustomProjectNode node, bool expanded)
        {
            if (node == null || !node.IsContainer)
            {
                return;
            }

            node.IsExpanded = expanded;
        }

        public CustomProjectNode FindNodeById(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            EnsureLookupCache();
            _nodeById.TryGetValue(id, out var node);
            return node;
        }

        public CustomProjectNode FindManualAssetRefByGuid(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return null;
            }

            EnsureLookupCache();
            _manualAssetRefByGuid.TryGetValue(guid, out var node);
            return node;
        }

        public CustomProjectNode FindNodeByAssetPath(string assetPath)
        {
            var normalized = CustomProjectNode.NormalizeAssetPath(assetPath);
            if (string.IsNullOrEmpty(normalized))
            {
                return null;
            }

            EnsureLookupCache();
            _nodeByAssetPath.TryGetValue(normalized, out var node);
            return node;
        }

        public List<CustomProjectNode> Search(string query)
        {
            var result = new List<CustomProjectNode>();
            SearchRecursive(_roots, query ?? string.Empty, result);
            return result;
        }

        private void SanitizeTree(List<CustomProjectNode> nodes)
        {
            if (nodes == null)
            {
                return;
            }

            for (var i = nodes.Count - 1; i >= 0; i--)
            {
                var node = nodes[i];
                if (node == null)
                {
                    nodes.RemoveAt(i);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(node.Id))
                {
                    node.Id = $"manual-legacy:{Guid.NewGuid():N}";
                }

                if (string.IsNullOrWhiteSpace(node.Label))
                {
                    node.Label = node.Kind == ProjectNodeKind.Asset ? "Missing Asset" : "Group";
                }

                node.Children ??= new List<CustomProjectNode>();

                if (node.Source == ProjectNodeSource.FolderRefSynced)
                {
                    nodes.RemoveAt(i);
                    continue;
                }

                if (node.IsFolderRefRoot || node.IsFolderPointer)
                {
                    node.AssetPath = CustomProjectNode.NormalizeAssetPath(node.ResolveAssetPath());
                    node.AssetGuid = string.IsNullOrEmpty(node.AssetGuid) && !string.IsNullOrEmpty(node.AssetPath)
                        ? AssetDatabase.AssetPathToGUID(node.AssetPath)
                        : node.AssetGuid;
                    node.Children.Clear();
                }
                else
                {
                    if (node.Kind == ProjectNodeKind.Asset)
                    {
                        node.AssetPath = CustomProjectNode.NormalizeAssetPath(node.ResolveAssetPath());
                        if (string.IsNullOrEmpty(node.AssetGuid) && !string.IsNullOrEmpty(node.AssetPath))
                        {
                            node.AssetGuid = AssetDatabase.AssetPathToGUID(node.AssetPath);
                        }
                    }
                    SanitizeTree(node.Children);
                }
            }
        }

        private List<CustomProjectNode> ClonePersistentNodes(List<CustomProjectNode> nodes)
        {
            var result = new List<CustomProjectNode>();
            if (nodes == null)
            {
                return result;
            }

            foreach (var node in nodes)
            {
                var clone = ClonePersistentNode(node);
                if (clone != null)
                {
                    result.Add(clone);
                }
            }

            return result;
        }

        private CustomProjectNode ClonePersistentNode(CustomProjectNode node)
        {
            if (node == null || node.Source == ProjectNodeSource.FolderRefSynced)
            {
                return null;
            }

            var clone = new CustomProjectNode
            {
                Id = node.Id,
                Label = node.Label,
                Kind = node.Kind,
                Source = node.Source,
                AssetGuid = node.AssetGuid,
                AssetPath = node.AssetPath,
                IsExpanded = node.IsExpanded,
                Children = new List<CustomProjectNode>(),
            };

            if (node.IsManualGroup)
            {
                clone.Children = ClonePersistentNodes(node.Children);
            }

            return clone;
        }

        private void AppendToParent(CustomProjectNode node, CustomProjectNode parent)
        {
            if (parent == null)
            {
                _roots.Add(node);
                return;
            }

            parent.Children ??= new List<CustomProjectNode>();

            parent.Children.Add(node);
            parent.IsExpanded = true;
        }

        private bool CanAcceptChild(CustomProjectNode parent)
        {
            return parent == null || parent.CanAddChildren;
        }

        private bool HasManualAssetRef(string guid)
        {
            return FindManualAssetRefByGuid(guid) != null;
        }

        private bool HasFolderLink(string assetPath)
        {
            var normalized = CustomProjectNode.NormalizeAssetPath(assetPath);
            return EnumerateNodes(_roots)
                .Any(n => (n.IsFolderRefRoot || n.IsFolderPointer) && CustomProjectNode.NormalizeAssetPath(n.ResolveAssetPath()) == normalized);
        }

        private bool RemoveRecursive(List<CustomProjectNode> nodes, string id)
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Id == id)
                {
                    nodes.RemoveAt(i);
                    return true;
                }

                if (RemoveRecursive(nodes[i].Children, id))
                {
                    return true;
                }
            }

            return false;
        }

        private bool RenameSyncedAsset(CustomProjectNode node, string newLabel)
        {
            var path = node.ResolveAssetPath();
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            var trimmed = string.IsNullOrWhiteSpace(newLabel) ? string.Empty : newLabel.Trim();
            var renameTarget = Path.GetFileNameWithoutExtension(trimmed);
            if (string.IsNullOrWhiteSpace(renameTarget))
            {
                return false;
            }

            var error = AssetDatabase.RenameAsset(path, renameTarget);
            if (!string.IsNullOrEmpty(error))
            {
                EditorUtility.DisplayDialog("名前を変更できません", error, "OK");
                return false;
            }

            var renamedPath = CustomProjectNode.NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(node.AssetGuid));
            if (string.IsNullOrEmpty(renamedPath))
            {
                renamedPath = CustomProjectNode.NormalizeAssetPath(path);
            }

            node.AssetPath = renamedPath;
            node.Label = Path.GetFileName(renamedPath);
            Save();
            return true;
        }

        private bool ReplaceNode(string oldId, CustomProjectNode replacement, List<CustomProjectNode> nodes)
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Id == oldId)
                {
                    nodes[i] = replacement;
                    return true;
                }

                if (ReplaceNode(oldId, replacement, nodes[i].Children))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsDescendant(CustomProjectNode ancestor, CustomProjectNode candidate)
        {
            if (ancestor == null || candidate == null)
            {
                return false;
            }

            return ancestor.Children
                .Any(child => child.Id == candidate.Id || IsDescendant(child, candidate));
        }

        private void SyncFolderRefsRecursive(List<CustomProjectNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (node.IsFolderRefRoot)
                {
                    SyncFolderRef(node);
                }

                if (node.Children != null && node.Children.Count > 0)
                {
                    SyncFolderRefsRecursive(node.Children);
                }
            }
        }

        private bool SyncFolderRefsForPaths(IEnumerable<string> assetPaths)
        {
            if (assetPaths == null)
            {
                return false;
            }

            var normalizedPathList = assetPaths
                .Where(path => !string.IsNullOrEmpty(path))
                .Select(CustomProjectNode.NormalizeAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalizedPathList.Count == 0)
            {
                return false;
            }

            var folderRefRootList = EnumerateNodes(_roots)
                .Where(node => node.IsFolderRefRoot)
                .ToList();

            var syncedAny = false;

            foreach (var folderRefRoot in folderRefRootList)
            {
                var folderPath = CustomProjectNode.NormalizeAssetPath(folderRefRoot.ResolveAssetPath());
                if (string.IsNullOrEmpty(folderPath))
                {
                    folderPath = CustomProjectNode.NormalizeAssetPath(folderRefRoot.AssetPath);
                }

                if (string.IsNullOrEmpty(folderPath))
                {
                    continue;
                }

                if (!normalizedPathList.Any(path => IsSameOrChildPath(path, folderPath)))
                {
                    continue;
                }

                InvalidateSyncedFolderSubtree(folderRefRoot);
                SyncFolderRef(folderRefRoot);
                syncedAny = true;
            }

            return syncedAny;
        }

        public void SyncFolderRef(CustomProjectNode folderRefRoot)
        {
            if (folderRefRoot == null || !folderRefRoot.IsFolderRefRoot)
            {
                return;
            }

            var folderPath = folderRefRoot.ResolveAssetPath();
            folderPath = CustomProjectNode.NormalizeAssetPath(folderPath);
            folderRefRoot.AssetPath = folderPath;

            if (string.IsNullOrEmpty(folderPath) || !CustomProjectAssetCache.IsValidFolder(folderPath))
            {
                folderRefRoot.Children = new List<CustomProjectNode>();
                folderRefRoot.SyncedChildrenLoaded = true;
                return;
            }

            folderRefRoot.AssetGuid = AssetDatabase.AssetPathToGUID(folderPath);
            folderRefRoot.Label = Path.GetFileName(folderPath.TrimEnd('/', '\\'));
            folderRefRoot.SyncedChildrenLoaded = false;
            EnsureSyncedFolderChildrenLoaded(folderRefRoot);
        }

        /// <summary>
        /// FolderRef 同期フォルダの直下1階層だけを読み込む。
        /// 深い階層は展開時に遅延ロードする。
        /// </summary>
        public void EnsureSyncedFolderChildrenLoaded(CustomProjectNode folderNode)
        {
            if (folderNode == null)
            {
                return;
            }
            if (!folderNode.IsFolderRefRoot && !folderNode.IsLazySyncedFolder)
            {
                return;
            }
            if (folderNode.SyncedChildrenLoaded)
            {
                return;
            }
            var folderPath = CustomProjectNode.NormalizeAssetPath(folderNode.ResolveAssetPath());
            folderNode.AssetPath = folderPath;
            if (string.IsNullOrEmpty(folderPath) || !CustomProjectAssetCache.IsValidFolder(folderPath))
            {
                folderNode.Children = new List<CustomProjectNode>();
                folderNode.SyncedChildrenLoaded = true;
                return;
            }
            folderNode.Children = BuildSyncedChildrenImmediate(folderPath);
            folderNode.SyncedChildrenLoaded = true;
        }

        private void InvalidateSyncedFolderSubtree(CustomProjectNode folderNode)
        {
            if (folderNode == null)
            {
                return;
            }
            if (folderNode.IsFolderRefRoot || folderNode.IsLazySyncedFolder)
            {
                folderNode.SyncedChildrenLoaded = false;
                folderNode.Children = new List<CustomProjectNode>();
            }
            if (folderNode.Children == null || folderNode.Children.Count == 0)
            {
                return;
            }
            var children = folderNode.Children.ToArray();
            foreach (var child in children)
            {
                InvalidateSyncedFolderSubtree(child);
            }
        }

        private void RefreshFolderPointer(CustomProjectNode folderPointer)
        {
            if (folderPointer == null || !folderPointer.IsFolderPointer)
            {
                return;
            }

            var folderPath = folderPointer.ResolveAssetPath();
            folderPath = CustomProjectNode.NormalizeAssetPath(folderPath);
            folderPointer.AssetPath = folderPath;
            folderPointer.Children = new List<CustomProjectNode>();

            if (string.IsNullOrEmpty(folderPath) || !CustomProjectAssetCache.IsValidFolder(folderPath))
            {
                folderPointer.AssetGuid = string.Empty;
                return;
            }

            folderPointer.AssetGuid = AssetDatabase.AssetPathToGUID(folderPath);
            folderPointer.Label = Path.GetFileName(folderPath.TrimEnd('/', '\\'));
        }

        private List<CustomProjectNode> BuildSyncedChildrenImmediate(string folderPath)
        {
            var absPath = ToAbsolutePath(folderPath);
            var result = new List<CustomProjectNode>();

            if (!Directory.Exists(absPath))
            {
                return result;
            }

            foreach (var dir in Directory.EnumerateDirectories(absPath).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                var name = Path.GetFileName(dir);
                if (ShouldExclude(name))
                {
                    continue;
                }

                var childPath = CustomProjectNode.NormalizeAssetPath(Path.Combine(folderPath, name));
                var folderNode = CustomProjectNode.CreateSyncedFolder(childPath);
                folderNode.SyncedChildrenLoaded = false;
                folderNode.Children = new List<CustomProjectNode>();
                result.Add(folderNode);
            }

            foreach (var file in Directory.EnumerateFiles(absPath).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                var name = Path.GetFileName(file);
                if (ShouldExclude(name))
                {
                    continue;
                }

                var childPath = CustomProjectNode.NormalizeAssetPath(Path.Combine(folderPath, name));
                var guid = AssetDatabase.AssetPathToGUID(childPath);
                if (string.IsNullOrEmpty(guid))
                {
                    continue;
                }

                result.Add(CustomProjectNode.CreateSyncedAsset(childPath, guid));
            }

            return result;
        }

        private bool HandleAssetMovedRecursive(List<CustomProjectNode> nodes, string guid, string newPath)
        {
            var changed = false;

            foreach (var node in nodes)
            {
                if (node.Source == ProjectNodeSource.Manual && node.Kind == ProjectNodeKind.Asset && node.AssetGuid == guid)
                {
                    node.Label = Path.GetFileName(newPath);
                    changed = true;
                }
                else if ((node.IsFolderRefRoot || node.IsFolderPointer) && node.AssetGuid == guid)
                {
                    node.AssetPath = CustomProjectNode.NormalizeAssetPath(newPath);
                    node.Label = Path.GetFileName(newPath.TrimEnd('/', '\\'));
                    changed = true;
                }

                if (node.Children != null && node.Children.Count > 0)
                {
                    changed |= HandleAssetMovedRecursive(node.Children, guid, newPath);
                }
            }

            return changed;
        }

        private bool RemoveMissingManualAssetRefs(List<CustomProjectNode> nodes)
        {
            var removedAny = false;

            for (var i = nodes.Count - 1; i >= 0; i--)
            {
                var node = nodes[i];
                if (node.Source == ProjectNodeSource.Manual && node.Kind == ProjectNodeKind.Asset)
                {
                    var path = node.ResolveAssetPath();
                    if (string.IsNullOrEmpty(path))
                    {
                        nodes.RemoveAt(i);
                        removedAny = true;
                        continue;
                    }
                }

                removedAny |= RemoveMissingManualAssetRefs(node.Children);
            }

            return removedAny;
        }

        private bool RefreshFolderPointersForPaths(IEnumerable<string> assetPaths)
        {
            if (assetPaths == null)
            {
                return false;
            }

            var normalizedPathList = assetPaths
                .Where(path => !string.IsNullOrEmpty(path))
                .Select(CustomProjectNode.NormalizeAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalizedPathList.Count == 0)
            {
                return false;
            }

            var folderPointerList = EnumerateNodes(_roots)
                .Where(node => node.IsFolderPointer)
                .ToList();

            var refreshedAny = false;

            foreach (var folderPointer in folderPointerList)
            {
                var folderPath = CustomProjectNode.NormalizeAssetPath(folderPointer.ResolveAssetPath());
                if (string.IsNullOrEmpty(folderPath))
                {
                    folderPath = CustomProjectNode.NormalizeAssetPath(folderPointer.AssetPath);
                }

                if (string.IsNullOrEmpty(folderPath))
                {
                    continue;
                }

                if (!normalizedPathList.Any(path => string.Equals(path, folderPath, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                RefreshFolderPointer(folderPointer);
                refreshedAny = true;
            }

            return refreshedAny;
        }

        private CustomProjectNode SnapshotAsManualGroup(CustomProjectNode folderRefRoot)
        {
            var group = CustomProjectNode.CreateManualGroup(folderRefRoot.Label);
            group.IsExpanded = folderRefRoot.IsExpanded;

            foreach (var child in folderRefRoot.Children)
            {
                var snapshot = SnapshotChildRecursive(child);
                if (snapshot != null)
                {
                    group.Children.Add(snapshot);
                }
            }

            return group;
        }

        private CustomProjectNode SnapshotChildRecursive(CustomProjectNode node)
        {
            if (node == null)
            {
                return null;
            }

            if (node.Kind == ProjectNodeKind.Asset)
            {
                var path = node.ResolveAssetPath();
                var guid = !string.IsNullOrEmpty(node.AssetGuid)
                    ? node.AssetGuid
                    : AssetDatabase.AssetPathToGUID(path);

                if (string.IsNullOrEmpty(guid))
                {
                    return null;
                }

                return CustomProjectNode.CreateManualAssetRef(guid, node.Label);
            }

            var group = CustomProjectNode.CreateManualGroup(node.Label);
            group.IsExpanded = node.IsExpanded;
            foreach (var child in node.Children)
            {
                var snapshot = SnapshotChildRecursive(child);
                if (snapshot != null)
                {
                    group.Children.Add(snapshot);
                }
            }

            return group;
        }

        private CustomProjectNode FindNodeByIdRecursive(string id, List<CustomProjectNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (node.Id == id)
                {
                    return node;
                }

                var found = FindNodeByIdRecursive(id, node.Children);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private void MarkLookupCacheDirty()
        {
            _lookupCacheDirty = true;
        }

        private void CancelDeferredSave()
        {
            if (!_saveDeferredQueued)
            {
                return;
            }

            _saveDeferredQueued = false;
            EditorApplication.delayCall -= FlushDeferredSave;
        }

        private void FlushDeferredSave()
        {
            if (!_saveDeferredQueued)
            {
                return;
            }

            _saveDeferredQueued = false;
            EditorApplication.delayCall -= FlushDeferredSave;
            Save();
        }

        private void EnsureLookupCache()
        {
            if (_lookupCacheDirty)
            {
                RebuildLookupCache();
            }
        }

        private void RebuildLookupCache()
        {
            _nodeById.Clear();
            _nodeByAssetPath.Clear();
            _manualAssetRefByGuid.Clear();

            if (_roots != null)
            {
                foreach (var node in EnumerateNodes(_roots))
                {
                    if (!string.IsNullOrEmpty(node.Id) && !_nodeById.ContainsKey(node.Id))
                    {
                        _nodeById[node.Id] = node;
                    }

                    var assetPath = CustomProjectNode.NormalizeAssetPath(node.ResolveAssetPath());
                    if (!string.IsNullOrEmpty(assetPath) && !_nodeByAssetPath.ContainsKey(assetPath))
                    {
                        _nodeByAssetPath[assetPath] = node;
                    }

                    if (node.Source == ProjectNodeSource.Manual
                        && node.Kind == ProjectNodeKind.Asset
                        && !string.IsNullOrEmpty(node.AssetGuid)
                        && !_manualAssetRefByGuid.ContainsKey(node.AssetGuid))
                    {
                        _manualAssetRefByGuid[node.AssetGuid] = node;
                    }
                }
            }

            _lookupCacheDirty = false;
        }

        private CustomProjectNode FindManualAssetRefByGuidRecursive(string guid, List<CustomProjectNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (node.Source == ProjectNodeSource.Manual && node.Kind == ProjectNodeKind.Asset && node.AssetGuid == guid)
                {
                    return node;
                }

                var found = FindManualAssetRefByGuidRecursive(guid, node.Children);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private CustomProjectNode FindNodeByAssetPathRecursive(string assetPath, List<CustomProjectNode> nodes)
        {
            foreach (var node in nodes)
            {
                var resolved = CustomProjectNode.NormalizeAssetPath(node.ResolveAssetPath());
                if (!string.IsNullOrEmpty(resolved) && resolved == assetPath)
                {
                    return node;
                }

                var found = FindNodeByAssetPathRecursive(assetPath, node.Children);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private void SearchRecursive(List<CustomProjectNode> nodes, string query, List<CustomProjectNode> result)
        {
            foreach (var node in nodes)
            {
                if (node.Label.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    result.Add(node);
                }

                SearchRecursive(node.Children, query, result);
            }
        }

        private IEnumerable<CustomProjectNode> EnumerateNodes(List<CustomProjectNode> nodes)
        {
            foreach (var node in nodes)
            {
                yield return node;
                foreach (var child in EnumerateNodes(node.Children))
                {
                    yield return child;
                }
            }
        }

        private void SortNodes(List<CustomProjectNode> nodes)
        {
            nodes.Sort((a, b) =>
            {
                var pa = GetSortPriority(a);
                var pb = GetSortPriority(b);
                if (pa != pb)
                {
                    return pa.CompareTo(pb);
                }

                return string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase);
            });

            foreach (var node in nodes)
            {
                if (node.Source != ProjectNodeSource.FolderRefSynced && node.Children != null && node.Children.Count > 0)
                {
                    SortNodes(node.Children);
                }
            }
        }

        private int GetSortPriority(CustomProjectNode node)
        {
            if (node.IsManualGroup)
            {
                return 0;
            }

            if (node.IsFolderRefRoot)
            {
                return 1;
            }

            if (node.IsFolderPointer)
            {
                return 1;
            }

            if (node.Kind == ProjectNodeKind.Folder)
            {
                return 2;
            }

            return 3;
        }

        private static bool ShouldExclude(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return true;
            }

            if (name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (name.StartsWith(".", StringComparison.Ordinal))
            {
                return true;
            }

            if (name.EndsWith("~", StringComparison.Ordinal))
            {
                return true;
            }

            if (name == "Temp" || name == "Library" || name == "obj")
            {
                return true;
            }

            return false;
        }

        private static bool IsSameOrChildPath(string path, string rootPath)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(rootPath))
            {
                return false;
            }

            if (string.Equals(path, rootPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return path.StartsWith(rootPath + "/", StringComparison.OrdinalIgnoreCase);
        }

        private static string ToAbsolutePath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        }

        private void FlattenNode(CustomProjectNode node, string parentId, List<SerializableNode> list)
        {
            if (node == null) return;
            list.Add(new SerializableNode
            {
                Id = node.Id,
                ParentId = parentId,
                Label = node.Label,
                Kind = node.Kind,
                Source = node.Source,
                AssetGuid = node.AssetGuid,
                AssetPath = node.AssetPath,
                IsExpanded = node.IsExpanded
            });
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    FlattenNode(child, node.Id, list);
                }
            }
        }

        private List<CustomProjectNode> ReconstructTree(List<SerializableNode> flatNodes)
        {
            var roots = new List<CustomProjectNode>();
            if (flatNodes == null || flatNodes.Count == 0)
            {
                return roots;
            }

            var nodeMap = new Dictionary<string, CustomProjectNode>(StringComparer.Ordinal);
            foreach (var flat in flatNodes)
            {
                if (flat == null) continue;
                var node = new CustomProjectNode
                {
                    Id = flat.Id,
                    Label = flat.Label,
                    Kind = flat.Kind,
                    Source = flat.Source,
                    AssetGuid = flat.AssetGuid,
                    AssetPath = flat.AssetPath,
                    IsExpanded = flat.IsExpanded,
                    Children = new List<CustomProjectNode>()
                };
                nodeMap[node.Id] = node;
            }

            foreach (var flat in flatNodes)
            {
                if (flat == null) continue;
                if (nodeMap.TryGetValue(flat.Id, out var node))
                {
                    if (string.IsNullOrEmpty(flat.ParentId))
                    {
                        roots.Add(node);
                    }
                    else if (nodeMap.TryGetValue(flat.ParentId, out var parent))
                    {
                        parent.Children.Add(node);
                    }
                    else
                    {
                        roots.Add(node);
                    }
                }
            }

            return roots;
        }
    }
}
