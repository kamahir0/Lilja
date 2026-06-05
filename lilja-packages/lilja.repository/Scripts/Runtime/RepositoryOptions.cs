using System;

namespace Lilja.Repository
{
    [Flags]
    public enum RepositoryOptions
    {
        None = 0,
        InMemory = 1,
        Json = 2,
        MessagePack = 4,
    }
}
