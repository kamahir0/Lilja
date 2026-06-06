using UnityEditor;
using UnityEngine;

namespace Lilja.CustomProjectWindow
{
    internal sealed class CustomProjectKeyConfigWindow : EditorWindow
    {
#if UNITY_EDITOR_OSX
        private static readonly string[] ModifierLabels = { "Option (Alt)", "Shift", "Command (⌘)" };
        private const string ShortcutHintFormat = "{0}+右クリックでクイック追加メニューを表示します。";
#else
        private static readonly string[] ModifierLabels = { "Alt", "Shift", "Ctrl" };
        private const string ShortcutHintFormat = "{0}+右クリックでクイック追加メニューを表示します。";
#endif

        private int _selectedIndex;

        internal static void Open()
        {
            var win = GetWindow<CustomProjectKeyConfigWindow>(true, "クイック追加 キー設定");
            win.minSize = new Vector2(340f, 110f);
            win.maxSize = new Vector2(500f, 110f);
            win._selectedIndex = (int)CustomProjectQuickAdd.Modifier;
            win.ShowUtility();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("クイック追加のトリガーとなる修飾キー：");
            EditorGUILayout.Space(4f);

            var newIndex = EditorGUILayout.Popup("修飾キー", _selectedIndex, ModifierLabels);
            if (newIndex != _selectedIndex)
            {
                _selectedIndex = newIndex;
                CustomProjectQuickAdd.Modifier = (QuickAddModifier)_selectedIndex;
            }

            EditorGUILayout.Space(6f);

            var hint = string.Format(ShortcutHintFormat, ModifierLabels[_selectedIndex]);
            EditorGUILayout.HelpBox(hint, MessageType.Info);
        }
    }
}
