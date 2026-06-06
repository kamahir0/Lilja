using UnityEditor;

namespace Lilja.CustomProjectWindow
{
    internal sealed class CustomProjectAssetPostprocessor : AssetPostprocessor
    {
#pragma warning disable IDE0051, IDE0040
        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            var window = EditorWindow.HasOpenInstances<CustomProjectWindow>()
                ? EditorWindow.GetWindow<CustomProjectWindow>(false, null, false)
                : null;

            if (window == null || window.Model == null)
            {
                return;
            }

            var changed = false;

            for (var i = 0; i < movedAssets.Length; i++)
            {
                changed |= window.Model.HandleAssetMoved(movedFromAssetPaths[i], movedAssets[i], save: false);
            }

            foreach (var deleted in deletedAssets)
            {
                changed |= window.Model.HandleAssetDeleted(deleted, save: false);
            }

            if (importedAssets.Length > 0)
            {
                changed |= window.Model.HandleAssetsImported(importedAssets, save: false);
            }

            if (changed)
            {
                window.Model.Save();
            }

            if (changed)
            {
                window.RequestRefresh();
            }
        }
#pragma warning restore IDE0051, IDE0040
    }
}
