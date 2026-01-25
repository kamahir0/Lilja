using System;
using Cysharp.Threading.Tasks;
using Lilja.AssetManagement;
using UnityEngine;
using UnityEngine.UI;

namespace AssetManagementTest
{
    public class AddressableLoaderTest : MonoBehaviour
    {
        [SerializeField] private Image _image;
        [SerializeField] private string _key = "lilja_1";

        private AssetLifetime _lifetime;
        private IAssetLoader _loader;

        private void Start()
        {
            _loader = new AddressableAssetLoader();

            UniTask.Void(async () =>
            {
                await Load();
                await UniTask.Delay(1000);
                Unload();
            });
        }

        public async UniTask Load()
        {
            if (_lifetime != null)
            {
                Debug.LogWarning("Already loaded. Dispose first.");
                return;
            }

            _lifetime = new AssetLifetime();

            Debug.Log($"Loading {_key}...");
            try
            {
                var sprite = await _loader.LoadAsync<Sprite>(_key, _lifetime, this.GetCancellationTokenOnDestroy());
                _image.sprite = sprite;
                Debug.Log($"Loaded: {sprite.name}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load: {e}");
                _lifetime.Dispose();
                _lifetime = null;
            }
        }

        public void Unload()
        {
            if (_lifetime == null)
            {
                Debug.LogWarning("Not loaded.");
                return;
            }

            Debug.Log("Unloading...");
            _lifetime.Dispose();
            _lifetime = null;
            Debug.Log("Unloaded.");
        }
    }
}

