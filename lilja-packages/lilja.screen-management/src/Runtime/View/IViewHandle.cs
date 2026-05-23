using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Lilja.ScreenManagement
{

    public interface IViewHandle
    {

        GameObject[] RootObjects { get; }

        bool IsLoaded { get; }

        bool IsUnloadedTemporarily { get; set; }

        bool UnloadsAncestors { get; }

        void Initialize(Type ownerType);

        UniTask PreloadAsync(GameScreenContext context, CancellationToken cancellationToken);

        UniTask LoadAsync(GameScreenContext context, CancellationToken cancellationToken);

        void Unload();
    }
}
