using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// ScreenManagement システムのAPI提供クラス
    /// </summary>
    public static class ScreenManager
    {
        /// <summary>
        /// World を初期化します
        /// </summary>
        /// <param name="configuration">Worldの登録処理</param>
        /// <param name="worldType">初期Worldの型</param>
        /// <param name="args">初期Worldの引数</param>
        /// <param name="transition">トランジション</param>
        public static void Initialize(Action<IWorldBuilder> configuration, Type worldType, object args, ITransition transition)
        {
            Repository.Instance.WorldFactories.Clear();
            configuration?.Invoke(new WorldBuilder());
            Repository.Instance.Transition = transition;
            WorldService.InitializeAsync(worldType, args, CancellationToken.None).Forget();
        }

        public interface IWorldBuilder
        {
            /// <summary>
            /// World 作成用のファクトリを登録します
            /// </summary>
            /// <typeparam name="TWorld">World の型</typeparam>
            /// <param name="factory">World 作成用のファクトリ</param>
            void Register<TWorld>(Func<TWorld> factory) where TWorld : IWorld;

            /// <summary>
            /// World 作成用のファクトリを登録します
            /// </summary>
            /// <param name="worldType">World の型</param>
            /// <param name="factory">World 作成用のファクトリ</param>
            void Register(Type worldType, Func<IWorld> factory);
        }

        private class WorldBuilder : IWorldBuilder
        {
            /// <inheritdoc/>
            public void Register<TWorld>(Func<TWorld> factory) where TWorld : IWorld
            {
                Repository.Instance.WorldFactories.Add(typeof(TWorld), () => factory.Invoke());
            }

            /// <inheritdoc/>
            public void Register(Type worldType, Func<IWorld> factory)
            {
                Repository.Instance.WorldFactories.Add(worldType, factory);
            }
        }

        /// <summary>
        /// デバッグ用API提供クラス
        /// </summary>
        public static class Debug
        {
            /// <summary>
            /// World を初期化します（await可能）
            /// </summary>
            /// <param name="configuration">Worldの登録処理</param>
            /// <param name="worldType">初期Worldの型</param>
            /// <param name="args">初期Worldの引数</param>
            /// <param name="transition">トランジション</param>
            /// <param name="cancellationToken">キャンセル用トークン</param>
            public static UniTask InitializeAsync(Action<IWorldBuilder> configuration, Type worldType, object args, ITransition transition, CancellationToken cancellationToken = default)
            {
                Repository.Instance.WorldFactories.Clear();
                configuration?.Invoke(new WorldBuilder());
                Repository.Instance.Transition = transition;
                return WorldService.InitializeAsync(worldType, args, cancellationToken);
            }
        }
    }
}
