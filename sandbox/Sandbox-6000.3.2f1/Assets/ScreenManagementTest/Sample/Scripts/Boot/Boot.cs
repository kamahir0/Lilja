using System;
using Cysharp.Threading.Tasks;
using Lilja.ScreenManagement;
using ScreenManagementSample.Application;
using ScreenManagementSample.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ScreenManagementSample
{
    /// <summary>
    /// ゲーム起動クラス
    /// </summary>
    public class Boot : MonoBehaviour
    {
        private void Awake()
        {
            // サービスを初期化
            GameServices.Initialize();

            Debug.Log("[Boot] ゲームを開始します...");

            UniTask.Void(async () =>
            {
                try
                {
                    await ScreenManager.Debug.InitializeAsync(
                        builder =>
                        {
                            builder.Register(() => new TitleWorld());
                            builder.Register(() => new MapWorld());
                            builder.Register(() => new GameOverWorld());
                        },
                        typeof(TitleWorld),
                        new ValueTuple(),
                        SampleFade.Instance,
                        destroyCancellationToken
                    );
                }
                finally
                {
                    // Bootシーンをアンロード
                    await SceneManager.UnloadSceneAsync(gameObject.scene);
                }
            });
        }
    }
}
