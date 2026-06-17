using UnityEditor;

namespace Lilja.HierarchyDecorator
{
    public static class HierarchyDecoratorMenu
    {
        private const string ActiveToggleMenuPath = "Lilja/EditorEx/Hierarchy Decorator/Active Toggle";
        private const string MissingScriptMenuPath = "Lilja/EditorEx/Hierarchy Decorator/Missing Script Warning";

        [MenuItem(ActiveToggleMenuPath, false, 20)]
        private static void ToggleActiveDrawer()
        {
            bool isEnabled = EditorPrefs.GetBool(ActiveToggleDrawer.PrefKey, true);
            EditorPrefs.SetBool(ActiveToggleDrawer.PrefKey, !isEnabled);
            EditorApplication.RepaintHierarchyWindow();
        }

        [MenuItem(ActiveToggleMenuPath, true, 20)]
        private static bool ToggleActiveDrawerValidate()
        {
            Menu.SetChecked(ActiveToggleMenuPath, EditorPrefs.GetBool(ActiveToggleDrawer.PrefKey, true));
            return true;
        }

        [MenuItem(MissingScriptMenuPath, false, 21)]
        private static void ToggleMissingScriptDrawer()
        {
            bool isEnabled = EditorPrefs.GetBool(MissingScriptPingButtonDrawer.PrefKey, true);
            EditorPrefs.SetBool(MissingScriptPingButtonDrawer.PrefKey, !isEnabled);
            EditorApplication.RepaintHierarchyWindow();
        }

        [MenuItem(MissingScriptMenuPath, true, 21)]
        private static bool ToggleMissingScriptDrawerValidate()
        {
            Menu.SetChecked(MissingScriptMenuPath, EditorPrefs.GetBool(MissingScriptPingButtonDrawer.PrefKey, true));
            return true;
        }
    }
}
