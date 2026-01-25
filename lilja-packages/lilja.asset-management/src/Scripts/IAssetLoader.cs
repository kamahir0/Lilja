using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Lilja.AssetManagement
{
    /// <summary>
    /// アセットの読み込みを行うI/F
    /// </summary>
    public interface IAssetLoader
    {
        /// <summary>
        /// アセットを非同期で読み込みます
        /// </summary>
        /// <typeparam name="T">読み込むアセットの型</typeparam>
        /// <param name="key">アセットのキー（パスなど）</param>
        /// <param name="lifetime">アセットの寿命管理オブジェクト</param>
        /// <param name="ct">キャンセレーショントークン</param>
        /// <returns>読み込まれたアセット</returns>
        UniTask<T> LoadAsync<T>(string key, AssetLifetime lifetime, CancellationToken ct = default)
            where T : Object;
    }
}
