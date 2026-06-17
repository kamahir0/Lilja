using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Lilja.CustomProjectWindow
{
    /// <summary>
    /// CustomProjectView 向けのアセット存在・フォルダ・Icon 判定キャッシュ。
    /// Reload ごとの AssetDatabase 呼び出しを抑える。
    /// </summary>
    internal static class CustomProjectAssetCache
    {
        private static readonly Dictionary<string, bool> MissingByPath = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, bool> ValidFolderByPath = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, Texture2D> IconByPath = new(StringComparer.Ordinal);

        public static void InvalidateAll()
        {
            MissingByPath.Clear();
            ValidFolderByPath.Clear();
            IconByPath.Clear();
        }

        public static void InvalidatePath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }
            var normalized = CustomProjectNode.NormalizeAssetPath(assetPath);
            MissingByPath.Remove(normalized);
            ValidFolderByPath.Remove(normalized);
            IconByPath.Remove(normalized);
        }

        public static void InvalidatePaths(IEnumerable<string> assetPaths)
        {
            if (assetPaths == null)
            {
                return;
            }
            foreach (var assetPath in assetPaths)
            {
                InvalidatePath(assetPath);
            }
        }

        public static bool IsMissingAsset(string assetPath, string assetGuid = null)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return true;
            }
            var normalized = CustomProjectNode.NormalizeAssetPath(assetPath);
            if (MissingByPath.TryGetValue(normalized, out var missing))
            {
                return missing;
            }
            if (!string.IsNullOrEmpty(assetGuid))
            {
                var pathFromGuid = AssetDatabase.GUIDToAssetPath(assetGuid);
                missing = string.IsNullOrEmpty(pathFromGuid)
                    || !string.Equals(CustomProjectNode.NormalizeAssetPath(pathFromGuid), normalized, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                missing = string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(normalized));
            }
            MissingByPath[normalized] = missing;
            return missing;
        }

        public static bool IsValidFolder(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }
            var normalized = CustomProjectNode.NormalizeAssetPath(assetPath);
            if (ValidFolderByPath.TryGetValue(normalized, out var valid))
            {
                return valid;
            }
            valid = AssetDatabase.IsValidFolder(normalized);
            ValidFolderByPath[normalized] = valid;
            return valid;
        }

        public static Texture2D GetCachedIcon(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }
            var normalized = CustomProjectNode.NormalizeAssetPath(assetPath);
            if (IconByPath.TryGetValue(normalized, out var icon))
            {
                return icon;
            }
            icon = AssetDatabase.GetCachedIcon(normalized) as Texture2D;
            IconByPath[normalized] = icon;
            return icon;
        }
    }
}
