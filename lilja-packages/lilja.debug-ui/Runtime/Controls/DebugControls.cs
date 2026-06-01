using System;
using UnityEngine.UIElements;

namespace Lilja.DebugUI
{
    public static class DebugMenuUssClass
    {
        public const string ControlSize = "c-control-size";
        internal const string Input = "c-input";
        internal const string HorizontalScope = "c-h-scope";

        internal static class Button
        {
            internal const string Root = "c-button";
            internal const string Primary = Root + "--primary";
            internal const string Secondary = Root + "--secondary";
            internal const string Danger = Root + "--danger";
        }

        internal static class NavigationButton
        {
            internal const string Root = "c-nav-button";
            internal const string Label = Root + "__label";
            internal const string LeftIcon = Root + "__left-icon";
            internal const string Icon = Root + "__icon";
        }

        internal static class Label
        {
            internal const string Root = "c-label";
        }

        internal static class Foldout
        {
            internal const string Root = "c-foldout";
        }

        internal static class RadioButtonGroup
        {
            internal const string Root = "c-radio-group";
        }

        internal static class ToggleGroup
        {
            internal const string Root = "c-toggle-group";
            internal const string LabelElement = Root + "__label";
            internal const string Items = Root + "__items";
        }

        internal static class ToggleGroupItem
        {
            internal const string Root = "c-toggle-group-item";
            internal const string Checked = Root + "--checked";
            internal const string Checkbox = Root + "__checkbox";
            internal const string LabelElement = Root + "__label";
        }
    }

    /// <summary>
    /// ナビゲーションボタン（iOS設定アプリ風）
    /// 背景色はページと同一、上下のセパレーターで矩形を可視化する。
    /// テキストラベルと右端矢印アイコンを子要素として保持する。
    /// </summary>
    [UxmlElement]
    public partial class DebugNavigationButton : Button, IDebugUI
    {
        public DebugNavigationButton() : this(string.Empty) { }

        public DebugNavigationButton(string text, StyleBackground? leftIcon = null)
        {
            AddToClassList(DebugMenuUssClass.ControlSize);
            AddToClassList(DebugMenuUssClass.NavigationButton.Root);

            if (leftIcon.HasValue)
            {
                var iconLeft = new VisualElement();
                iconLeft.AddToClassList(DebugMenuUssClass.NavigationButton.LeftIcon);
                iconLeft.style.backgroundImage = leftIcon.Value;
                iconLeft.pickingMode = PickingMode.Ignore;
                Add(iconLeft);
            }

            var label = new Label(text);
            label.AddToClassList(DebugMenuUssClass.NavigationButton.Label);
            Add(label);

            var icon = new VisualElement();
            icon.AddToClassList(DebugMenuUssClass.NavigationButton.Icon);
            Add(icon);

            ButtonInteractionHelper.Register(this);
        }
    }

    /// <summary>
    /// デバッグメニュー用のプライマリボタン
    /// </summary>
    [UxmlElement]
    public partial class DebugButton : Button, IDebugUI
    {
        public DebugButton() : this(string.Empty) { }

        public DebugButton(string text)
        {
            this.text = text;
            AddToClassList(DebugMenuUssClass.ControlSize);
            AddToClassList(DebugMenuUssClass.Button.Root);
            AddToClassList(DebugMenuUssClass.Button.Primary);
            ButtonInteractionHelper.Register(this);
        }
    }

    /// <summary>
    /// デバッグメニュー用のセカンダリボタン
    /// </summary>
    [UxmlElement]
    public partial class DebugSecondaryButton : Button, IDebugUI
    {
        public DebugSecondaryButton() : this(string.Empty) { }

        public DebugSecondaryButton(string text)
        {
            this.text = text;
            AddToClassList(DebugMenuUssClass.ControlSize);
            AddToClassList(DebugMenuUssClass.Button.Root);
            AddToClassList(DebugMenuUssClass.Button.Secondary);
            ButtonInteractionHelper.Register(this);
        }
    }

    /// <summary>
    /// デバッグメニュー用のデンジャーボタン（削除・リセットなど破壊的操作用）
    /// </summary>
    [UxmlElement]
    public partial class DebugDangerButton : Button, IDebugUI
    {
        public DebugDangerButton() : this(string.Empty) { }

        public DebugDangerButton(string text)
        {
            this.text = text;
            AddToClassList(DebugMenuUssClass.ControlSize);
            AddToClassList(DebugMenuUssClass.Button.Root);
            AddToClassList(DebugMenuUssClass.Button.Danger);
            ButtonInteractionHelper.Register(this);
        }
    }

    /// <summary>
    /// デバッグメニュー用のテキストフィールド
    /// </summary>
    [UxmlElement]
    public partial class DebugTextField : TextField, IDebugUI
    {
        public DebugTextField() : this(string.Empty) { }

        public DebugTextField(string label) : base(label)
        {
            AddToClassList(DebugMenuUssClass.ControlSize);
            AddToClassList(DebugMenuUssClass.Input);
        }
    }

