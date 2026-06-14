using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Lilja.CustomProjectWindow
{
    public enum ProjectNodeKind
    {
        Group,
        Folder,
        Asset,
    }

    public enum ProjectNodeSource
    {
        Manual,
        FolderRefRoot,
        FolderRefSynced,
        FolderPointer,
    }

    [Serializable]
    public sealed class CustomProjectNode
    {
        public string Id;
        public string Label;
        public ProjectNodeKind Kind;
        public ProjectNodeSource Source;
        public string AssetGuid;
        public string AssetPath;
        public bool IsExpanded = true;
        public List<CustomProjectNode> Children = new();

        /// <summary>FolderRef 同期フォルダの子階層が読み込み済みかどうか（永続化しない）</summary>
        [System.NonSerialized]
        public bool SyncedChildrenLoaded;

        public bool IsContainer => Kind != ProjectNodeKind.Asset && !IsFolderPointer;
        public bool IsLazySyncedFolder => Source == ProjectNodeSource.FolderRefSynced && Kind == ProjectNodeKind.Folder;
        public bool IsManualGroup => Kind == ProjectNodeKind.Group && Source == ProjectNodeSource.Manual;
        public bool IsFolderRefRoot => Kind == ProjectNodeKind.Folder && Source == ProjectNodeSource.FolderRefRoot;
        public bool IsFolderPointer => Kind == ProjectNodeKind.Folder && Source == ProjectNodeSource.FolderPointer;
        public bool IsSynced => Source == ProjectNodeSource.FolderRefSynced;
        public bool CanAddChildren => IsManualGroup;
        public bool CanRenameInTree => IsManualGroup || CanDeleteOnDisk;
        public bool CanRemoveFromList => Source != ProjectNodeSource.FolderRefSynced;
        public bool CanMoveInTree => Source != ProjectNodeSource.FolderRefSynced;
        public bool CanDeleteOnDisk => Kind == ProjectNodeKind.Asset && !string.IsNullOrEmpty(ResolveAssetPath());
        public bool CanOpenAsset => Kind == ProjectNodeKind.Asset;
        public bool CanSelectInProject => !string.IsNullOrEmpty(ResolveAssetPath());
        public bool CanCopyPath => !string.IsNullOrEmpty(ResolveAssetPath());
        public bool CanRevealInFinder => !string.IsNullOrEmpty(ResolveAssetPath());

        public string ResolveAssetPath()
        {
            if (!string.IsNullOrEmpty(AssetPath))
            {
                return AssetPath;
            }

            if (!string.IsNullOrEmpty(AssetGuid))
            {
                return AssetDatabase.GUIDToAssetPath(AssetGuid);
            }

            return null;
        }

        public static CustomProjectNode CreateManualGroup(string label)
        {
            return new CustomProjectNode
            {
                Id = $"manual-group:{Guid.NewGuid():N}",
                Label = string.IsNullOrWhiteSpace(label) ? "New Group" : label.Trim(),
                Kind = ProjectNodeKind.Group,
                Source = ProjectNodeSource.Manual,
                IsExpanded = true,
            };
        }

        public static CustomProjectNode CreateManualAssetRef(string guid, string label)
        {
            return new CustomProjectNode
            {
                Id = $"manual-asset:{Guid.NewGuid():N}",
                Label = label,
                Kind = ProjectNodeKind.Asset,
                Source = ProjectNodeSource.Manual,
                AssetGuid = guid,
                IsExpanded = false,
            };
        }

        public static CustomProjectNode CreateFolderRefRoot(string assetPath)
        {
            var normalized = NormalizeAssetPath(assetPath);
            return new CustomProjectNode
            {
                Id = $"folderref-root:{normalized}",
                Label = Path.GetFileName(normalized.TrimEnd('/', '\\')),
                Kind = ProjectNodeKind.Folder,
                Source = ProjectNodeSource.FolderRefRoot,
                AssetGuid = AssetDatabase.AssetPathToGUID(normalized),
                AssetPath = normalized,
                IsExpanded = false,
            };
        }

        public static CustomProjectNode CreateFolderPointer(string assetPath)
        {
            var normalized = NormalizeAssetPath(assetPath);
            return new CustomProjectNode
            {
                Id = $"folder-pointer:{normalized}",
                Label = Path.GetFileName(normalized.TrimEnd('/', '\\')),
                Kind = ProjectNodeKind.Folder,
                Source = ProjectNodeSource.FolderPointer,
                AssetGuid = AssetDatabase.AssetPathToGUID(normalized),
                AssetPath = normalized,
                IsExpanded = false,
            };
        }

        public static CustomProjectNode CreateSyncedFolder(string assetPath)
        {
            var normalized = NormalizeAssetPath(assetPath);
            return new CustomProjectNode
            {
                Id = $"folderref-sync-folder:{normalized}",
                Label = Path.GetFileName(normalized.TrimEnd('/', '\\')),
                Kind = ProjectNodeKind.Folder,
                Source = ProjectNodeSource.FolderRefSynced,
                AssetPath = normalized,
                IsExpanded = false,
            };
        }

        public static CustomProjectNode CreateSyncedAsset(string assetPath, string guid)
        {
            var normalized = NormalizeAssetPath(assetPath);
            return new CustomProjectNode
            {
                Id = $"folderref-sync-asset:{normalized}",
                Label = Path.GetFileName(normalized),
                Kind = ProjectNodeKind.Asset,
                Source = ProjectNodeSource.FolderRefSynced,
                AssetGuid = guid,
                AssetPath = normalized,
                IsExpanded = false,
            };
        }

        public static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace("\\", "/").TrimEnd('/');
        }
    }

    [Serializable]
    public sealed class SerializableNode
    {
        public string Id;
        public string ParentId;
        public string Label;
        public ProjectNodeKind Kind;
        public ProjectNodeSource Source;
        public string AssetGuid;
        public string AssetPath;
        public bool IsExpanded;
    }

    [Serializable]
    internal sealed class SerializableModel
    {
        public int Version = 2;
        public List<SerializableNode> Nodes = new();
    }

    internal static class CustomProjectViewIcons
    {
        private const string MissingAssetIconName = "console.erroricon.sml";
        private const string FolderFavoriteIconName = "FolderFavorite Icon";
        private const string FolderIconName = "Folder Icon";
        private const string AddGroupIconName = "CreateAddNew";
        private const string DropdownIconName = "d_icon dropdown@2x";
        private const string ExpandIconName = "CollabCreate Icon";
        private const string CollapseIconName = "CollabDeleted Icon";
        private const string SyncIconName = "d_Linked";
        private const string TrashIconName = "d_TreeEditor.Trash";
        private const string ProjectIconName = "FolderFavorite Icon";
        private const string PingIconName = "d_Selectable Icon";
        private const string FolderPointerIconName = "d_FolderOpened Icon";

        public static Texture2D MissingAsset => GetTexture(MissingAssetIconName);
        public static Texture2D FolderRefRoot => GetTexture(FolderFavoriteIconName);
        public static Texture2D FolderPointer => GetTexture(FolderPointerIconName);
        public static Texture2D Folder => GetTexture(FolderIconName);
        public static Texture2D AddGroup => GetTexture(AddGroupIconName);
        public static Texture2D Dropdown => GetTexture(DropdownIconName);
        public static Texture2D Expand => GetTexture(ExpandIconName);
        public static Texture2D Collapse => GetTexture(CollapseIconName);
        public static Texture2D Sync => GetTexture(SyncIconName);
        public static Texture2D Remove => GetTexture(TrashIconName);
        public static Texture2D Project => GetTexture(ProjectIconName);
        public static Texture2D Ping => GetTexture(PingIconName);

        private static Texture2D GetTexture(string iconName)
        {
            if (string.IsNullOrEmpty(iconName))
            {
                return null;
            }

            return EditorGUIUtility.IconContent(iconName)?.image as Texture2D;
        }
    }
}
