using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// World 関連のAPI提供クラス
    /// </summary>
    public static class World
    {
        /// <summary>
        /// World を切り替えます
        /// </summary>
        /// <param name="worldType">切り替えるWorldの型</param>
        /// <param name="args">切り替えるWorldの引数</param>
        public static void Switch(Type worldType, object args) => WorldService.SwitchAsync(worldType, args, CancellationToken.None).Forget();

        /// <summary>
        /// デバッグ用API提供クラス
        /// </summary>
        public static class Debug
        {
            /// <summary>
            /// World を切り替えます（await可能）
            /// </summary>
            /// <param name="worldType">切り替えるWorldの型</param>
            /// <param name="args">切り替えるWorldの引数</param>
            /// <param name="cancellationToken">キャンセル用トークン</param>
            public static UniTask SwitchAsync(Type worldType, object args, CancellationToken cancellationToken = default) => WorldService.SwitchAsync(worldType, args, cancellationToken);
        }
    }
}
