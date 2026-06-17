using System;

namespace Lilja.Repository
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class EntityAttribute : Attribute
    {
        public EntityAttribute(RepositoryOptions repositoryOptions = RepositoryOptions.None)
        {
            RepositoryOptions = repositoryOptions;
        }

        public RepositoryOptions RepositoryOptions { get; }
    }
}
