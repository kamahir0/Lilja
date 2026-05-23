using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// 画面システムに参加するユーザー定義画面オブジェクトの共通マーカーインターフェース。
    /// </summary>
    public interface IGameScreen : IDisposable { }

    /// <summary>
    /// 画面のクローズ、再開、一時停止など、ランタイムが汎用的に操作するための内部インターフェース。
    /// </summary>
    internal interface IGameScreenInternal : IGameScreen
    {
        /// <summary>
        /// 伝播された画面遷移コンテキストを取得します。
        /// </summary>
        GameScreenContext Context { get; }

        /// <summary>
        /// この画面が所有するビューハンドルを取得または遅延解決します。
        /// </summary>
        /// <returns>ビューハンドルインスタンス</returns>
        IViewHandle GetViewHandle();

        /// <summary>
        /// 画面を閉じます。
        /// </summary>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>非同期タスク</returns>
        UniTask CloseAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 画面を再開します。
        /// </summary>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>非同期タスク</returns>
        UniTask ResumeAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 画面を一時停止します。
        /// </summary>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>非同期タスク</returns>
        UniTask PauseAsync(CancellationToken cancellationToken);

        /// <summary>
        /// ビューのロードおよび注入が完了したことを通知します。
        /// </summary>
        void OnViewLoaded();

        /// <summary>
        /// ビューがアンロードされる直前であることを通知します。
        /// </summary>
        void OnViewUnloaded();
    }

    /// <summary>
    /// 引数を伴う画面の初期化・オープンを行うための内部インターフェース。
    /// </summary>
    /// <typeparam name="TArgs">初期化引数の型</typeparam>
    internal interface IGameScreenInternal<in TArgs> : IGameScreenInternal
    {
        /// <summary>
        /// 画面を初期化してオープンします。
        /// </summary>
        /// <param name="args">初期化引数</param>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>非同期タスク</returns>
        UniTask OpenAsync(TArgs args, CancellationToken cancellationToken);
    }
}
