namespace Lilja.ScreenManagement
{
    /// <summary>
    /// Transition 関連のAPI提供クラス
    /// </summary>
    public static class Transition
    {
        /// <summary>
        /// 現在のトランジション
        /// </summary>
        public static ITransition Current
        {
            get => Repository.Instance.Transition;
            set => Repository.Instance.Transition = value;
        }
    }
}