    /// <summary>
    /// デバッグメニュー用のラベル
    /// </summary>
    [UxmlElement]
    public partial class DebugLabel : Label, IDebugUI
    {
        public DebugLabel() : this(string.Empty) { }

        public DebugLabel(string text) : base(text)
        {
            AddToClassList(DebugMenuUssClass.ControlSize);
            AddToClassList(DebugMenuUssClass.Label.Root);
        }
    }

    /// <summary>
    /// デバッグメニュー用のフォールドアウト
    /// </summary>
    [UxmlElement]
    public partial class DebugFoldout : Foldout, IDebugUI
    {
        public DebugFoldout() : this(string.Empty) { }

        public DebugFoldout(string label)
        {
            this.name = label;
            text = label;
            value = false;
            AddToClassList(DebugMenuUssClass.ControlSize);
            AddToClassList(DebugMenuUssClass.Foldout.Root);
        }

        /// <summary>
        /// フォールドアウト内の末尾にUIを動的追加する。
        /// 返り値を Dispose するとUIが削除される。
        /// </summary>
        public IDisposable AddDebugUI(Action<IDebugUIBuilder> configure)
        {
            var wrapper = new VisualElement();
            configure(new DebugUIBuilder(wrapper, DebugMenu.CurrentCache));
            Add(wrapper);
            OnDynamicChildAdded(wrapper);
            return new DelegateDisposable(() =>
            {
                OnDynamicChildRemoved(wrapper);
                wrapper.RemoveFromHierarchy();
            });
        }

        protected virtual void OnDynamicChildAdded(VisualElement wrapper) { }
        protected virtual void OnDynamicChildRemoved(VisualElement wrapper) { }
    }

    /// <summary>
    /// デバッグメニュー用のラジオボタングループ
    /// </summary>
    [UxmlElement]
    public partial class DebugRadioButtonGroup : RadioButtonGroup, IDebugUI
    {
        public DebugRadioButtonGroup() : this(string.Empty) { }

        public DebugRadioButtonGroup(string label) : base(label)
        {
            AddToClassList(DebugMenuUssClass.ControlSize);
            AddToClassList(DebugMenuUssClass.RadioButtonGroup.Root);
        }
    }

    /// <summary>
    /// デバッグメニュー用の整数フィールド
    /// </summary>
    [UxmlElement]
    public partial class DebugIntegerField : IntegerField, IDebugUI
    {
        public DebugIntegerField() : this(string.Empty) { }

        public DebugIntegerField(string label) : base(label)
        {
            AddToClassList(DebugMenuUssClass.ControlSize);
            AddToClassList(DebugMenuUssClass.Input);
        }
    }

    /// <summary>
    /// デバッグメニュー用の浮動小数点フィールド
    /// </summary>
    [UxmlElement]
    public partial class DebugFloatField : FloatField, IDebugUI
    {
        public DebugFloatField() : this(string.Empty) { }

        public DebugFloatField(string label) : base(label)
        {
            AddToClassList(DebugMenuUssClass.ControlSize);
            AddToClassList(DebugMenuUssClass.Input);
        }
    }

    /// <summary>
    /// デバッグメニュー用のスライダー
    /// </summary>
    [UxmlElement]
    public partial class DebugSlider : Slider, IDebugUI
    {
        public DebugSlider() : this(string.Empty) { }

        public DebugSlider(string label) : base(label)
        {
            AddToClassList(DebugMenuUssClass.ControlSize);
            AddToClassList(DebugMenuUssClass.Input);
        }
    }

    /// <summary>
    /// デバッグメニュー用の整数スライダー
    /// </summary>
    [UxmlElement]
    public partial class DebugSliderInt : SliderInt, IDebugUI
    {
        public DebugSliderInt() : this(string.Empty) { }

        public DebugSliderInt(string label) : base(label)
        {
            AddToClassList(DebugMenuUssClass.ControlSize);
            AddToClassList(DebugMenuUssClass.Input);
        }
    }

    /// <summary>
    /// デバッグメニュー用の最小最大スライダー
    /// </summary>
    [UxmlElement]
    public partial class DebugMinMaxSlider : MinMaxSlider, IDebugUI
    {
        public DebugMinMaxSlider() : this(string.Empty) { }

        public DebugMinMaxSlider(string label) : base(label)
        {
            AddToClassList(DebugMenuUssClass.ControlSize);
            AddToClassList(DebugMenuUssClass.Input);
        }
    }

    /// <summary>
    /// デバッグメニュー用のプログレスバー
    /// </summary>
    [UxmlElement]
    public partial class DebugProgressBar : ProgressBar, IDebugUI
    {
        public DebugProgressBar()
        {
            AddToClassList(DebugMenuUssClass.ControlSize);
            AddToClassList(DebugMenuUssClass.Input);
        }
    }

    /// <summary>
    /// デバッグメニュー用の列挙型フィールド
    /// </summary>
    [UxmlElement]
    public partial class DebugEnumField : EnumField, IDebugUI
    {
        public DebugEnumField() : this(string.Empty) { }

        public DebugEnumField(string label) : base(label)
        {
            AddToClassList(DebugMenuUssClass.ControlSize);
            AddToClassList(DebugMenuUssClass.Input);
        }
    }

