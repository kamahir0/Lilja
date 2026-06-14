using UnityEditor;

namespace Lilja.HierarchyDecorator
{
    public static class HierarchyDecoratorMenu
    {
        private const string ActiveToggleMenuPath = "Lilja/Hierarchy Decorator/Show Active Toggle";
        private const string MissingScriptMenuPath = "Lilja/Hierarchy Decorator/Show Missing Script Warning";

        [MenuItem(ActiveToggleMenuPath, false, 1)]
        private static void ToggleActiveDrawer()
        {
            bool isEnabled = EditorPrefs.GetBool(ActiveToggleDrawer.PrefKey, true);
            EditorPrefs.SetBool(ActiveToggleDrawer.PrefKey, !isEnabled);
            EditorApplication.RepaintHierarchyWindow();
        }

        [MenuItem(ActiveToggleMenuPath, true)]
        private static bool ToggleActiveDrawerValidate()
        {
            Menu.SetChecked(ActiveToggleMenuPath, EditorPrefs.GetBool(ActiveToggleDrawer.PrefKey, true));
            return true;
        }

        [MenuItem(MissingScriptMenuPath, false, 2)]
        private static void ToggleMissingScriptDrawer()
        {
            bool isEnabled = EditorPrefs.GetBool(MissingScriptPingButtonDrawer.PrefKey, true);
            EditorPrefs.SetBool(MissingScriptPingButtonDrawer.PrefKey, !isEnabled);
            EditorApplication.RepaintHierarchyWindow();
        }

        [MenuItem(MissingScriptMenuPath, true)]
        private static bool ToggleMissingScriptDrawerValidate()
        {
            Menu.SetChecked(MissingScriptMenuPath, EditorPrefs.GetBool(MissingScriptPingButtonDrawer.PrefKey, true));
            return true;
        }
    }
}
