namespace Lilja.ScreenManagement
{
    /// <summary>
    /// Pause時のコンテキスト情報
    /// </summary>
    public readonly struct PauseContext
    {
        /// <summary> 次に開く Screen </summary>
        public IScreen NextScreen { get; }

        /// <summary> コンストラクタ </summary>
        public PauseContext(IScreen nextScreen)
        {
            NextScreen = nextScreen;
        }
    }
}
