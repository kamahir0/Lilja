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
        public static void Button(this IDebugUIBuilder builder, string text, ButtonType buttonType = ButtonType.Primary)
        {
            builder.VisualElement(buttonType switch
            {
                ButtonType.Secondary => new DebugSecondaryButton(text),
                ButtonType.Danger => new DebugDangerButton(text),
                _ => new DebugButton(text)
            });
        }

        public static void NavigationButton<T>(this IDebugUIBuilder builder, StyleBackground? icon = null)
            where T : DebugPage, new()
        {
            builder.NavigationButton(typeof(T).Name, () => new T(), icon);
        }

        public static void NavigationButton<T>(this IDebugUIBuilder builder, string pageName, Func<T> pageFactory, StyleBackground? icon = null)
            where T : DebugPage
        {
            builder.RegisterPage(pageName, () => pageFactory());

            var button = new DebugNavigationButton(pageName, icon);
            button.clicked += () =>
            {
                using var evt = DebugNavigateEvent.GetPooled(button, pageName);
                button.SendEvent(evt);
            };
            builder.VisualElement(button);
        }

        public static void NavigationButton(this IDebugUIBuilder builder, string pageName, Action<IDebugUIBuilder> configure, StyleBackground? icon = null)
        {
            builder.NavigationButton(pageName, () => new GenericDebugPage(pageName, configure), icon);
        }

        public static void Foldout(this IDebugUIBuilder builder, string text, Action<IDebugUIBuilder> configure)
        {
            var foldout = new DebugFoldout(text);
            configure(builder.CreateChildBuilder(foldout));
            builder.VisualElement(foldout);
        }

        public static void HorizontalScope(this IDebugUIBuilder builder, Action<IDebugUIBuilder> configure)
        {
            var row = new VisualElement();
            row.AddToClassList(DebugMenuUssClass.HorizontalScope);
            configure(new HorizontalScopeBuilder(builder.CreateChildBuilder(row)));
            builder.VisualElement(row);
        }

        public static void TextField(this IDebugUIBuilder builder, string label, string value, Action<string> onValueChanged = null)
        {
            var field = new DebugTextField(label) { value = value };
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            builder.VisualElement(field);
        }

        public static void IntegerField(this IDebugUIBuilder builder, string label, int value, Action<int> onValueChanged = null)
        {
            var field = new DebugIntegerField(label) { value = value };
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            builder.VisualElement(field);
        }

        public static void LongField(this IDebugUIBuilder builder, string label, long value, Action<long> onValueChanged = null)
        {
            var field = new DebugLongField(label) { value = value };
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            builder.VisualElement(field);
        }

        public static void FloatField(this IDebugUIBuilder builder, string label, float value, Action<float> onValueChanged = null)
        {
            var field = new DebugFloatField(label) { value = value };
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            builder.VisualElement(field);
        }

        public static void DoubleField(this IDebugUIBuilder builder, string label, double value, Action<double> onValueChanged = null)
        {
            var field = new DebugDoubleField(label) { value = value };
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            builder.VisualElement(field);
        }

        public static void Slider(this IDebugUIBuilder builder, string label, float value, float start, float end, Action<float> onValueChanged = null)
        {
            var field = new DebugSlider(label) { value = value, lowValue = start, highValue = end };
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            builder.VisualElement(field);
        }

        public static void SliderInt(this IDebugUIBuilder builder, string label, int value, int start, int end, Action<int> onValueChanged = null)
        {
            var field = new DebugSliderInt(label) { value = value, lowValue = start, highValue = end };
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            builder.VisualElement(field);
        }

        public static void MinMaxSlider(this IDebugUIBuilder builder, string label, UnityEngine.Vector2 value, float min, float max, Action<UnityEngine.Vector2> onValueChanged = null)
        {
            var field = new DebugMinMaxSlider(label) { value = value, lowLimit = min, highLimit = max };
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            builder.VisualElement(field);
        }

        public static void ProgressBar(this IDebugUIBuilder builder, string title, float value, float lowValue = 0f, float highValue = 100f)
        {
            var field = new DebugProgressBar { title = title, value = value, lowValue = lowValue, highValue = highValue };
            builder.VisualElement(field);
        }

        public static void EnumField(this IDebugUIBuilder builder, string label, Enum value, Action<Enum> onValueChanged = null)
        {
            var field = new DebugEnumField(label);
            field.Init(value);
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            builder.VisualElement(field);
        }

        public static void EnumField<T>(this IDebugUIBuilder builder, string label, T value, Action<T> onValueChanged = null) where T : Enum
        {
            var field = new DebugEnumField(label);
            field.Init(value);
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged((T)evt.newValue));
            builder.VisualElement(field);
        }

        public static void Vector2Field(this IDebugUIBuilder builder, string label, UnityEngine.Vector2 value, Action<UnityEngine.Vector2> onValueChanged = null)
        {
            var field = new DebugVector2Field(label) { value = value };
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            builder.VisualElement(field);
        }

        public static void Vector2IntField(this IDebugUIBuilder builder, string label, UnityEngine.Vector2Int value, Action<UnityEngine.Vector2Int> onValueChanged = null)
        {
            var field = new DebugVector2IntField(label) { value = value };
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            builder.VisualElement(field);
        }

        public static void Vector3Field(this IDebugUIBuilder builder, string label, UnityEngine.Vector3 value, Action<UnityEngine.Vector3> onValueChanged = null)
        {
            var field = new DebugVector3Field(label) { value = value };
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            builder.VisualElement(field);
        }

        public static void Vector3IntField(this IDebugUIBuilder builder, string label, UnityEngine.Vector3Int value, Action<UnityEngine.Vector3Int> onValueChanged = null)
        {
            var field = new DebugVector3IntField(label) { value = value };
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            builder.VisualElement(field);
        }

        public static void Vector4Field(this IDebugUIBuilder builder, string label, UnityEngine.Vector4 value, Action<UnityEngine.Vector4> onValueChanged = null)
        {
            var field = new DebugVector4Field(label) { value = value };
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            builder.VisualElement(field);
        }

        public static void RectField(this IDebugUIBuilder builder, string label, UnityEngine.Rect value, Action<UnityEngine.Rect> onValueChanged = null)
        {
            var field = new DebugRectField(label) { value = value };
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            builder.VisualElement(field);
        }

        public static void RectIntField(this IDebugUIBuilder builder, string label, UnityEngine.RectInt value, Action<UnityEngine.RectInt> onValueChanged = null)
        {
            var field = new DebugRectIntField(label) { value = value };
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            builder.VisualElement(field);
        }

        public static void BoundsField(this IDebugUIBuilder builder, string label, UnityEngine.Bounds value, Action<UnityEngine.Bounds> onValueChanged = null)
        {
            var field = new DebugBoundsField(label) { value = value };
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            builder.VisualElement(field);
        }

        public static void BoundsIntField(this IDebugUIBuilder builder, string label, UnityEngine.BoundsInt value, Action<UnityEngine.BoundsInt> onValueChanged = null)
        {
            var field = new DebugBoundsIntField(label) { value = value };
            if (onValueChanged != null) field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            builder.VisualElement(field);
        }

        private sealed class HorizontalScopeBuilder : IDebugUIBuilder
        {
            private readonly IDebugUIBuilder _inner;

            public HorizontalScopeBuilder(IDebugUIBuilder inner) => _inner = inner;

            public void VisualElement(VisualElement visualElement)
            {
                visualElement.style.flexBasis = new StyleLength(new Length(0));
                visualElement.style.flexGrow = 1f;
                _inner.VisualElement(visualElement);
            }

            public IDebugUIBuilder CreateChildBuilder(VisualElement parent)
                => _inner.CreateChildBuilder(parent);

            public void RegisterPage(string pageName, Func<DebugPage> factory)
                => _inner.RegisterPage(pageName, factory);
        }
    }
}
