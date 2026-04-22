using System;

namespace Lilja.Repository
{
/// <summary>
/// Represents a read-only repository transaction scope.
/// </summary>
/// <remarks>
/// Instances are created by <see cref="TxManager"/> and should be treated as short-lived.
/// </remarks>
public interface IReadOnlyTx : IDisposable
{
}
}
