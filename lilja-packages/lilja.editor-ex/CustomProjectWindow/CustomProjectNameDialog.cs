using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Lilja.CustomProjectWindow
{
    internal sealed class PopupNameDialog : EditorWindow
    {
        private const float WindowWidth = 300f;
        private const float WindowHeight = 90f;
        private static readonly Vector2 AnchorOffset = new Vector2(12f, 16f);

        private string _message;
        private string _value;
        private Action<string> _onConfirm;
        private bool _focused;

        public static void Show(string title, string message, string defaultValue, Vector2 anchorScreenPosition, Rect fallbackBounds, Action<string> onConfirm)
        {
            var win = CreateInstance<PopupNameDialog>();
            win._message = message;
            win._value = defaultValue;
            win._onConfirm = onConfirm;
            win.titleContent = new GUIContent(title);

            var windowSize = new Vector2(WindowWidth, WindowHeight);
            var initialRect = CalculateInitialRect(anchorScreenPosition, fallbackBounds, windowSize);
            win.minSize = windowSize;
            win.maxSize = windowSize;
            win.position = initialRect;
            win.ShowUtility();
            win.position = initialRect;
        }

        private static Rect CalculateInitialRect(Vector2 anchorScreenPosition, Rect fallbackBounds, Vector2 windowSize)
        {
            var desiredRect = new Rect(anchorScreenPosition + AnchorOffset, windowSize);
            var fittedRect = FitRectToScreen(desiredRect);
            if (!fittedRect.position.Equals(desiredRect.position))
            {
                return new Rect(fittedRect.position, windowSize);
            }

            var xMin = fallbackBounds.xMin;
            var yMin = fallbackBounds.yMin;
            var xMax = Mathf.Max(xMin, fallbackBounds.xMax - windowSize.x);
            var yMax = Mathf.Max(yMin, fallbackBounds.yMax - windowSize.y);
            return new Rect(
                Mathf.Clamp(desiredRect.x, xMin, xMax),
                Mathf.Clamp(desiredRect.y, yMin, yMax),
                windowSize.x,
                windowSize.y);
        }

        private static Rect FitRectToScreen(Rect rect)
        {
            var containerWindowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.ContainerWindow");
            if (containerWindowType == null)
            {
                return rect;
            }

            var methods = containerWindowType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var method in methods)
            {
                if (method.Name != "FitRectToScreen" || method.ReturnType != typeof(Rect))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length == 0 || parameters[0].ParameterType != typeof(Rect))
                {
                    continue;
                }

                var args = new object[parameters.Length];
                args[0] = rect;

                var supported = true;
                for (var i = 1; i < parameters.Length; i++)
                {
                    if (parameters[i].ParameterType != typeof(bool))
                    {
                        supported = false;
                        break;
                    }

                    args[i] = true;
                }

                if (!supported)
                {
                    continue;
                }

                try
                {
                    var fittedRect = (Rect)method.Invoke(null, args);
                    return new Rect(fittedRect.position, rect.size);
                }
                catch
                {
                    return rect;
                }
            }

            return rect;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(_message);
            EditorGUILayout.Space(4);

            GUI.SetNextControlName("NameField");
            _value = EditorGUILayout.TextField(_value);

            if (!_focused)
            {
                EditorGUI.FocusTextInControl("NameField");
                _focused = true;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("キャンセル", GUILayout.Width(80)))
            {
                Close();
                return;
            }

            GUI.enabled = !string.IsNullOrWhiteSpace(_value);
            if (GUILayout.Button("追加", GUILayout.Width(80))
                || (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return))
            {
                _onConfirm?.Invoke(_value.Trim());
                Close();
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }
    }
}
