using System;

namespace Lilja.DebugUI
{
    internal sealed class DelegateDisposable : IDisposable
    {
        private readonly Action _onDispose;
        private bool _disposed;

        public DelegateDisposable(Action onDispose) => _onDispose = onDispose;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _onDispose?.Invoke();
        }
    }
}