    /// <summary>
    /// デバッグメニュー用のロング整数フィールド
    /// </summary>
    [UxmlElement]
    public partial class DebugLongField : LongField, IDebugUI
    {
        public DebugLongField() : this(string.Empty) { }

        public DebugLongField(string label) : base(label)
        {
            AddToClassList(DebugMenuUssClass.ControlSize);
            AddToClassList(DebugMenuUssClass.Input);
        }
    }

    /// <summary>
    /// デバッグメニュー用の倍精度浮動小数点フィールド
    /// </summary>
    [UxmlElement]
    public partial class DebugDoubleField : DoubleField, IDebugUI
    {
        public DebugDoubleField() : this(string.Empty) { }

        public DebugDoubleField(string label) : base(label)
        {
            AddToClassList(DebugMenuUssClass.ControlSize);
            AddToClassList(DebugMenuUssClass.Input);
        }
    }

    /// <summary>
    /// デバッグメニュー用の Vector2 フィールド
    /// </summary>
    [UxmlElement]
    public partial class DebugVector2Field : Vector2Field, IDebugUI
    {
        public DebugVector2Field() : this(string.Empty) { }

        public DebugVector2Field(string label) : base(label)
        {
            AddToClassList(DebugMenuUssClass.ControlSize);
            AddToClassList(DebugMenuUssClass.Input);
        }
    }

    /// <summary>
    /// デバッグメニュー用の Vector2Int フィールド
    /// </summary>
    [UxmlElement]
    public partial class DebugVector2IntField : Vector2IntField, IDebugUI
    {
        public DebugVector2IntField() : this(string.Empty) { }

        public DebugVector2IntField(string label) : base(label)
        {
            AddToClassList(DebugMenuUssClass.ControlSize);
            AddToClassList(DebugMenuUssClass.Input);
        }
    }

    /// <summary>
    /// デバッグメニュー用の Vector3 フィールド
    /// </summary>
    [UxmlElement]
    public partial class DebugVector3Field : Vector3Field, IDebugUI
    {
        public DebugVector3Field() : this(string.Empty) { }

        public DebugVector3Field(string label) : base(label)
        {
            AddToClassList(DebugMenuUssClass.ControlSize);
            AddToClassList(DebugMenuUssClass.Input);
        }
    }

    /// <summary>
    /// デバッグメニュー用の Vector3Int フィールド
    /// </summary>
    [UxmlElement]
    public partial class DebugVector3IntField : Vector3IntField, IDebugUI
    {
        public DebugVector3IntField() : this(string.Empty) { }

        public DebugVector3IntField(string label) : base(label)
        {
            AddToClassList(DebugMenuUssClass.ControlSize);
            AddToClassList(DebugMenuUssClass.Input);
        }
    }

    /// <summary>
    /// デバッグメニュー用の Vector4 フィールド
    /// </summary>
    [UxmlElement]
    public partial class DebugVector4Field : Vector4Field, IDebugUI
    {
        public DebugVector4Field() : this(string.Empty) { }

        public DebugVector4Field(string label) : base(label)
        {
            AddToClassList(DebugMenuUssClass.ControlSize);
            AddToClassList(DebugMenuUssClass.Input);
        }
    }

    /// <summary>
    /// デバッグメニュー用の Rect フィールド
    /// </summary>
    [UxmlElement]
    public partial class DebugRectField : RectField, IDebugUI
    {
        public DebugRectField() : this(string.Empty) { }

        public DebugRectField(string label) : base(label)
        {
            AddToClassList(DebugMenuUssClass.ControlSize);
            AddToClassList(DebugMenuUssClass.Input);
        }
    }

    /// <summary>
    /// デバッグメニュー用の RectInt フィールド
    /// </summary>
    [UxmlElement]
    public partial class DebugRectIntField : RectIntField, IDebugUI
    {
        public DebugRectIntField() : this(string.Empty) { }

        public DebugRectIntField(string label) : base(label)
        {
            AddToClassList(DebugMenuUssClass.ControlSize);
            AddToClassList(DebugMenuUssClass.Input);
        }
    }

    /// <summary>
    /// デバッグメニュー用の Bounds フィールド
    /// </summary>
    [UxmlElement]
    public partial class DebugBoundsField : BoundsField, IDebugUI
    {
        public DebugBoundsField() : this(string.Empty) { }

        public DebugBoundsField(string label) : base(label)
        {
            AddToClassList(DebugMenuUssClass.ControlSize);
            AddToClassList(DebugMenuUssClass.Input);
        }
    }

    /// <summary>
    /// デバッグメニュー用の BoundsInt フィールド
    /// </summary>
    [UxmlElement]
    public partial class DebugBoundsIntField : BoundsIntField, IDebugUI
    {
        public DebugBoundsIntField() : this(string.Empty) { }

        public DebugBoundsIntField(string label) : base(label)
        {
            AddToClassList(DebugMenuUssClass.ControlSize);
            AddToClassList(DebugMenuUssClass.Input);
        }
    }
}
