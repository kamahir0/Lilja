using System;
using System.Collections.Generic;

namespace Lilja.AssetManagement
{
    public sealed class AssetLifetime : IDisposable
    {
        private readonly List<IAssetHandle> _handles = new List<IAssetHandle>();
        private bool _isDisposed;

        public void Register(IAssetHandle handle)
        {
            if (_isDisposed)
            {
                handle.Dispose();
                return;
            }

            _handles.Add(handle);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            foreach (var handle in _handles)
            {
                handle.Dispose();
            }

            _handles.Clear();
        }
    }
}
