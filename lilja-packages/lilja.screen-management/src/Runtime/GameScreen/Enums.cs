namespace Lilja.ScreenManagement
{
    /// <summary>
    /// 画面が活性化される（入場する）際の遷移タイプを表します。
    /// </summary>
    public enum EnterType
    {
        /// <summary>
        /// 画面が新しく生成されてオープンする際。
        /// </summary>
        OnOpen,

        /// <summary>
        /// 重ね合わされていた上の画面が閉じ、背後から復帰する際。
        /// </summary>
        OnResume,
    }

    /// <summary>
    /// 画面が非活性化される（退出する）際の遷移タイプを表します。
    /// </summary>
    public enum ExitType
    {
        /// <summary>
        /// 画面が完全にクローズして破棄される際。
        /// </summary>
        OnClose,

        /// <summary>
        /// 新しい画面が上に重なることで、一時的に停止する際。
        /// </summary>
        OnPause,
    }
}
