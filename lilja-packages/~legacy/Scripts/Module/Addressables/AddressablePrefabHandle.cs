#if ENABLE_ADDRESSABLES
using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// Addressable を使用して Prefab をロード・解放する IPrefabHandle
    /// </summary>
    public class AddressablePrefabHandle : IPrefabHandle
    {
        private readonly string _address;
        private AsyncOperationHandle<GameObject> _handle;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public AddressablePrefabHandle(string address)
        {
            _address = address;
        }

        /// <inheritdoc/>
        public async UniTask<GameObject> LoadAsync(CancellationToken cancellationToken)
        {
            if (_handle.IsValid())
            {
                if (_handle.IsDone) return _handle.Result;
                await _handle.ToUniTask(cancellationToken: cancellationToken);
                return _handle.Result;
            }

            if (!Addressables.ResourceLocators.Any())
            {
                await Addressables.InitializeAsync().ToUniTask(cancellationToken: cancellationToken);
            }

            if (!Exists(_address))
            {
                return null;
            }

            _handle = Addressables.LoadAssetAsync<GameObject>(_address);

            try
            {
                await _handle.ToUniTask(cancellationToken: cancellationToken);
                return _handle.Result;
            }
            catch (Exception)
            {
                Release();
                return null;
            }
        }

        /// <inheritdoc/>
        public void Release()
        {
            if (_handle.IsValid())
            {
                Addressables.Release(_handle);
                _handle = default;
            }
        }

        /// <summary> 指定されたアドレスが存在するかを確認します </summary>
        private bool Exists(string key)
        {
            return Addressables.ResourceLocators.Any(locator => locator.Locate(key, typeof(object), out _));
        }
    }
}
#endif