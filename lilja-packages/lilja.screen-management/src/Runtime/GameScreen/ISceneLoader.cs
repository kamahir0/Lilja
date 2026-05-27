using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// シーンアセットのロード・アンロードを抽象化するインターフェース。
    /// </summary>
    public interface ISceneLoader
    {
        /// <summary>
        /// 指定された名前のシーンを非同期で加算ロードします。
        /// </summary>
        /// <param name="sceneName">シーン名</param>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>ロードされたシーン情報</returns>
        UniTask<Scene> LoadSceneAsync(string sceneName, CancellationToken cancellationToken);

        /// <summary>
        /// 指定されたシーンを非同期でアンロードします。
        /// </summary>
        /// <param name="scene">アンロード対象 of シーン情報</param>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>非同期タスク</returns>
        UniTask UnloadSceneAsync(Scene scene, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Unity標準の SceneManager を使用してシーンをロード・アンロードするデフォルトのシーンローダー。
    /// </summary>
    internal sealed class DefaultSceneLoader : ISceneLoader
    {
        #region ISceneLoader

        /// <inheritdoc />
        public async UniTask<Scene> LoadSceneAsync(
            string sceneName,
            CancellationToken cancellationToken
        )
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (op == null)
            {
                throw new System.IO.FileNotFoundException(
                    $"[Lilja.ScreenManagement] シーン '{sceneName}' をロードできませんでした。ビルド設定（Build Settings）にシーンが登録されているか確認してください。"
                );
            }
            await op.WithCancellation(cancellationToken);
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

        #endregion
    }
}
