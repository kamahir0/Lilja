using Lilja.Repository;

namespace RepositoryTest
{
    [Entity]
    public partial class PlayerData
    {
        [Persist(0)] private int _score;
    }
}