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
        /// この画面がビューを持たない論理画面（GameFlow等）であるかどうかを示す値を取得します。
        /// </summary>
        bool IsViewless { get; }

        /// <summary>
        /// 伝播された画面遷移コンテキストを取得します。
        /// </summary>
        GameScreenContext Context { get; }

        /// <summary>
        /// この画面が割り当てられたソート順などの描画レイヤー値。
        /// </summary>
        int Layer { get; set; }

        /// <summary>
        /// この画面が現在クローズ処理中であるか。
        /// </summary>
        bool IsClosing { get; set; }

        /// <summary>
        /// この画面が所有するビューハンドルを取得または遅延解決します。
        /// </summary>
        /// <returns>ビューハンドルインスタンス</returns>
        IViewHandle GetViewHandle();

        /// <summary>
        /// 画面への入場演出・処理を実行します。
        /// </summary>
        /// <param name="context">入場遷移のコンテキスト</param>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>非同期タスク</returns>
        UniTask ExecuteEnterAsync(EnterContext context, CancellationToken cancellationToken);

        /// <summary>
        /// 画面からの退場演出・処理を実行します。
        /// </summary>
        /// <param name="context">退場遷移のコンテキスト</param>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>非同期タスク</returns>
        UniTask ExecuteExitAsync(ExitContext context, CancellationToken cancellationToken);

        /// <summary>
        /// ビューのロードおよび注入が完了したことを通知します。
        /// </summary>
        void OnViewLoaded();

        /// <summary>
        /// ビューがアンロードされる直前であることを通知します。
        /// </summary>
        void OnViewUnload();
    }

    /// <summary>
    /// 引数を伴う画面の初期化・オープンを行うための内部インターフェース。
    /// </summary>
    /// <typeparam name="TArgs">初期化引数の型</typeparam>
    internal interface IGameScreenInternal<in TArgs> : IGameScreenInternal
    {
        /// <summary>
        /// 画面を初期化します。
        /// </summary>
        /// <param name="args">初期化引数</param>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>非同期タスク</returns>
        UniTask InitializeAsync(TArgs args, CancellationToken cancellationToken);
    }
}
