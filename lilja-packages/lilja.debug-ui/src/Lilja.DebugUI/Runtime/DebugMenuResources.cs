using UnityEngine;
using UnityEngine.UIElements;

namespace Lilja.DebugUI
{
    internal static class DebugMenuResources
    {
        private const string DebugMenuVisualTreePath = "DebugMenu/DebugMenu";
        private const string DefaultPanelSettingsPath = "DebugMenu/DebugMenuPanelSettings";
        private const string OpenButtonPanelSettingsPath = "DebugMenu/DebugMenuOpenButtonPanelSettings";
        private const string OpenButtonVisualTreePath = "DebugMenu/DebugMenuOpenButton";

        internal static VisualTreeAsset LoadDebugMenuVisualTree()
            => Resources.Load<VisualTreeAsset>(DebugMenuVisualTreePath);

        internal static PanelSettings LoadDefaultPanelSettings()
            => Resources.Load<PanelSettings>(DefaultPanelSettingsPath);

        internal static PanelSettings LoadOpenButtonPanelSettings()
            => Resources.Load<PanelSettings>(OpenButtonPanelSettingsPath);


        internal static VisualTreeAsset LoadOpenButtonVisualTree()
            => Resources.Load<VisualTreeAsset>(OpenButtonVisualTreePath);
    }
}
