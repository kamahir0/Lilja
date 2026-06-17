using System;
using System.Collections.Generic;

namespace Lilja.AssetManagement
{
    /// <summary>
    /// 読み込まれたアセットの寿命を管理するクラス
    /// </summary>
    public sealed class AssetLifetime : IDisposable
    {
        private readonly List<IAssetHandle> _handles = new List<IAssetHandle>();
        private bool _isDisposed;

        /// <summary>
        /// 寿命が尽きた際に破棄されるハンドルを登録します。
        /// </summary>
        /// <param name="handle">登録するアセットハンドル。</param>
        public void Register(IAssetHandle handle)
        {
            if (_isDisposed)
            {
                handle.Dispose();
                return;
            }

            _handles.Add(handle);
        }

        /// <summary>
        /// リソースを破棄します。登録されたすべてのハンドルのDisposeを呼び出します。
        /// </summary>
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
