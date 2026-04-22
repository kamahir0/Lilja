#nullable enable
namespace Lilja.Repository.Internal
{
    /// <summary>
    /// Stores staged write information for singleton repositories.
    /// </summary>
    /// <typeparam name="TValue">The value type stored in the repository.</typeparam>
    internal sealed class RepositoryWriteState<TValue>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RepositoryWriteState{TValue}"/> class.
        /// </summary>
        /// <param name="value">The currently staged or committed value.</param>
        /// <param name="hasValue">Whether a value is present.</param>
        public RepositoryWriteState(TValue? value, bool hasValue)
        {
            Value = value;
            HasValue = hasValue;
        }

        /// <summary>
        /// Gets or sets the staged value.
        /// </summary>
        public TValue? Value { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a staged value exists.
        /// </summary>
        public bool HasValue { get; set; }
    }
}
