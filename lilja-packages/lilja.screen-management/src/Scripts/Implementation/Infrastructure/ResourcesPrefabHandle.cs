using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// Resources を使用して Prefab をロード・解放する IPrefabHandle
    /// </summary>
    public class ResourcesPrefabHandle : IPrefabHandle
    {
        private readonly string _path;
        private GameObject _prefab;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public ResourcesPrefabHandle(string path)
        {
            _path = path;
        }

        /// <inheritdoc/>
        public async UniTask<GameObject> LoadAsync(CancellationToken cancellationToken)
        {
            if (_prefab != null) return _prefab;

            _prefab = (GameObject)await Resources.LoadAsync(_path);
            return _prefab;
        }

        /// <inheritdoc/>
        public void Release()
        {
            _prefab = null;
        }
    }
}
