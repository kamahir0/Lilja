using UnityEngine;
using UnityEngine.UIElements;

namespace Lilja.DebugUI
{
    internal static class DebugMenuResources
    {
        private const string DefaultPanelSettingsPath = "DebugMenu/DebugMenuPanelSettings";
        private const string OpenButtonVisualTreePath = "DebugMenu/DebugMenuOpenButton";

        internal static PanelSettings LoadDefaultPanelSettings()
            => Resources.Load<PanelSettings>(DefaultPanelSettingsPath);

        internal static VisualTreeAsset LoadOpenButtonVisualTree()
            => Resources.Load<VisualTreeAsset>(OpenButtonVisualTreePath);
    }
}
