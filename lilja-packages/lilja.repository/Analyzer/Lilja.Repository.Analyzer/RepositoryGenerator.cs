using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Lilja.Repository.Analyzer.Analysis;
using Lilja.Repository.Analyzer.Emitters;
using Lilja.Repository.Analyzer.Models;

namespace Lilja.Repository.Analyzer;

/// <summary>
/// Lilja.Repository Source Generator。
/// [Entity]属性を持つクラスからDTO、Converter、Formatter、Repositoryを生成する。
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class RepositoryGenerator : IIncrementalGenerator
{
    private const string MessagePackFormatterTypeName = "MessagePack.Formatters.IMessagePackFormatter`1";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var entityAnalyses = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Lilja.Repository.EntityAttribute",
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, _) => GetEntityAnalysis(ctx));

        var hasMessagePack = context.CompilationProvider
            .Select(static (compilation, _) => compilation.GetTypeByMetadataName(MessagePackFormatterTypeName) != null);

        context.RegisterSourceOutput(entityAnalyses.Combine(hasMessagePack), static (spc, pair) =>
        {
            var (analysis, messagePackAvailable) = pair;

            foreach (var diagnostic in analysis.Diagnostics)
            {
                spc.ReportDiagnostic(diagnostic);
            }

            if (!analysis.Entity.HasValue)
            {
                return;
            }

            var entity = analysis.Entity.Value;

            GenerateRepositoryInterface(spc, entity);
            GenerateInMemoryRepository(spc, entity);

            if (entity.HasKey)
            {
                GenerateKeyAccessor(spc, entity);
            }

            if (!entity.HasPersistMembers)
            {
                return;
            }

            GenerateDto(spc, entity);
            GenerateConverter(spc, entity);
            GenerateStorageEnvelope(spc, entity);
            GenerateJsonRepository(spc, entity);

            if (messagePackAvailable)
            {
                GenerateFormatter(spc, entity);
                GenerateStorageEnvelopeFormatter(spc, entity);
                GenerateMessagePackRepository(spc, entity);
            }
        });
    }

    private static EntityAnalysisResult GetEntityAnalysis(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol classSymbol)
        {
            return default;
        }

        return EntityAnalyzer.Analyze(classSymbol);
    }

    private static void GenerateDto(SourceProductionContext context, EntityInfo entity)
    {
        context.AddSource($"{entity.ClassName}Dto.g.cs", DtoEmitter.Emit(entity));
    }

    private static void GenerateConverter(SourceProductionContext context, EntityInfo entity)
    {
        context.AddSource($"{entity.ClassName}.Converter.g.cs", ConverterEmitter.Emit(entity));
    }

    private static void GenerateFormatter(SourceProductionContext context, EntityInfo entity)
    {
        context.AddSource($"{entity.ClassName}DtoFormatter.g.cs", FormatterEmitter.EmitDtoFormatter(entity));
    }

    private static void GenerateStorageEnvelope(SourceProductionContext context, EntityInfo entity)
    {
        context.AddSource($"{entity.ClassName}StorageEnvelope.g.cs", StorageEnvelopeEmitter.Emit(entity));
    }

    private static void GenerateStorageEnvelopeFormatter(SourceProductionContext context, EntityInfo entity)
    {
        context.AddSource($"{entity.ClassName}StorageEnvelopeFormatter.g.cs", FormatterEmitter.EmitStorageEnvelopeFormatter(entity));
    }

    private static void GenerateRepositoryInterface(SourceProductionContext context, EntityInfo entity)
    {
        context.AddSource($"I{entity.ClassName}Repository.g.cs", RepositoryEmitter.EmitInterface(entity));
    }

    private static void GenerateInMemoryRepository(SourceProductionContext context, EntityInfo entity)
    {
        context.AddSource($"InMemory{entity.ClassName}Repository.g.cs", RepositoryEmitter.EmitInMemoryImplementation(entity));
    }

    private static void GenerateJsonRepository(SourceProductionContext context, EntityInfo entity)
    {
        context.AddSource($"Json{entity.ClassName}Repository.g.cs", RepositoryEmitter.EmitJsonImplementation(entity));
    }

    private static void GenerateKeyAccessor(SourceProductionContext context, EntityInfo entity)
    {
        context.AddSource($"{entity.ClassName}.KeyAccessor.g.cs", KeyAccessorEmitter.Emit(entity));
    }

    private static void GenerateMessagePackRepository(SourceProductionContext context, EntityInfo entity)
    {
        context.AddSource($"MessagePack{entity.ClassName}Repository.g.cs", RepositoryEmitter.EmitMessagePackImplementation(entity));
    }
}
