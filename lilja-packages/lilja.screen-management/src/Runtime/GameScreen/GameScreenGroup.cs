using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// 常に 1 つのアクティブな <see cref="GameScreen{TArgs}"/> を排他所有して切り替える実行インスタンス。
    /// グループは通常、呼び出し側から await して実行され、グループ自体の終了（Complete）まで待機されます。
    /// </summary>
    public class GameScreenGroup
    {
        private bool _configured;

        /// <summary>
        /// 新しい <see cref="GameScreenGroup"/> インスタンスを初期化します。
        /// </summary>
        public GameScreenGroup()
        {
            var connector = new GameScreenConnector { Owner = this };
            Context = new GameScreenContext(connector);
        }

        /// <summary>
        /// このグループが属する画面階層コンテキスト。
        /// </summary>
        protected internal GameScreenContext Context { get; }

        /// <summary>
        /// このグループ内の画面レジストリ。
        /// </summary>
        internal GameScreenRegistry Registry { get; } = new();

        /// <summary>
        /// 多重画面遷移を防ぐための非同期セマフォ。
        /// </summary>
        internal SemaphoreSlim Gate { get; } = new(1, 1);

        /// <summary>
        /// グループの生存期間を待機するための非同期ソース。
        /// </summary>
        internal UniTaskCompletionSource<ValueTuple> CompletionSource { get; } = new();

        /// <summary>
        /// このグループ内の画面遷移設定を行います。
        /// 派生クラスでオーバーライドして、このグループで使用する画面（GameScreen）を登録します。
        /// </summary>
        /// <param name="registry">登録用レジストリ</param>
        protected virtual void Configure(IGameScreenRegistry registry) { }

        /// <summary>
        /// 内部的な初期設定（Configure）を実行します。
        /// </summary>
        internal void ConfigureInternal()
        {
            if (_configured)
            {
                return;
            }

            Configure(Registry);
            _configured = true;
        }

        /// <summary>
        /// 指定された呼び出し元のコンテキストの下でこのグループを起動し、初期画面を表示してグループの寿命が終了するまで非同期待機します。
        /// </summary>
        /// <typeparam name="TArgs">初期画面の引数の型</typeparam>
        /// <param name="callerContext">呼び出し側の画面コンテキスト</param>
        /// <param name="initialScreenKey">初期表示画面のキー名</param>
        /// <param name="initialScreenArgs">初期表示画面の引数</param>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>グループの生存期間を表す非同期タスク</returns>
        public UniTask CallAsync<TArgs>(
            GameScreenContext callerContext,
            string initialScreenKey,
            TArgs initialScreenArgs,
            CancellationToken cancellationToken = default
        )
        {
            if (callerContext == null)
            {
                throw new ArgumentNullException(nameof(callerContext));
            }

            return Procedures.Group.CallAsync(
                callerContext,
                this,
                initialScreenKey,
                initialScreenArgs,
                cancellationToken
            );
        }

        /// <summary>
        /// 指定された呼び出し元のコンテキストの下でこのグループを起動し、型名で指定された初期画面を表示してグループの寿命が終了するまで非同期待機します（型安全風ラッパー）。
        /// </summary>
        /// <typeparam name="TScreen">初期画面の型</typeparam>
        /// <typeparam name="TArgs">引数の型</typeparam>
        /// <param name="callerContext">呼び出し側の画面コンテキスト</param>
        /// <param name="initialScreenArgs">初期表示画面の引数</param>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>グループの生存期間を表す非同期タスク</returns>
        public UniTask CallAsync<TScreen, TArgs>(
            GameScreenContext callerContext,
            TArgs initialScreenArgs,
            CancellationToken cancellationToken = default
        )
            where TScreen : GameScreen<TArgs>
        {
            return CallAsync(
                callerContext,
                typeof(TScreen).FullName,
                initialScreenArgs,
                cancellationToken
            );
        }

        /// <summary>
        /// このグループ内のアクティブなアクティブ画面を、キー名を指定して別の画面へと排他切り替えします。
        /// </summary>
        /// <typeparam name="TArgs">切り替え先画面の引数の型</typeparam>
        /// <param name="key">切り替え先画面の登録キー名</param>
        /// <param name="args">切り替え先画面に渡す引数</param>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>切り替え演出完了までを待つ非同期タスク</returns>
        public UniTask SwitchAsync<TArgs>(
            string key,
            TArgs args,
            CancellationToken cancellationToken = default
        )
        {
            return Procedures.Group.SwitchAsync(this, key, args, cancellationToken);
        }

        /// <summary>
        /// このグループ内のアクティブなアクティブ画面を、型を指定して別の画面へと排他切り替えします（型安全風ラッパー）。
        /// </summary>
        /// <typeparam name="TScreen">切り替え先画面の型</typeparam>
        /// <typeparam name="TArgs">切り替え先画面の引数の型</typeparam>
        /// <param name="args">切り替え先画面に渡す引数</param>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>切り替え演出完了までを待つ非同期タスク</returns>
        public UniTask SwitchAsync<TScreen, TArgs>(
            TArgs args,
            CancellationToken cancellationToken = default
        )
            where TScreen : GameScreen<TArgs>
        {
            return SwitchAsync(typeof(TScreen).FullName, args, cancellationToken);
        }

        /// <summary>
        /// この画面グループの正常終了を確定します。
        /// CallAsync 呼び出し側は、この後の退出・ビューアンロード・クリーンアップ演出の完了を待ってから完了します。
        /// </summary>
        public void Complete()
        {
            CompletionSource.TrySetResult(new ValueTuple());
        }

        /// <summary>
        /// この画面グループの失敗終了を確定し、呼び出し側へ例外を伝播させます。
        /// </summary>
        /// <param name="exception">伝播させる例外</param>
        public void Fail(Exception exception)
        {
            CompletionSource.TrySetException(exception);
        }

        /// <summary>
        /// この画面グループをキャンセル終了させます。呼び出し側にはキャンセル例外が伝播します。
        /// </summary>
        public void Cancel()
        {
            CompletionSource.TrySetCanceled();
        }

        /// <summary>
        /// レジストリ登録のラムダ設定のみでグループを簡易生成します。
        /// </summary>
        /// <param name="configure">画面登録を行うデリゲート</param>
        /// <returns>簡易生成された画面グループインスタンス</returns>
        public static GameScreenGroup Create(Action<IGameScreenRegistry> configure)
        {
            return new ConfiguredGameScreenGroup(configure);
        }

        private sealed class ConfiguredGameScreenGroup : GameScreenGroup
        {
            private readonly Action<IGameScreenRegistry> _configure;

            public ConfiguredGameScreenGroup(Action<IGameScreenRegistry> configure)
            {
                _configure = configure ?? throw new ArgumentNullException(nameof(configure));
            }

            protected override void Configure(IGameScreenRegistry registry)
            {
                _configure.Invoke(registry);
            }
        }
    }
}
