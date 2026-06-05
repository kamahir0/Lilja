#nullable enable
namespace Lilja.Repository.Internal
{
    /// <summary>
    /// シングルトンリポジトリ向けのステージング済み書き込み情報を保持します。
    /// </summary>
    /// <typeparam name="TValue">リポジトリに格納される値の型。</typeparam>
    internal sealed class RepositoryWriteState<TValue>
    {
        /// <summary>
        /// <see cref="RepositoryWriteState{TValue}"/> クラスの新しいインスタンスを初期化します。
        /// </summary>
        /// <param name="value">現在ステージングされている、または確定済みの値。</param>
        /// <param name="hasValue">値が存在するかどうか。</param>
        public RepositoryWriteState(TValue? value, bool hasValue)
        {
            Value = value;
            HasValue = hasValue;
        }

        /// <summary>
        /// ステージングされた値を取得または設定します。
        /// </summary>
        public TValue? Value { get; set; }

        /// <summary>
        /// ステージングされた値が存在するかどうかを示す値を取得または設定します。
        /// </summary>
        public bool HasValue { get; set; }
    }
}
