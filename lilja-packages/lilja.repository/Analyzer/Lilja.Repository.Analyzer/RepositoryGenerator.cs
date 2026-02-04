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
    /// <summary>
    /// MessagePackのIMessagePackFormatter型の完全修飾名。
    /// </summary>
    private const string MessagePackFormatterTypeName = "MessagePack.Formatters.IMessagePackFormatter`1";

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

        // MessagePackの存在確認用にCompilationを取得
        var hasMessagePack = context.CompilationProvider
            .Select(static (compilation, _) => HasMessagePackReference(compilation));

        // EntityとMessagePack存在フラグを結合
        var entityWithContext = entityClasses.Combine(hasMessagePack);

        // コンパイルと結合して出力
        context.RegisterSourceOutput(entityWithContext, static (spc, tuple) =>
        {
            var (entity, messagePackAvailable) = tuple;

            GenerateDto(spc, entity);
            GenerateTransferable(spc, entity);

            // MessagePackが参照されている場合のみFormatter生成
            if (messagePackAvailable)
            {
                GenerateFormatter(spc, entity);
            }

            GenerateRepositoryInterface(spc, entity);
            GenerateInMemoryRepository(spc, entity);
            GenerateJsonRepository(spc, entity);

            // MessagePackが参照されている場合のみMessagePackRepository生成
            if (messagePackAvailable)
            {
                GenerateMessagePackRepository(spc, entity);
            }
        });
    }

    /// <summary>
    /// コンパイル対象のアセンブリでMessagePackが参照されているか確認する。
    /// </summary>
    private static bool HasMessagePackReference(Compilation compilation)
    {
        // IMessagePackFormatter<T>型が存在するか確認
        var formatterType = compilation.GetTypeByMetadataName(MessagePackFormatterTypeName);
        return formatterType != null;
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

    private static void GenerateInMemoryRepository(SourceProductionContext context, EntityInfo entity)
    {
        var source = RepositoryEmitter.EmitInMemoryImplementation(entity);
        context.AddSource($"InMemory{entity.ClassName}Repository.g.cs", source);
    }

    private static void GenerateJsonRepository(SourceProductionContext context, EntityInfo entity)
    {
        var source = RepositoryEmitter.EmitJsonImplementation(entity);
        context.AddSource($"Json{entity.ClassName}Repository.g.cs", source);
    }

    private static void GenerateMessagePackRepository(SourceProductionContext context, EntityInfo entity)
    {
        var source = RepositoryEmitter.EmitMessagePackImplementation(entity);
        context.AddSource($"MessagePack{entity.ClassName}Repository.g.cs", source);
    }
}
