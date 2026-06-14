using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Lilja.DebugUI
{
    public sealed class OrderedGroup<TItem> : VisualElement, IDebugUI
    {
        private readonly Action<TItem, IDebugUIBuilder> _configure;
        private readonly List<Entry> _entries = new();
        private long _nextSequence;

        public OrderedGroup(Action<TItem, IDebugUIBuilder> configure)
        {
            _configure = configure ?? throw new ArgumentNullException(nameof(configure));
        }

        public IDisposable Add(int order, TItem item)
        {
            var wrapper = new VisualElement();
            _configure(item, new DebugUIBuilder(wrapper, DebugMenu.CurrentCache));

            var entry = new Entry(this, wrapper, order, _nextSequence++);
            var index = FindInsertIndex(entry);
            _entries.Insert(index, entry);
            Insert(index, wrapper);
            return entry;
        }

        private int FindInsertIndex(Entry entry)
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                var current = _entries[i];
                if (entry.Order < current.Order)
                {
                    return i;
                }

                if (entry.Order == current.Order && entry.Sequence < current.Sequence)
                {
                    return i;
                }
            }

            return _entries.Count;
        }

        private void Remove(Entry entry)
        {
            if (!_entries.Remove(entry))
            {
                return;
            }

            entry.Wrapper.RemoveFromHierarchy();
        }

        private sealed class Entry : IDisposable
        {
            private readonly OrderedGroup<TItem> _owner;
            private bool _disposed;

            public Entry(OrderedGroup<TItem> owner, VisualElement wrapper, int order, long sequence)
            {
                _owner = owner;
                Wrapper = wrapper;
                Order = order;
                Sequence = sequence;
            }

            public VisualElement Wrapper { get; }
            public int Order { get; }
            public long Sequence { get; }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _owner.Remove(this);
            }
        }
    }
}
