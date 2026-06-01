using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace Lilja.AssetManagement
{
  /// <summary>
  /// Addressablesを使用してアセットを読み込むローダーの実装
  /// </summary>
  public sealed class AddressableAssetLoader : IAssetLoader
  {
    /// <inheritdoc />
    public async UniTask<T> LoadAsync<T>(string key, AssetLifetime lifetime, CancellationToken ct = default)
      where T : Object
    {
      var handle = Addressables.LoadAssetAsync<T>(key);

      try
      {
        var asset = await handle.ToUniTask(cancellationToken: ct);

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
          var disposableHandle = new AddressableHandle(handle);
          lifetime.Register(disposableHandle);
          return asset;
        }

        return null;
      }
      catch
      {
        if (handle.IsValid())
        {
          Addressables.Release(handle);
        }

        throw;
      }
    }

    private sealed class AddressableHandle : IAssetHandle
    {
      private AsyncOperationHandle _handle;

      public AddressableHandle(AsyncOperationHandle handle)
      {
        _handle = handle;
      }

      public void Dispose()
      {
        if (_handle.IsValid())
        {
          Addressables.Release(_handle);
        }
      }
    }
  }
}