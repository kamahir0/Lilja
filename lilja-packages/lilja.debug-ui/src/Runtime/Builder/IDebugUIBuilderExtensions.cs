using System;
using UnityEngine.UIElements;

namespace Lilja.DebugUI
{
    /// <summary>
    /// ボタンの系統を表す列挙型
    /// </summary>
    public enum ButtonType
    {
        /// <summary>プライマリ（決定・適用など）: 青</summary>
        Primary,
        /// <summary>セカンダリ（キャンセル・戻るなど）: 白</summary>
        Secondary,
        /// <summary>デンジャー（削除・リセットなど破壊的操作）: 赤</summary>
        Danger,
    }

    public static class IDebugUIBuilderExtensions
    {
        public static Button Button(this IDebugUIBuilder builder, string text, ButtonType buttonType = ButtonType.Primary)
        {
            return builder.Button(text, null, buttonType);
        }

        public static Button Button(this IDebugUIBuilder builder, string text, Action onClick, ButtonType buttonType = ButtonType.Primary)
        {
            Button button = buttonType switch
            {
                ButtonType.Secondary => new DebugSecondaryButton(text),
                ButtonType.Danger => new DebugDangerButton(text),
                _ => new DebugButton(text)
            };

            if (onClick != null)
            {
                button.clicked += onClick;
            }

            return builder.VisualElement(button);
        }

        public static DebugButton PrimaryButton(this IDebugUIBuilder builder, string text, Action onClick = null)
        {
            var button = new DebugButton(text);
            RegisterClick(button, onClick);
            return builder.VisualElement(button);
        }

        public static DebugSecondaryButton SecondaryButton(this IDebugUIBuilder builder, string text, Action onClick = null)
        {
            var button = new DebugSecondaryButton(text);
            RegisterClick(button, onClick);
            return builder.VisualElement(button);
        }

        public static DebugDangerButton DangerButton(this IDebugUIBuilder builder, string text, Action onClick = null)
        {
            var button = new DebugDangerButton(text);
            RegisterClick(button, onClick);
            return builder.VisualElement(button);
        }

        public static DebugLabel Label(this IDebugUIBuilder builder, string text = "")
        {
            return builder.VisualElement(new DebugLabel(text));
        }

        public static DebugNavigationButton NavigationButton<T>(this IDebugUIBuilder builder, StyleBackground? icon = null)
            where T : DebugPage, new()
        {
            return builder.NavigationButton(typeof(T).Name, () => new T(), icon);
        }

        public static DebugNavigationButton NavigationButton<T>(this IDebugUIBuilder builder, string pageName, Func<T> pageFactory, StyleBackground? icon = null)
            where T : DebugPage
        {
            if (pageFactory == null) throw new ArgumentNullException(nameof(pageFactory));

            builder.RegisterPage(pageName, () => pageFactory());

            var button = new DebugNavigationButton(pageName, icon);
            button.clicked += () =>
            {
                using var evt = DebugNavigateEvent.GetPooled(button, pageName);
                button.SendEvent(evt);
            };
            return builder.VisualElement(button);
        }

        public static DebugNavigationButton NavigationButton(this IDebugUIBuilder builder, string pageName, Action<IDebugUIBuilder> configure, StyleBackground? icon = null)
        {
            return builder.NavigationButton(pageName, () => new GenericDebugPage(pageName, configure), icon);
        }

        public static DebugNavigationButton TempNavigationButton(this IDebugUIBuilder builder, string pageName, Action<IDebugUIBuilder> configure, StyleBackground? icon = null)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            var button = new DebugNavigationButton(pageName, icon);
            button.clicked += () => DebugMenu.NavigateToTemp(pageName, configure);
            return builder.VisualElement(button);
        }

        public static DebugFoldout Foldout(this IDebugUIBuilder builder, string text, Action<IDebugUIBuilder> configure = null)
        {
            var foldout = new DebugFoldout(text);
            configure?.Invoke(builder.CreateChildBuilder(foldout));
            return builder.VisualElement(foldout);
        }

