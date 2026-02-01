namespace Lilja.Repository
{
    /// <summary>
    /// トランザクションファクトリのデフォルト実装。
    /// </summary>
    public class TxFactory : ITxFactory
    {
        /// <inheritdoc />
        public IReadableTx BeginRead()
        {
            return new ReadableTxImpl();
        }

        /// <inheritdoc />
        public IReadWriteTx BeginWrite()
        {
            return new ReadWriteTxImpl();
        }

        /// <summary>
        /// 読み取り専用トランザクションの内部実装。
        /// </summary>
        private sealed class ReadableTxImpl : IReadableTx
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// 読み書き可能トランザクションの内部実装。
        /// </summary>
        private sealed class ReadWriteTxImpl : IReadWriteTx
        {
            private bool _disposed;
            private bool _committed;

            public void Commit()
            {
                if (_disposed)
                {
                    return;
                }
                _committed = true;
            }

            public void Rollback()
            {
                if (_disposed)
                {
                    return;
                }
                _committed = false;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }
                
                if (!_committed)
                {
                    // 自動ロールバック
                }

                _disposed = true;
            }
        }
    }
}
