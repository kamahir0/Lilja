using System;
using System.Collections.Generic;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// 画面管理の状態を保持するリポジトリ（シングルトン）
    /// </summary>
    public class Repository
    {
        /// <summary> シングルトンインスタンス </summary>
        private static Repository _instance;

        /// <summary> シングルトンインスタンスを取得 </summary>
        public static Repository Instance => _instance ??= new();

        /// <summary> スクリーンスタック </summary>
        public ScreenStack ScreenStack { get; } = new();

        /// <summary> トランジション </summary>
        public ITransition Transition { get; set; }

        /// <summary> World の生成処理 Dictionary </summary>
        public Dictionary<Type, Func<IWorld>> WorldFactories { get; } = new();

        public Func<string, IPrefabHandle> PrefabHandleFactory { get; set; }
            = path => new ResourcesPrefabHandle(path); // デフォルトはResources
    }
}