        public static VirtualFoldout VirtualFoldout(this IDebugUIBuilder builder, string text)
        {
            return builder.VisualElement(new VirtualFoldout(text));
        }

        public static VisualElement HorizontalScope(this IDebugUIBuilder builder, Action<IDebugUIBuilder> configure)
        {
            var row = new VisualElement();
            row.AddToClassList(DebugMenuUssClass.HorizontalScope);
            configure?.Invoke(new HorizontalScopeBuilder(builder.CreateChildBuilder(row)));
            return builder.VisualElement(row);
        }

        public static DebugTextField TextField(this IDebugUIBuilder builder, string label, string value = "", Action<string> onValueChanged = null)
        {
            var field = new DebugTextField(label) { value = value };
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            return builder.VisualElement(field);
        }

        public static DebugTextField TextField(this IDebugUIBuilder builder, string label, Action<string> onValueChanged)
        {
            return builder.TextField(label, string.Empty, onValueChanged);
        }

        public static DebugIntegerField IntegerField(this IDebugUIBuilder builder, string label, int value = 0, Action<int> onValueChanged = null)
        {
            var field = new DebugIntegerField(label) { value = value };
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            return builder.VisualElement(field);
        }

        public static DebugIntegerField IntegerField(this IDebugUIBuilder builder, string label, Action<int> onValueChanged)
        {
            return builder.IntegerField(label, 0, onValueChanged);
        }

        public static DebugLongField LongField(this IDebugUIBuilder builder, string label, long value = 0L, Action<long> onValueChanged = null)
        {
            var field = new DebugLongField(label) { value = value };
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            return builder.VisualElement(field);
        }

        public static DebugLongField LongField(this IDebugUIBuilder builder, string label, Action<long> onValueChanged)
        {
            return builder.LongField(label, 0L, onValueChanged);
        }

        public static DebugFloatField FloatField(this IDebugUIBuilder builder, string label, float value = 0f, Action<float> onValueChanged = null)
        {
            var field = new DebugFloatField(label) { value = value };
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            return builder.VisualElement(field);
        }

        public static DebugFloatField FloatField(this IDebugUIBuilder builder, string label, Action<float> onValueChanged)
        {
            return builder.FloatField(label, 0f, onValueChanged);
        }

        public static DebugDoubleField DoubleField(this IDebugUIBuilder builder, string label, double value = 0d, Action<double> onValueChanged = null)
        {
            var field = new DebugDoubleField(label) { value = value };
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            return builder.VisualElement(field);
        }

        public static DebugDoubleField DoubleField(this IDebugUIBuilder builder, string label, Action<double> onValueChanged)
        {
            return builder.DoubleField(label, 0d, onValueChanged);
        }

        public static DebugSlider Slider(this IDebugUIBuilder builder, string label, float value, float start, float end, Action<float> onValueChanged = null)
        {
            var field = new DebugSlider(label) { value = value, lowValue = start, highValue = end };
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            return builder.VisualElement(field);
        }

        public static DebugSliderInt SliderInt(this IDebugUIBuilder builder, string label, int value, int start, int end, Action<int> onValueChanged = null)
        {
            var field = new DebugSliderInt(label) { value = value, lowValue = start, highValue = end };
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            return builder.VisualElement(field);
        }

        public static DebugMinMaxSlider MinMaxSlider(this IDebugUIBuilder builder, string label, UnityEngine.Vector2 value, float min, float max, Action<UnityEngine.Vector2> onValueChanged = null)
        {
            var field = new DebugMinMaxSlider(label) { value = value, lowLimit = min, highLimit = max };
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            return builder.VisualElement(field);
        }

        public static DebugProgressBar ProgressBar(this IDebugUIBuilder builder, string title, float value, float lowValue = 0f, float highValue = 100f)
        {
            var field = new DebugProgressBar { title = title, value = value, lowValue = lowValue, highValue = highValue };
            return builder.VisualElement(field);
        }

