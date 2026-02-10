using System;

namespace Lilja.Repository
{
    /// <summary>
    /// 読み取り専用トランザクションのI/F。
    /// </summary>
    public interface IReadOnlyTx : IDisposable
    {
    }
}
