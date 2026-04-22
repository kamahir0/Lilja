using System;

namespace Lilja.Repository
{
    /// <summary>
    /// 読み取り専用のリポジトリトランザクションスコープを表します。
    /// </summary>
    /// <remarks>
    /// インスタンスは <see cref="TxManager"/> によって生成されるため、短命なものとして扱う必要があります。
    /// </remarks>
    public interface IReadOnlyTx : IDisposable
    {
    }
}
