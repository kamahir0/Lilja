#nullable enable
using System.Collections.Generic;

namespace Lilja.Persistence
{
    public interface IStagingSnapshot<TDto>
    {
        IReadOnlyList<TDto> ExportDtos();

        void ImportDtos(IEnumerable<TDto>? dtos);
    }
}
