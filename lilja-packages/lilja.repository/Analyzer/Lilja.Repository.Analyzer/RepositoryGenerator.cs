using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Lilja.Repository.Analyzer.Analysis;
using Lilja.Repository.Analyzer.Emitters;
using Lilja.Repository.Analyzer.Models;

namespace Lilja.Repository.Analyzer;

/// <summary>
/// Lilja.Repository Source Generator。
/// [Entity]属性を持つクラスからDTO、ITransferable実装、Formatter、Repositoryを生成する。
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class RepositoryGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 属性定義を埋め込み生成（RuntimeにあるものをSource Generator側でも認識できるようにする）
        // 注: Runtimeに定義があるため、ここでは生成しない

        // [Entity]属性を持つクラスを検出
        var entityClasses = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Lilja.Repository.EntityAttribute",
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, _) => GetEntityInfo(ctx))
            .Where(static info => info.HasValue)
            .Select(static (info, _) => info!.Value);

        // コンパイルと結合して出力
        context.RegisterSourceOutput(entityClasses, static (spc, entity) =>
        {
            GenerateDto(spc, entity);
            GenerateTransferable(spc, entity);
            GenerateFormatter(spc, entity);
            GenerateRepositoryInterface(spc, entity);
            GenerateRepositoryImplementation(spc, entity);
        });
    }

    private static EntityInfo? GetEntityInfo(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol classSymbol)
        {
            return null;
        }

        return EntityAnalyzer.Analyze(classSymbol, context.SemanticModel.Compilation);
    }

    private static void GenerateDto(SourceProductionContext context, EntityInfo entity)
    {
        var source = DtoEmitter.Emit(entity);
        context.AddSource($"{entity.ClassName}Dto.g.cs", source);
    }

    private static void GenerateTransferable(SourceProductionContext context, EntityInfo entity)
    {
        var source = TransferableEmitter.Emit(entity);
        context.AddSource($"{entity.ClassName}.Transferable.g.cs", source);
    }

    private static void GenerateFormatter(SourceProductionContext context, EntityInfo entity)
    {
        var source = FormatterEmitter.Emit(entity);
        context.AddSource($"{entity.ClassName}DtoFormatter.g.cs", source);
    }

    private static void GenerateRepositoryInterface(SourceProductionContext context, EntityInfo entity)
    {
        var source = RepositoryEmitter.EmitInterface(entity);
        context.AddSource($"I{entity.ClassName}Repository.g.cs", source);
    }

    private static void GenerateRepositoryImplementation(SourceProductionContext context, EntityInfo entity)
    {
        var source = RepositoryEmitter.EmitInMemoryImplementation(entity);
        context.AddSource($"InMemory{entity.ClassName}Repository.g.cs", source);
    }
}
