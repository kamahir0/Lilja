namespace Lilja.ScreenManagement
{
    /// <summary>
    /// Resume時のコンテキスト情報
    /// </summary>
    public readonly struct ResumeContext
    {
        /// <summary> 閉じられた Screen </summary>
        public IScreen PreviousScreen { get; }

        /// <summary> コンストラクタ </summary>
        public ResumeContext(IScreen previousScreen)
        {
            PreviousScreen = previousScreen;
        }
    }
}
