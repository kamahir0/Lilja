using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// 画面の具象的なビューのロード・配置・解放など、物理的インフラライフサイクルを抽象化するハンドルインターフェース。
    /// </summary>
    public interface IViewHandle
    {
        /// <summary>
        /// ロードされインスタンス化されたビューのルート GameObject 群を取得します。
        /// </summary>
        GameObject[] RootObjects { get; }

        /// <summary>
        /// ビューアセットがロードされ、物理的な実体が配置されているかを示す値。
        /// </summary>
        bool IsLoaded { get; }

        /// <summary>
        /// メモリ節約などの目的で、ビューアセットが一時的にアンロードされた状態であるかを示す値。
        /// </summary>
        bool IsUnloadedTemporarily { get; set; }

        /// <summary>
        /// このビューのロード時に、先祖（親）画面のビューを一時アンロードしてメモリ解放を要求するかを示す値。
        /// </summary>
        bool UnloadsAncestors { get; }

        /// <summary>
        /// ハンドルの初期設定を行い、所有者となる画面の型を紐付けます。
        /// </summary>
        /// <param name="ownerType">所有者となる画面オブジェクトの型</param>
        void Initialize(Type ownerType);

        /// <summary>
        /// 画面がロードされる前に、ビューのアセットのみを事前に非同期ロードしてキャッシュします。
        /// </summary>
        /// <param name="context">ロード時コンテキスト</param>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>非同期タスク</returns>
        UniTask PreloadAsync(GameScreenContext context, CancellationToken cancellationToken);

        /// <summary>
        /// ビューアセットをロードし、GameObject を生成してアクティベートします。
        /// </summary>
        /// <param name="context">ロード時コンテキスト</param>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>非同期タスク</returns>
        UniTask LoadAsync(GameScreenContext context, CancellationToken cancellationToken);

        /// <summary>
        /// 生成された GameObject を破棄し、ロードされたビューアセットをメモリからアンロード（解放）します。
        /// </summary>
        void Unload();
    }
}
