using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Lilja.Repository.Analyzer.Models;

namespace Lilja.Repository.Analyzer.Analysis;

internal readonly struct EntityAnalysisResult
{
    public EntityAnalysisResult(EntityInfo? entity, ImmutableArray<Diagnostic> diagnostics)
    {
        Entity = entity;
        Diagnostics = diagnostics;
    }

    public EntityInfo? Entity { get; }

    public ImmutableArray<Diagnostic> Diagnostics { get; }
}
