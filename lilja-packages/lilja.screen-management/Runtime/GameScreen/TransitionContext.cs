using System;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// 画面の入場（Enter）時に渡される遷移コンテキスト構造体。
    /// </summary>
    public readonly struct EnterContext
    {
        /// <summary>
        /// 入場遷移の種類を取得します。
        /// </summary>
        public EnterType EnterType { get; }

        /// <summary>
        /// 遷移元（手前）の画面の型を取得します。遷移元がない場合は null になります。
        /// </summary>
        public Type PreviousScreenType { get; }

        /// <summary>
        /// 遷移元の画面が存在するかどうかを示す値を取得します。
        /// </summary>
        public bool HasPreviousScreen => PreviousScreenType != null;

        /// <summary>
        /// トランジション演出を制御するハンドルオブジェクトを取得します。
        /// </summary>
        public ITransitionHandle Transition { get; }

        internal EnterContext(
            EnterType enterType,
            Type previousScreenType,
            ITransitionHandle transition
        )
        {
            EnterType = enterType;
            PreviousScreenType = previousScreenType;
            Transition = transition ?? throw new ArgumentNullException(nameof(transition));
        }
    }

    /// <summary>
    /// 画面の退場（Exit）時に渡される遷移コンテキスト構造体。
    /// </summary>
    public readonly struct ExitContext
    {
        /// <summary>
        /// 退場遷移の種類を取得します。
        /// </summary>
        public ExitType ExitType { get; }

        /// <summary>
        /// 遷移先（次）の画面の型を取得します。遷移先がない場合は null になります。
        /// </summary>
        public Type NextScreenType { get; }

        /// <summary>
        /// 遷移先の画面が存在するかどうかを示す値を取得します。
        /// </summary>
        public bool HasNextScreen => NextScreenType != null;

        /// <summary>
        /// トランジション演出を制御するハンドルオブジェクトを取得します。
        /// </summary>
        public ITransitionHandle Transition { get; }

        internal ExitContext(ExitType exitType, Type nextScreenType, ITransitionHandle transition)
        {
            ExitType = exitType;
            NextScreenType = nextScreenType;
            Transition = transition ?? throw new ArgumentNullException(nameof(transition));
        }
    }
}
