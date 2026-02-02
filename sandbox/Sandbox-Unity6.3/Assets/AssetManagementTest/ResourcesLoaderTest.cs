using Cysharp.Threading.Tasks;
using Lilja.AssetManagement;
using UnityEngine;
using UnityEngine.UI;

namespace AssetManagementTest
{
    public class ResourcesLoaderTest : MonoBehaviour
    {
        [SerializeField] private RawImage _image;
        [SerializeField] private string _path = "lilja_1";

        private AssetLifetime _lifetime;
        private AssetLifetime _lifetime2;
        private IAssetLoader _loader;

        private void Start()
        {
            _loader = new ResourcesAssetLoader();

            UniTask.Void(async () =>
            {
                _lifetime = new AssetLifetime();
                var texture = await _loader.LoadAsync<Texture>(_path, _lifetime, this.GetCancellationTokenOnDestroy());
                _image.texture = texture;
                Debug.Log($"アセットをロードしました 1回目");

                _lifetime2 = new AssetLifetime();
                var _ = await _loader.LoadAsync<Texture>(_path, _lifetime2, this.GetCancellationTokenOnDestroy());
                Debug.Log($"アセットをロードしました 2回目");

                await UniTask.Delay(1000);

                _lifetime.Dispose();
                _lifetime = null;
                Debug.Log($"アセットをアンロードしました 1回目");

                await UniTask.Delay(1000);

                _lifetime2.Dispose();
                _lifetime2 = null;
                Debug.Log($"アセットをアンロードしました 2回目");
            });
        }
    }
}

