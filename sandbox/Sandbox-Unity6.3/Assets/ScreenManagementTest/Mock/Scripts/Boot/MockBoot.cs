using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lilja.ScreenManagement.Mock
{
    public class MockBoot : MonoBehaviour
    {
        [SerializeField] private bool _useAddressables;

        private void Awake()
        {
            if (_useAddressables)
            {
                PrefabHandle.Factory = address => new AddressablePrefabHandle(address);
            }

            Debug.Log("タイトル画面を開きます...");

            UniTask.Void(async () =>
            {
                try
                {
                    await ScreenManager.Debug.InitializeAsync(builder =>
                    {
                        builder.Register(() => new MockTitleWorld());
                        builder.Register(() => new MockExploreWorld());
                    }, typeof(MockTitleWorld), new ValueTuple(), Fade.Instance, destroyCancellationToken);
                }
                finally
                {
                    await SceneManager.UnloadSceneAsync(gameObject.scene);
                }
            });
        }
    }
}
