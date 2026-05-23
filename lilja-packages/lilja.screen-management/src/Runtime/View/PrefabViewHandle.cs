using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Lilja.ScreenManagement
{

    public sealed class PrefabViewHandle : IViewHandle
    {

        public static PrefabViewHandle Default => new(null);

        private readonly string _specifiedKey;
        private readonly bool _unloadsAncestors;
        private string _resolvedKey;
        private GameObject _instance;
        private GameObject[] _rootObjects = Array.Empty<GameObject>();

        public GameObject[] RootObjects => _rootObjects;

        public bool IsLoaded => _instance != null;

        public bool IsUnloadedTemporarily { get; set; }

        public bool UnloadsAncestors => _unloadsAncestors;

        public PrefabViewHandle(string prefabKey, bool unloadsAncestors = false)
        {
            _specifiedKey = prefabKey;
            _unloadsAncestors = unloadsAncestors;
        }

        public void Initialize(Type ownerType)
        {
            if (_resolvedKey != null)
            {
                return;
            }

            _resolvedKey = !string.IsNullOrEmpty(_specifiedKey)
                ? _specifiedKey
                : ResolveKeyFromType(ownerType);
        }

        public async UniTask LoadAsync(
            GameScreenContext context,
            CancellationToken cancellationToken
        )
        {
            if (_instance != null)
            {
                return;
            }

            if (_resolvedKey == null)
            {
                throw new InvalidOperationException(
                    "PrefabViewHandle has not been initialized with a type context."
                );
            }

            var provider = context.Options.PrefabProvider;
            var prefab = await provider.LoadAsync(_resolvedKey, cancellationToken);

            if (prefab == null)
            {
                throw new FileNotFoundException(
                    $"Prefab asset could not be loaded at key: '{_resolvedKey}'"
                );
            }

            _instance = UnityEngine.Object.Instantiate(prefab);
            _rootObjects = new[] { _instance };
        }

        public void Unload()
        {
            if (_instance != null)
            {
                UnityEngine.Object.Destroy(_instance);
                _instance = null;
            }
            _rootObjects = Array.Empty<GameObject>();
        }

        public async UniTask PreloadAsync(GameScreenContext context, CancellationToken cancellationToken)
        {
            if (_resolvedKey == null)
            {
                throw new InvalidOperationException(
                    "PrefabViewHandle has not been initialized with a type context."
                );
            }
            await context.Options.PrefabProvider.LoadAsync(_resolvedKey, cancellationToken);
        }

        private static string ResolveKeyFromType(Type ownerType)
        {
            var typeName = ownerType.Name;
            const string suffix = "Screen";

            if (typeName.EndsWith(suffix))
            {
                typeName = typeName[..^suffix.Length];
            }

            return $"Screens/{typeName}";
        }
    }
}
