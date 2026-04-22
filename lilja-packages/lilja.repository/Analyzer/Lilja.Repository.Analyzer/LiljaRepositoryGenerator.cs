using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Lilja.Repository.Analyzer;

[Generator]
public sealed partial class LiljaRepositoryGenerator : IIncrementalGenerator
{
    private const string EntityAttributeMetadataName = "Lilja.Repository.EntityAttribute";
    private const string KeyAttributeMetadataName = "Lilja.Repository.KeyAttribute";
    private const string PersistAttributeMetadataName = "Lilja.Repository.PersistAttribute";
    private const string ToPrimitiveAttributeMetadataName = "Lilja.Repository.ToPrimitiveAttribute";
    private const string FromPrimitiveAttributeMetadataName = "Lilja.Repository.FromPrimitiveAttribute";
    private static readonly SymbolDisplayFormat FullyQualifiedTypeFormat = SymbolDisplayFormat.FullyQualifiedFormat;

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var entityAnalyses = context.SyntaxProvider.ForAttributeWithMetadataName(
            EntityAttributeMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (attributeContext, _) => AnalyzeEntity((INamedTypeSymbol)attributeContext.TargetSymbol))
            .Collect();

        var input = context.CompilationProvider.Combine(entityAnalyses);
        context.RegisterSourceOutput(input, static (productionContext, pair) =>
        {
            var compilation = pair.Left;
            var analyses = pair.Right;
            var hasMessagePack = compilation.GetTypeByMetadataName("MessagePack.Formatters.IMessagePackFormatter`1") is not null;

            foreach (var analysis in analyses)
            {
                foreach (var diagnostic in analysis.Diagnostics)
                {
                    productionContext.ReportDiagnostic(diagnostic);
                }

                if (analysis.Model is null || analysis.Diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
                {
                    continue;
                }

                EmitEntity(productionContext, analysis.Model, hasMessagePack);
            }
        });
    }
}
