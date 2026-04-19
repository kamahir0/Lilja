using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lilja.DebugUI
{
    /// USS カスタムプロパティ --hover-color / --active-color の C# バインディング。
    /// ButtonInteractionHelper と VisualElementInteractionHelper の両方から使用する。
    internal static class InteractionColorProperties
    {
        internal static readonly CustomStyleProperty<Color> HoverColor = new("--hover-color");
        internal static readonly CustomStyleProperty<Color> ActiveColor = new("--active-color");
    }

    /// <summary>
    /// Clickable のサブクラス。ProcessDownEvent / ProcessUpEvent をフックして
    /// ボタン押下・解放の通知を外部に公開する。
    /// Clickable は PointerDownEvent 内で StopImmediatePropagation() を呼ぶため
    /// 通常の RegisterCallback では押下を検知できず、このサブクラスで対処する。
    /// </summary>
    internal class InteractiveClickable : Clickable
    {
        public event Action OnPressed;
        public event Action OnReleased;

        public InteractiveClickable() : base((Action)null) { }

        protected override void ProcessDownEvent(EventBase evt, Vector2 localPosition, int pointerId)
        {
            OnPressed?.Invoke();
            base.ProcessDownEvent(evt, localPosition, pointerId);
        }

        protected override void ProcessUpEvent(EventBase evt, Vector2 localPosition, int pointerId)
        {
            base.ProcessUpEvent(evt, localPosition, pointerId);
            OnReleased?.Invoke();
        }
    }

    /// <summary>
    /// ボタンのホバー・アクティブ状態の背景色をインラインスタイルで管理するヘルパー。
    /// Unity のデフォルトテーマが background-color を上書きするため USS では制御できず、
    /// C# から直接 style.backgroundColor をセットして対応する。
    /// ホバー色・アクティブ色は USS カスタムプロパティ --hover-color / --active-color で定義する。
    /// 呼び出し前に button.clicked への購読を済ませないこと（clickable 置き換えで失われる）。
    /// </summary>
    internal static class ButtonInteractionHelper
    {
        internal static void Register(Button button)
        {
            Color hoverColor = default;
            Color activeColor = default;
            bool isOver = false;

            // Clickable を置き換えてプレス/リリースをフック
            var clickable = new InteractiveClickable();
            button.clickable = clickable;

            button.RegisterCallback<CustomStyleResolvedEvent>(e =>
            {
                e.customStyle.TryGetValue(InteractionColorProperties.HoverColor, out hoverColor);
                e.customStyle.TryGetValue(InteractionColorProperties.ActiveColor, out activeColor);
            });

            button.RegisterCallback<PointerEnterEvent>(_ =>
            {
                isOver = true;
                button.style.backgroundColor = hoverColor;
            });

            button.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                isOver = false;
                button.style.backgroundColor = StyleKeyword.Null;
            });

            clickable.OnPressed += () =>
            {
                button.style.backgroundColor = activeColor;
            };

            clickable.OnReleased += () =>
            {
                button.style.backgroundColor = isOver ? (StyleColor)hoverColor : StyleKeyword.Null;
            };

            // ページ遷移などで要素がパネルから切り離された場合、PointerLeave が届かず
            // isOver=true・hoverColor がインラインスタイルに残るため、ここでリセットする。
            button.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                isOver = false;
                button.style.backgroundColor = StyleKeyword.Null;
            });
        }
    }

    /// <summary>
    /// VisualElement のホバー・アクティブ状態の背景色をインラインスタイルで管理するヘルパー。
    /// Button 以外の VisualElement（RepeatButton、Slider ドラッガーなど）に使用する。
    /// ホバー色・アクティブ色は USS カスタムプロパティ --hover-color / --active-color で定義する。
    /// </summary>
    public static class VisualElementInteractionHelper
    {
        // ホバー + クリック（PointerDown/Up でクリックを検知。RepeatButton など用）
        public static void Register(VisualElement element)
        {
            Color hoverColor = default;
            Color activeColor = default;
            bool isOver = false;
            bool isPressed = false;

            element.RegisterCallback<CustomStyleResolvedEvent>(e =>
            {
                e.customStyle.TryGetValue(InteractionColorProperties.HoverColor, out hoverColor);
                e.customStyle.TryGetValue(InteractionColorProperties.ActiveColor, out activeColor);
            });

            element.RegisterCallback<PointerEnterEvent>(_ =>
            {
                isOver = true;
                element.style.backgroundColor = isPressed ? activeColor : hoverColor;
            });

            element.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                isOver = false;
                element.style.backgroundColor = StyleKeyword.Null;
            });

            element.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                isOver = false;
                isPressed = false;
                element.style.backgroundColor = StyleKeyword.Null;
            });

            element.RegisterCallback<PointerDownEvent>(_ =>
            {
                isPressed = true;
                element.style.backgroundColor = activeColor;
            });

            element.RegisterCallback<PointerUpEvent>(_ =>
            {
                isPressed = false;
                element.style.backgroundColor = isOver ? (StyleColor)hoverColor : StyleKeyword.Null;
            });
        }

        // ホバー + クリック（Slider ドラッガー専用）
        // ClampedDragger が TrickleDown で PointerDown を処理するため、
        // 親 slider に TrickleDown 登録して先に拾う。
        internal static void RegisterSliderDragger(VisualElement slider, VisualElement dragger) // スクロールバー内部専用
        {
            Color hoverColor = default;
            Color activeColor = default;
            bool isOver = false;
            bool isPressed = false;

            dragger.RegisterCallback<CustomStyleResolvedEvent>(e =>
            {
                e.customStyle.TryGetValue(InteractionColorProperties.HoverColor, out hoverColor);
                e.customStyle.TryGetValue(InteractionColorProperties.ActiveColor, out activeColor);
            });

            dragger.RegisterCallback<PointerEnterEvent>(_ =>
            {
                isOver = true;
                dragger.style.backgroundColor = isPressed ? activeColor : hoverColor;
            });

            dragger.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                isOver = false;
                if (!isPressed)
                    dragger.style.backgroundColor = StyleKeyword.Null;
            });

            // 押下: TrickleDown で親から登録し ClampedDragger より先に検知
            slider.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.target == dragger)
                {
                    isPressed = true;
                    dragger.style.backgroundColor = activeColor;
                }
            }, TrickleDown.TrickleDown);

            // 解放: PointerUp は内部処理でブロックされるため PointerCaptureOutEvent を使う。
            // どちらの要素がキャプチャしているか不明なので両方に登録し、isPressed で二重実行を防ぐ。
            void OnRelease()
            {
                if (!isPressed) return;
                isPressed = false;
                dragger.style.backgroundColor = isOver ? (StyleColor)hoverColor : StyleKeyword.Null;
            }
            slider.RegisterCallback<PointerCaptureOutEvent>(_ => OnRelease());
            dragger.RegisterCallback<PointerCaptureOutEvent>(_ => OnRelease());

            dragger.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                isOver = false;
                isPressed = false;
                dragger.style.backgroundColor = StyleKeyword.Null;
            });
        }

        // ホバーのみ（トラック背景など、クリック時の色変化が不要な要素用）
        internal static void RegisterHoverOnly(VisualElement element)
        {
            Color hoverColor = default;

            element.RegisterCallback<CustomStyleResolvedEvent>(e =>
            {
                e.customStyle.TryGetValue(InteractionColorProperties.HoverColor, out hoverColor);
            });

            element.RegisterCallback<PointerEnterEvent>(_ =>
            {
                element.style.backgroundColor = hoverColor;
            });

            element.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                element.style.backgroundColor = StyleKeyword.Null;
            });

            element.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                element.style.backgroundColor = StyleKeyword.Null;
            });
        }
    }
}
