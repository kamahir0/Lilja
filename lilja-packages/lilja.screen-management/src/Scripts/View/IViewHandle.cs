using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// 画面の「肉体（ビューアセット）」の読み込み、破棄、および管理を行うための抽象インターフェース。
    /// </summary>
    public interface IViewHandle
    {
        /// <summary>
        /// ロードされたビューのルートGameObject配列を取得します。
        /// </summary>
        GameObject[] RootObjects { get; }

        /// <summary>
        /// 画面がインスタンス化された後、ビューのロードが始まる直前に呼び出されます。
        /// 画面の型情報を受け取って、マジックテキストを排除した自動キー解決などを行います。
        /// </summary>
        /// <param name="ownerType">このビューハンドルを所有する画面の型</param>
        void Initialize(Type ownerType);

        /// <summary>
        /// ビューアセットを非同期でロード（生成または加算シーンロード）します。
        /// </summary>
        /// <param name="context">伝播された画面遷移コンテキスト</param>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>非同期タスク</returns>
        UniTask LoadAsync(GameScreenContext context, CancellationToken cancellationToken);

        /// <summary>
        /// ビューアセットをアンロード（GameObjectの破棄、シーンのアンロードなど）し、メモリを解放します。
        /// </summary>
        void Unload();
    }
}
