using UnityEngine.UIElements;

namespace Lilja.DebugUI
{
    /// <summary>
    /// 動的に追加された子要素が1つ以上あるときだけ表示されるフォールドアウト。
    /// AddDebugUI で子が追加されると表示され、すべて Dispose されると非表示に戻る。
    /// </summary>
    [UxmlElement]
    public sealed partial class VirtualFoldout : DebugFoldout
    {
        private int _count = 0;

        public VirtualFoldout() : this(string.Empty) { }

        public VirtualFoldout(string label) : base(label)
        {
            style.display = DisplayStyle.None;
        }

        protected override void OnDynamicChildAdded(VisualElement wrapper)
        {
            _count++;
            style.display = DisplayStyle.Flex;
        }

        protected override void OnDynamicChildRemoved(VisualElement wrapper)
        {
            if (--_count <= 0) style.display = DisplayStyle.None;
        }
    }
}
