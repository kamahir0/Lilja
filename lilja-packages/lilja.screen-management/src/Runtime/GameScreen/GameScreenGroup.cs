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
        /// グループ自身の開始および完了（Complete）時に使用される一時差し替えトランジションを取得します。
        /// </summary>
        public ITransition OverrideTransition { get; private set; }

        /// <summary>
        /// 戻り先となる遷移履歴がスタックに存在するかどうかを取得します。
        /// </summary>
        public bool CanGoBack => _history.Count > 0;

        /// <summary>
        /// 履歴スタックの数を取得します。
        /// </summary>
        public int HistoryCount => _history.Count;

        /// <summary>
        /// 新しい <see cref="GameScreenGroup"/> インスタンスを初期化します。
        /// </summary>
        public GameScreenGroup()
        {
            Context = new GameScreenContext();
        }

        /// <summary>
        /// 指定されたキー名が登録されているか判定します。
        /// </summary>
        /// <param name="screenType">判定対象の画面の型。</param>
        /// <returns>管理対象の画面であれば true、それ以外は false。</returns>
        public bool ContainsScreenType(Type screenType)
        {
            if (screenType == null)
            {
                return false;
            }
            EnsureConfiguration();
            return _types.ContainsValue(screenType);
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
        /// <param name="initialScreenArgs">初期表示画面 of 引数</param>
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
        /// 履歴スタックをすべてクリアします。
        /// </summary>
        public void ClearHistory()
        {
            _history.Clear();
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
            if (_currentKey != null)
            {
                _history.Push((_currentKey, _currentArgs, _currentArgsType));
            }
            return SwitchAsyncInternal(key, args, cancellationToken);
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
        /// 遷移履歴を一つ戻り、前回の画面へ前回の引数と共に切り替えます。
        /// 戻り先の履歴が存在しない場合は、自動的に Complete() を実行してこの画面グループを正常終了します。
        /// </summary>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>切り替え、または終了処理の完了を待つタスク</returns>
        public async UniTask SwitchBackAsync(CancellationToken cancellationToken = default)
        {
            if (_history.Count == 0)
            {
                // 戻る履歴が残っていない場合は、グループを正常終了・クローズする
                Complete();
                return;
            }

            var (prevKey, prevArgs, prevArgsType) = _history.Pop();

            // 型パラメータ付きの SwitchAsyncInternal を動的に構築して実行
            var method = typeof(GameScreenGroup).GetMethod(
                nameof(SwitchAsyncInternal),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            );

            if (method == null)
            {
                throw new InvalidOperationException(
                    "[Lilja.ScreenManagement] 内部切り替えメソッドの解決に失敗しました。"
                );
            }

            var genericMethod = method.MakeGenericMethod(prevArgsType);
            await (UniTask)
                genericMethod.Invoke(this, new[] { prevKey, prevArgs, cancellationToken });
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

        /// <summary>
        /// このグループ内の画面遷移設定を行います。
        /// </summary>
        /// <param name="builder">登録用ビルダー</param>
        protected virtual void Configure(IGameScreenGroupBuilder builder) { }

        private bool _called;
        private bool _configured;
        private string _currentKey;
        private object _currentArgs;
        private Type _currentArgsType;
        private readonly Stack<(string Key, object Args, Type ArgsType)> _history = new();
        private readonly Dictionary<string, Func<object>> _factories = new();
        private readonly Dictionary<string, Type> _types = new();

        /// <summary>
        /// グループの生存期間を待機するための非同期ソース。
        /// </summary>
        internal UniTaskCompletionSource CompletionSource { get; private set; } = new();

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
        /// 内部的な初期設定を実行します。
        /// </summary>
        internal void EnsureConfiguration()
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

            OverrideTransition = builder.GroupOverrideTransition;

            _configured = true;
        }

        /// <summary>
        /// 現在のアクティブ画面情報を設定します（システム内部用）。
        /// </summary>
        internal void SetCurrent(string key, object args, Type argsType)
        {
            _currentKey = key;
            _currentArgs = args;
            _currentArgsType = argsType;
        }

        private UniTask SwitchAsyncInternal<TArgs>(
            string key,
            TArgs args,
            CancellationToken cancellationToken
        )
        {
            // 履歴プッシュを行わずに切り替えを行う内部メソッド。
            _currentKey = key;
            _currentArgs = args;
            _currentArgsType = typeof(TArgs);
            return Procedures.Group.SwitchAsync(this, key, args, cancellationToken);
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
    }
}
