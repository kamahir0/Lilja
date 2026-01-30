using System;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// PrefabHandle 関連のAPI提供クラス
    /// </summary>
    public static class PrefabHandle
    {
        /// <summary>
        /// PrefabHandle のファクトリ
        /// </summary>
        public static Func<string, IPrefabHandle> Factory
        {
            get => Repository.Instance.PrefabHandleFactory;
            set => Repository.Instance.PrefabHandleFactory = value;
        }
    }
}
