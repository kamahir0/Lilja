using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// 画面遷移時のトランジション（フェード演出など）を制御するインターフェース。
    /// </summary>
    public interface ITransition
    {
        /// <summary>
        /// 画面が覆い隠される（フェードアウトや暗転など）演出を行います。
        /// </summary>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>非同期タスク</returns>
        UniTask OutAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 画面の覆いが解除される（フェードインや明転など）演出を行います。
        /// </summary>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>非同期タスク</returns>
        UniTask InAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    /// プレハブアセットのロード・アンロードを抽象化するインターフェース。
    /// </summary>
    public interface IPrefabProvider
    {
        /// <summary>
        /// 指定されたキーのプレハブを非同期でロードします。
        /// </summary>
        /// <param name="key">アセットのキー（ResourcesパスやAddressableアドレスなど）</param>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>ロードされたプレハブオブジェクト</returns>
        UniTask<GameObject> LoadAsync(string key, CancellationToken cancellationToken);

        /// <summary>
        /// 指定されたキーのプレハブアセットをアンロード（解放）します。
        /// </summary>
        /// <param name="key">対象のキー名</param>
        void Unload(string key);
    }

    /// <summary>
    /// シーンアセットのロード・アンロードを抽象化するインターフェース。
    /// </summary>
    public interface ISceneLoader
    {
        /// <summary>
        /// 指定された名前のシーンを非同期で加算（Additive）ロードします。
        /// </summary>
        /// <param name="sceneName">シーン名</param>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>ロードされたシーン情報</returns>
        UniTask<Scene> LoadSceneAsync(string sceneName, CancellationToken cancellationToken);

        /// <summary>
        /// 指定されたシーンを非同期でアンロードします。
        /// </summary>
        /// <param name="scene">アンロード対象のシーン情報</param>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>非同期タスク</returns>
        UniTask UnloadSceneAsync(Scene scene, CancellationToken cancellationToken);
    }

    /// <summary>
    /// 画面遷移システムで共有・使用される、各種サービス（アセットロード、トランジションなど）の依存設定オプション。
    /// </summary>
    public sealed class GameScreenOptions
    {
        /// <summary>
        /// 画面遷移時に使用されるトランジション演出（フェード等）。
        /// </summary>
        public ITransition Transition { get; set; }

        /// <summary>
        /// プレハブのロードに使用されるアセットプロバイダー。未設定の場合は Resources API にフォールバックします。
        /// </summary>
        public IPrefabProvider PrefabProvider { get; set; }

        /// <summary>
        /// シーンのロードに使用されるサービス。未設定の場合は Unity 標準の SceneManager にフォールバックします。
        /// </summary>
        public ISceneLoader SceneLoader { get; set; }

        /// <summary>
        /// 未設定のプロバイダーに標準のデフォルト実装を割り当てて補完します。
        /// </summary>
        internal void ApplyDefaults()
        {
            PrefabProvider ??= new ResourcesPrefabProvider();
            SceneLoader ??= new DefaultSceneLoader();
        }
    }

    /// <summary>
    /// Unity標準の SceneManager を使用してシーンをロード・アンロードするデフォルトのシーンローダー。
    /// </summary>
    internal sealed class DefaultSceneLoader : ISceneLoader
    {
        /// <inheritdoc />
        public async UniTask<Scene> LoadSceneAsync(
            string sceneName,
            CancellationToken cancellationToken
        )
        {
            await SceneManager
                .LoadSceneAsync(sceneName, LoadSceneMode.Additive)
                .WithCancellation(cancellationToken);
            return SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
        }

        /// <inheritdoc />
        public async UniTask UnloadSceneAsync(Scene scene, CancellationToken cancellationToken)
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                await SceneManager.UnloadSceneAsync(scene).WithCancellation(cancellationToken);
            }
        }
    }

    /// <summary>
    /// Unity標準の Resources API を使用してプレハブをロード・アンロードするデフォルトのプロバイダー。
    /// </summary>
    internal sealed class ResourcesPrefabProvider : IPrefabProvider
    {
        private readonly Dictionary<string, GameObject> _loadedCache = new();

        /// <inheritdoc />
        public async UniTask<GameObject> LoadAsync(string key, CancellationToken cancellationToken)
        {
            if (_loadedCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var request = Resources.LoadAsync<GameObject>(key);
            await request.WithCancellation(cancellationToken);
            var prefab = request.asset as GameObject;

            if (prefab == null)
            {
                throw new FileNotFoundException($"Prefab not found in Resources at key: '{key}'");
            }

            _loadedCache[key] = prefab;
            return prefab;
        }

        /// <inheritdoc />
        public void Unload(string key)
        {
            _loadedCache.Remove(key);
        }
    }
}
