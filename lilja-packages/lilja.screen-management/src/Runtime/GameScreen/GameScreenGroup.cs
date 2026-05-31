using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// 複数の画面をグループとして一元管理し、排他的な切り替えを提供するクラス。
    /// </summary>
    public class GameScreenGroup
    {
        /// <summary>
        /// このグループ内の画面遷移設定を行います。
        /// </summary>
        /// <param name="builder">登録用ビルダー</param>
        protected virtual void Configure(IGameScreenGroupBuilder builder) { }

        /// <summary>
        /// 新しい <see cref="GameScreenGroup"/> インスタンスを初期化します。
        /// </summary>
        public GameScreenGroup()
        {
            Context = new GameScreenContext();
        }

        /// <summary>
        /// このグループが属する画面階層コンテキスト。
        /// </summary>
        public GameScreenContext Context { get; internal set; }

        /// <summary>
        /// このグループが使用するソート順などの描画レイヤー。
        /// </summary>
        public int Layer { get; set; }

        /// <summary>
        /// 画面遷移元と遷移先の組み合わせに応じた一時差し替えトランジションマップ。
        /// </summary>
        public Dictionary<
            (System.Type From, System.Type To),
            ITransition
        > OverrideTransitionMap { get; } = new();

        /// <summary>
        /// 指定されたキー名が登録されているか判定します。
        /// </summary>
        internal bool Contains(string key)
        {
            return _factories.ContainsKey(key);
        }

        /// <summary>
        /// キーに紐づく画面の型を取得します。
        /// </summary>
        internal Type GetScreenType(string key)
        {
            if (!_types.TryGetValue(key, out var type))
            {
                throw new InvalidOperationException(
                    $"[Lilja.ScreenManagement] 画面キー '{key}' は登録されていません。"
                );
            }
            return type;
        }

        /// <summary>
        /// キーに紐づく画面オブジェクトを作成します。
        /// </summary>
        internal object Create(string key)
        {
            if (!_factories.TryGetValue(key, out var factory))
            {
                throw new InvalidOperationException(
                    $"[Lilja.ScreenManagement] 画面キー '{key}' はこの GameScreenGroup に登録されていません。Configure(IGameScreenGroupBuilder) で登録されているか確認してください。"
                );
            }
            return factory.Invoke();
        }

        /// <summary>
        /// グループの生存期間を待機するための非同期ソース。
        /// </summary>
        internal UniTaskCompletionSource CompletionSource { get; private set; } = new();


        /// <summary>
        /// 内部的な初期設定を実行します。
        /// </summary>
        internal void ConfigureInternal()
        {
            if (_configured)
            {
                return;
            }

            var builder = new GameScreenGroupBuilder();
            Configure(builder);

            foreach (var kvp in builder.Factories)
            {
                _factories[kvp.Key] = kvp.Value;
            }
            foreach (var kvp in builder.Types)
            {
                _types[kvp.Key] = kvp.Value;
            }
            foreach (var kvp in builder.OverrideTransitionMap)
            {
                OverrideTransitionMap[kvp.Key] = kvp.Value;
            }

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
        public GameScreenGroupHandle CallAsync<TArgs>(
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

            if (_called)
            {
                throw new InvalidOperationException(
                    $"[Lilja.ScreenManagement] この GameScreenGroup '{GetType().Name}' は既に呼び出されています。画面グループは使い捨て（再利用不可）です。"
                );
            }
            _called = true;


            return Procedures.Group.CallAsync(
                callerContext,
                this,
                initialScreenKey,
                initialScreenArgs,
                cancellationToken
            );
        }

        /// <summary>
        /// 指定された呼び出し元のコンテキストの下でこのグループを起動し、型名で指定された初期画面を表示してグループの寿命が終了するまで非同期待機します。
        /// </summary>
        /// <typeparam name="TScreen">初期画面の型</typeparam>
        /// <typeparam name="TArgs">引数の型</typeparam>
        /// <param name="callerContext">呼び出し側の画面コンテキスト</param>
        /// <param name="initialScreenArgs">初期表示画面の引数</param>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>グループの生存期間を表す非同期タスク</returns>
        public GameScreenGroupHandle CallAsync<TScreen, TArgs>(
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
        /// このグループ内のアクティブな画面を、キー名を指定して別の画面へと排他切り替えします。
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
        /// このグループ内のアクティブな画面を、型を指定して別の画面へと排他切り替えします。
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
        /// </summary>
        public void Complete()
        {
            CompletionSource.TrySetResult();
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
        /// この画面グループをキャンセル終了させます。
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
        public static GameScreenGroup Create(Action<IGameScreenGroupBuilder> configure)
        {
            return new ConfiguredGameScreenGroup(configure);
        }

        private sealed class ConfiguredGameScreenGroup : GameScreenGroup
        {
            private readonly Action<IGameScreenGroupBuilder> _configure;

            public ConfiguredGameScreenGroup(Action<IGameScreenGroupBuilder> configure)
            {
                _configure = configure ?? throw new ArgumentNullException(nameof(configure));
            }

            /// <inheritdoc />
            protected override void Configure(IGameScreenGroupBuilder builder)
            {
                _configure.Invoke(builder);
            }
        }

        private bool _called;
        private bool _configured;
        private readonly Dictionary<string, Func<object>> _factories = new();
        private readonly Dictionary<string, Type> _types = new();
    }
}
