using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Lilja.DebugUI
{
    /// <summary>
    /// ToggleButtonGroup と同様の構造で複数選択が可能なカスタムトグルグループ。
    /// </summary>
    [UxmlElement]
    public partial class DebugToggleGroup : VisualElement, INotifyValueChanged<ToggleButtonGroupState>, IDebugUI
    {
        // UI
        private readonly Label _labelElement;
        private readonly VisualElement _itemsContainer;
        private readonly List<DebugToggleGroupItem> _items = new();

        private ToggleButtonGroupState _state;

        /// <inheritdoc/>
        public override VisualElement contentContainer => _itemsContainer;


        [UxmlAttribute]
        public string Label
        {
            get => _labelElement.text;
            set => _labelElement.text = value;
        }

        /// <summary>現在の選択状態をビットマスクで返す</summary>
        public ToggleButtonGroupState Value
        {
            get => _state;
            set
            {
                if (_state.Equals(value)) return;
                var previous = _state;
                SetValueWithoutNotify(value);
                using var evt = ChangeEvent<ToggleButtonGroupState>.GetPooled(previous, _state);
                evt.target = this;
                SendEvent(evt);
            }
        }

        /// <inheritdoc/>
        ToggleButtonGroupState INotifyValueChanged<ToggleButtonGroupState>.value
        {
            get => Value;
            set => Value = value;
        }

        public DebugToggleGroup() : this(string.Empty) { }

        public DebugToggleGroup(string label)
        {
            AddToClassList(DebugMenuUssClass.ToggleGroup.Root);
            AddToClassList(DebugMenuUssClass.ControlSize);

            // ラベル
            _labelElement = new Label(label);
            _labelElement.AddToClassList(DebugMenuUssClass.ToggleGroup.LabelElement);
            hierarchy.Add(_labelElement);

            // コンテンツエリア
            _itemsContainer = new VisualElement();
            _itemsContainer.AddToClassList(DebugMenuUssClass.ToggleGroup.Items);
            hierarchy.Add(_itemsContainer);
        }

        /// <inheritdoc/>
        public void SetValueWithoutNotify(ToggleButtonGroupState state)
        {
            int count = System.Math.Min(_items.Count, state.length);
            for (int i = 0; i < count; i++)
            {
                _items[i].SetValueWithoutNotify(state[i]);
            }
            RebuildState();
        }

        internal void RegisterItem(DebugToggleGroupItem item)
        {
            if (_items.Contains(item)) return;
            _items.Add(item);
            item.RegisterValueChangedCallback(OnItemValueChanged);
            RebuildState();
        }

        internal void UnregisterItem(DebugToggleGroupItem item)
        {
            if (!_items.Contains(item)) return;
            item.UnregisterValueChangedCallback(OnItemValueChanged);
            _items.Remove(item);
            RebuildState();
        }

        private void RebuildState()
        {
            _state = new ToggleButtonGroupState(0ul, _items.Count);
            for (int i = 0; i < _items.Count; i++)
            {
                _state[i] = _items[i].Value;
            }
        }

        private void OnItemValueChanged(ChangeEvent<bool> _)
        {
            var previous = _state;
            RebuildState();
            using var evt = ChangeEvent<ToggleButtonGroupState>.GetPooled(previous, _state);
            evt.target = this;
            SendEvent(evt);
        }
    }
}