        public static DebugEnumField EnumField(this IDebugUIBuilder builder, string label, Enum value, Action<Enum> onValueChanged = null)
        {
            var field = new DebugEnumField(label);
            field.Init(value);
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            return builder.VisualElement(field);
        }

        public static DebugEnumField EnumField<T>(this IDebugUIBuilder builder, string label, T value, Action<T> onValueChanged = null) where T : Enum
        {
            var field = new DebugEnumField(label);
            field.Init(value);
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged((T)evt.newValue));
            return builder.VisualElement(field);
        }

        public static DebugVector2Field Vector2Field(this IDebugUIBuilder builder, string label, UnityEngine.Vector2 value, Action<UnityEngine.Vector2> onValueChanged = null)
        {
            var field = new DebugVector2Field(label) { value = value };
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            return builder.VisualElement(field);
        }

        public static DebugVector2IntField Vector2IntField(this IDebugUIBuilder builder, string label, UnityEngine.Vector2Int value, Action<UnityEngine.Vector2Int> onValueChanged = null)
        {
            var field = new DebugVector2IntField(label) { value = value };
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            return builder.VisualElement(field);
        }

        public static DebugVector3Field Vector3Field(this IDebugUIBuilder builder, string label, UnityEngine.Vector3 value, Action<UnityEngine.Vector3> onValueChanged = null)
        {
            var field = new DebugVector3Field(label) { value = value };
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            return builder.VisualElement(field);
        }

        public static DebugVector3IntField Vector3IntField(this IDebugUIBuilder builder, string label, UnityEngine.Vector3Int value, Action<UnityEngine.Vector3Int> onValueChanged = null)
        {
            var field = new DebugVector3IntField(label) { value = value };
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            return builder.VisualElement(field);
        }

        public static DebugVector4Field Vector4Field(this IDebugUIBuilder builder, string label, UnityEngine.Vector4 value, Action<UnityEngine.Vector4> onValueChanged = null)
        {
            var field = new DebugVector4Field(label) { value = value };
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            return builder.VisualElement(field);
        }

        public static DebugRectField RectField(this IDebugUIBuilder builder, string label, UnityEngine.Rect value, Action<UnityEngine.Rect> onValueChanged = null)
        {
            var field = new DebugRectField(label) { value = value };
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            return builder.VisualElement(field);
        }

        public static DebugRectIntField RectIntField(this IDebugUIBuilder builder, string label, UnityEngine.RectInt value, Action<UnityEngine.RectInt> onValueChanged = null)
        {
            var field = new DebugRectIntField(label) { value = value };
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            return builder.VisualElement(field);
        }

        public static DebugBoundsField BoundsField(this IDebugUIBuilder builder, string label, UnityEngine.Bounds value, Action<UnityEngine.Bounds> onValueChanged = null)
        {
            var field = new DebugBoundsField(label) { value = value };
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            return builder.VisualElement(field);
        }

        public static DebugBoundsIntField BoundsIntField(this IDebugUIBuilder builder, string label, UnityEngine.BoundsInt value, Action<UnityEngine.BoundsInt> onValueChanged = null)
        {
            var field = new DebugBoundsIntField(label) { value = value };
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            return builder.VisualElement(field);
        }

        private static void RegisterClick(Button button, Action onClick)
        {
            if (onClick != null)
            {
                button.clicked += onClick;
            }
        }

        private sealed class HorizontalScopeBuilder : IDebugUIBuilder
        {
            private readonly IDebugUIBuilder _inner;

            public HorizontalScopeBuilder(IDebugUIBuilder inner) => _inner = inner;

            public T VisualElement<T>(T visualElement)
                where T : VisualElement
            {
                visualElement.style.flexBasis = new StyleLength(new Length(0));
                visualElement.style.flexGrow = 1f;
                return _inner.VisualElement(visualElement);
            }

            public IDebugUIBuilder CreateChildBuilder(VisualElement parent)
                => _inner.CreateChildBuilder(parent);

            public void RegisterPage(string pageName, Func<DebugPage> factory)
                => _inner.RegisterPage(pageName, factory);
        }
    }
}
