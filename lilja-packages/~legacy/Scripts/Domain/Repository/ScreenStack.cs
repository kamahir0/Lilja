using System;
using System.Collections;
using System.Collections.Generic;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// World + Overlay を統一的に管理するスタック
    /// </summary>
    public class ScreenStack : IEnumerable<IScreen>
    {
        /// <summary> 現在の World </summary>
        public IWorld CurrentWorld { get; set; }

        /// <summary> Overlay スタック </summary>
        public Stack<IOverlay> OverlayStack { get; } = new();

        /// <summary> スタックの一番上のScreenを取得します。OverlayStackが空の場合はWorldを返します。 </summary>
        public IScreen TopScreen => OverlayStack.Count > 0 ? OverlayStack.Peek() : CurrentWorld;

        public ScreenEnumerator GetEnumerator() => new(this);
        IEnumerator<IScreen> IEnumerable<IScreen>.GetEnumerator() => new BoxedScreenEnumerator(this);
        IEnumerator IEnumerable.GetEnumerator() => new BoxedScreenEnumerator(this);

        /// <summary>
        /// ScreenStack用の列挙子（構造体）
        /// </summary>
        public struct ScreenEnumerator
        {
            private readonly ScreenStack _stack;
            private Stack<IOverlay>.Enumerator _overlayEnumerator;
            private int _phase; // 0=overlay, 1=world, 2=done

            internal ScreenEnumerator(ScreenStack stack)
            {
                _stack = stack;
                _overlayEnumerator = stack.OverlayStack.GetEnumerator();
                _phase = 0;
                Current = null;
            }

            public IScreen Current { get; private set; }

            public bool MoveNext()
            {
                // Phase 0: OverlayStack を列挙
                if (_phase == 0)
                {
                    if (_overlayEnumerator.MoveNext())
                    {
                        Current = _overlayEnumerator.Current;
                        return true;
                    }

                    _phase = 1;
                }

                // Phase 1: World を返す
                if (_phase == 1)
                {
                    _phase = 2;
                    if (_stack.CurrentWorld != null)
                    {
                        Current = _stack.CurrentWorld;
                        return true;
                    }
                }

                // Phase 2: 終了
                return false;
            }
        }

        /// <summary>
        /// ScreenStack用の列挙子（ボックス化）
        /// </summary>
        private class BoxedScreenEnumerator : IEnumerator<IScreen>
        {
            private ScreenEnumerator _enumerator;

            public BoxedScreenEnumerator(ScreenStack stack)
            {
                _enumerator = new ScreenEnumerator(stack);
            }

            public IScreen Current => _enumerator.Current;
            object IEnumerator.Current => Current;
            public bool MoveNext() => _enumerator.MoveNext();
            public void Reset() => throw new NotSupportedException();
            public void Dispose() { }
        }
    }
}