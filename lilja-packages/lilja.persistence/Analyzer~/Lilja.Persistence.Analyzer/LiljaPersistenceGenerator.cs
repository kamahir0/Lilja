using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Lilja.Persistence.Analyzer;

[Generator(LanguageNames.CSharp)]
public sealed class LiljaPersistenceGenerator : IIncrementalGenerator
{
    private const string PersistableAttributeName = "Lilja.Persistence.PersistableAttribute";
    private const string PersistAttributeName = "Lilja.Persistence.PersistAttribute";
    private const string KeyAttributeName = "Lilja.Persistence.KeyAttribute";
    private const string ToPrimitiveAttributeName = "Lilja.Persistence.ToPrimitiveAttribute";
    private const string FromPrimitiveAttributeName = "Lilja.Persistence.FromPrimitiveAttribute";
    private const string KeyedStagingMetadataName = "Lilja.Persistence.KeyedStaging`2";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var analyses = context.SyntaxProvider.ForAttributeWithMetadataName(
                PersistableAttributeName,
                static (node, _) => node is ClassDeclarationSyntax,
                static (context, _) => AnalyzePersistable(context))
            .Collect();

        var combined = context.CompilationProvider.Combine(analyses);
        context.RegisterSourceOutput(combined, static (context, source) =>
        {
            var compilation = source.Left;
            var analyses = source.Right;
            var models = analyses.Select(static analysis => analysis.Model).Where(static model => model is not null).Cast<PersistableModel>().ToArray();

            foreach (var analysis in analyses)
            {
                foreach (var diagnostic in analysis.Diagnostics)
                {
                    context.ReportDiagnostic(diagnostic);
                }
            }

            if (models.Length == 0 || analyses.Any(static analysis => analysis.Diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)))
            {
                return;
            }

            var modelBySymbol = models.ToDictionary(static model => model.Symbol, SymbolEqualityComparer.Default);
            var hasMessagePack = MessagePackContract.HasCompatibleContract(compilation);

            foreach (var model in models)
            {
                AddSource(context, model, $"{model.Name}Dto.g.cs", GenerateDto(model));
                AddSource(context, model, $"{model.Name}.Persistence.g.cs", GeneratePersistablePartial(model));

                if (model.IsKeyed && !model.IsRoot)
                {
                    AddSource(context, model, $"{model.Name}Staging.g.cs", GenerateStaging(model));
                }

                if (model.IsRoot)
                {
                    AddSource(context, model, $"I{model.Name}Repository.g.cs", GenerateRepositoryInterface(model));
                    AddSource(context, model, $"Json{model.Name}Repository.g.cs", GenerateJsonRepository(model));
                    AddSource(context, model, $"InMemory{model.Name}Repository.g.cs", GenerateInMemoryRepository(model));
                }

                if (hasMessagePack)
                {
                    AddSource(context, model, $"{model.Name}DtoFormatter.g.cs", GenerateDtoFormatter(model));
                    if (model.IsRoot)
                    {
                        AddSource(context, model, $"MessagePack{model.Name}Repository.g.cs", GenerateMessagePackRepository(model, models));
                    }
                }
            }
        });
    }

    private static PersistableAnalysis AnalyzePersistable(GeneratorAttributeSyntaxContext context)
    {
        var symbol = (INamedTypeSymbol)context.TargetSymbol;
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        if (!IsPartial(symbol))
        {
            diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.PersistableMustBePartial, symbol.Locations.FirstOrDefault()));
        }

        var isRoot = IsRoot(symbol);
        var members = new List<MemberModel>();
        var keyMembers = new List<MemberModel>();
        var seenIndexes = new HashSet<int>();

        foreach (var member in symbol.GetMembers())
        {
            if (member is not IFieldSymbol && member is not IPropertySymbol)
            {
                continue;
            }

            var hasPersist = TryGetPersistIndex(member, out var index);
            var hasKey = HasAttribute(member, KeyAttributeName);
            if (!hasPersist && !hasKey)
            {
                continue;
            }

            if (member.IsStatic)
            {
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.UnsupportedMember, member.Locations.FirstOrDefault(), member.Name, "static members are not supported"));
                continue;
            }

            if (hasKey && !hasPersist)
            {
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.KeyMustBePersisted, member.Locations.FirstOrDefault(), member.Name));
                continue;
            }

            if (hasPersist && index < 0)
            {
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.PersistIndexMustBeNonNegative, member.Locations.FirstOrDefault()));
                continue;
            }

            if (hasPersist && !seenIndexes.Add(index))
            {
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.PersistIndexMustBeUnique, member.Locations.FirstOrDefault(), index));
                continue;
            }

            if (!hasPersist)
            {
                continue;
            }

            var type = GetMemberType(member);
            if (!TryCreateMemberModel(member, type, index, hasKey, out var model, out var reason))
            {
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.UnsupportedMember, member.Locations.FirstOrDefault(), member.Name, reason));
                continue;
            }

            members.Add(model);
            if (hasKey)
            {
                keyMembers.Add(model);
            }
        }

        var persistableModel = diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            ? null
            : new PersistableModel(symbol, isRoot, members.OrderBy(static member => member.Index).ToImmutableArray(), keyMembers.ToImmutableArray());

        return new PersistableAnalysis(persistableModel, diagnostics.ToImmutable());
    }

    private static bool TryCreateMemberModel(ISymbol member, ITypeSymbol type, int index, bool isKey, out MemberModel model, out string reason)
    {
        if (TryGetKeyedStagingArguments(type, out var stagingEntity, out var stagingKey))
        {
            if (!HasAttribute(stagingEntity, PersistableAttributeName))
            {
                model = default!;
                reason = "staging entity must be [Persistable]";
                return false;
            }

            if (!TryGetKeyTypeName(stagingEntity, out var actualKeyTypeName) ||
                actualKeyTypeName != GetTypeName(stagingKey))
            {
                model = default!;
                reason = "staging key type does not match the entity [Key] type";
                return false;
            }

            var dtoFieldType = $"global::System.Collections.Generic.List<{GetDtoTypeName(stagingEntity)}>";
            model = MemberModel.Create(member, type, index, isKey, MemberKind.Staging, dtoFieldType, stagingEntity);
            reason = string.Empty;
            return true;
        }

        if (TryGetListPersistableElement(type, out var listElement))
        {
            var dtoFieldType = $"global::System.Collections.Generic.List<{GetDtoTypeName(listElement)}>";
            model = MemberModel.Create(member, type, index, isKey, MemberKind.PersistableList, dtoFieldType, listElement);
            reason = string.Empty;
            return true;
        }

        if (HasAttribute(type, PersistableAttributeName))
        {
            model = MemberModel.Create(member, type, index, isKey, MemberKind.Persistable, GetDtoTypeName(type), type);
            reason = string.Empty;
            return true;
        }

        if (TryGetValueObject(type, out var primitiveType, out var toPrimitiveName, out var fromPrimitiveName, out var fromPrimitiveKind))
        {
            model = MemberModel.Create(member, type, index, isKey, MemberKind.ValueObject, GetTypeName(primitiveType), null, toPrimitiveName, fromPrimitiveName, fromPrimitiveKind);
            reason = string.Empty;
            return true;
        }

        model = MemberModel.Create(member, type, index, isKey, MemberKind.Value, GetTypeName(type));
        reason = string.Empty;
        return true;
    }

    private static string GenerateDto(PersistableModel model)
    {
        var sb = CreateSourceBuilder();
        using (AppendNamespace(sb, model.DtoNamespace))
        {
            sb.Append("[global::System.Serializable]\n");
            sb.Append("public sealed class ").Append(model.DtoName).Append("\n{\n");
            foreach (var member in model.PersistedMembers)
            {
                sb.Append("    public ").Append(member.DtoFieldTypeName).Append(' ').Append(member.EscapedName).Append(" = default!;\n");
            }

            sb.Append("}\n");
        }

        return sb.ToString();
    }

    private static string GeneratePersistablePartial(PersistableModel model)
    {
        var sb = CreateSourceBuilder();
        using (AppendNamespace(sb, model.NamespaceName))
        {
            sb.Append("public partial class ").Append(model.Name);
            if (model.IsKeyed)
            {
                sb.Append(" : global::Lilja.Persistence.IKeyed<").Append(model.KeyTypeName).Append('>');
            }

            sb.Append("\n{\n");
            AppendRestoreConstructor(sb, model);
            AppendToDto(sb, model);
            AppendFromDto(sb, model);

            if (model.IsKeyed)
            {
                AppendKeyImplementation(sb, model);
                AppendGetKeyFromDto(sb, model);
            }

            sb.Append("}\n");
        }

        return sb.ToString();
    }

    private static void AppendRestoreConstructor(StringBuilder sb, PersistableModel model)
    {
        sb.Append("    private ").Append(model.Name).Append("(global::Lilja.Persistence.RestoreToken _");
        foreach (var member in model.ConstructorMembers)
        {
            sb.Append(", ").Append(member.TypeName).Append(' ').Append(ToParameterName(member));
        }

        sb.Append(")\n    {\n");
        foreach (var member in model.ConstructorMembers)
        {
            sb.Append("        this.").Append(member.EscapedName).Append(" = ").Append(ToParameterName(member)).Append(";\n");
        }

        foreach (var member in model.StagingMembers)
        {
            sb.Append("        ").Append(member.EscapedName).Append(" = new ").Append(GetStagingTypeName(member.RelatedPersistable!)).Append("();\n");
        }

        sb.Append("    }\n\n");

        if (model.IsRoot && !HasParameterlessConstructor(model.Symbol))
        {
            sb.Append("    public ").Append(model.Name).Append("()\n");
            sb.Append("        : this(default(global::Lilja.Persistence.RestoreToken)");
            foreach (var member in model.ConstructorMembers)
            {
                sb.Append(", default!");
            }

            sb.Append(")\n    {\n    }\n\n");
        }
    }

    private static void AppendToDto(StringBuilder sb, PersistableModel model)
    {
        sb.Append("    public ").Append(model.DtoTypeName).Append(" ToDto()\n    {\n");
        sb.Append("        var dto = new ").Append(model.DtoTypeName).Append("();\n");
        foreach (var member in model.PersistedMembers)
        {
            AppendDtoAssignment(sb, member);
        }

        sb.Append("        return dto;\n");
        sb.Append("    }\n\n");
    }

    private static void AppendDtoAssignment(StringBuilder sb, MemberModel member)
    {
        switch (member.Kind)
        {
            case MemberKind.Staging:
                sb.Append("        dto.").Append(member.EscapedName).Append(" = new global::System.Collections.Generic.List<")
                    .Append(GetDtoTypeName(member.RelatedPersistable!)).Append(">(((")
                    .Append("global::Lilja.Persistence.IStagingSnapshot<").Append(GetDtoTypeName(member.RelatedPersistable!)).Append(">)")
                    .Append(member.EscapedName).Append(").ExportDtos());\n");
                return;
            case MemberKind.PersistableList:
                sb.Append("        dto.").Append(member.EscapedName).Append(" = new global::System.Collections.Generic.List<")
                    .Append(GetDtoTypeName(member.RelatedPersistable!)).Append(">();\n");
                sb.Append("        if (").Append(member.EscapedName).Append(" is not null)\n        {\n");
                sb.Append("            foreach (var item in ").Append(member.EscapedName).Append(")\n            {\n");
                sb.Append("                dto.").Append(member.EscapedName).Append(".Add(item.ToDto());\n");
                sb.Append("            }\n        }\n");
                return;
            case MemberKind.Persistable:
                sb.Append("        dto.").Append(member.EscapedName).Append(" = ").Append(member.EscapedName).Append("?.ToDto();\n");
                return;
            case MemberKind.ValueObject:
                sb.Append("        dto.").Append(member.EscapedName).Append(" = ").Append(member.EscapedName).Append('.').Append(member.ToPrimitiveName).Append("();\n");
                return;
            default:
                sb.Append("        dto.").Append(member.EscapedName).Append(" = ").Append(member.EscapedName).Append(";\n");
                return;
        }
    }

    private static void AppendFromDto(StringBuilder sb, PersistableModel model)
    {
        sb.Append("    public static ").Append(model.TypeName).Append(" FromDto(").Append(model.DtoTypeName).Append(" dto)\n    {\n");
        sb.Append("        if (dto is null)\n        {\n            throw new global::System.ArgumentNullException(nameof(dto));\n        }\n\n");

        foreach (var member in model.ConstructorMembers)
        {
            AppendFromDtoLocal(sb, member);
        }

        sb.Append("        var value = new ").Append(model.TypeName).Append("(default(global::Lilja.Persistence.RestoreToken)");
        foreach (var member in model.ConstructorMembers)
        {
            sb.Append(", ").Append(ToLocalName(member));
        }

        sb.Append(");\n");

        foreach (var member in model.StagingMembers)
        {
            sb.Append("        ((global::Lilja.Persistence.IStagingSnapshot<").Append(GetDtoTypeName(member.RelatedPersistable!)).Append(">)value.")
                .Append(member.EscapedName).Append(").ImportDtos(dto.").Append(member.EscapedName).Append(");\n");
        }

        sb.Append("        return value;\n");
        sb.Append("    }\n\n");
    }

    private static void AppendFromDtoLocal(StringBuilder sb, MemberModel member)
    {
        var localName = ToLocalName(member);
        switch (member.Kind)
        {
            case MemberKind.PersistableList:
                sb.Append("        var ").Append(localName).Append(" = new ").Append(member.TypeName).Append("();\n");
                sb.Append("        if (dto.").Append(member.EscapedName).Append(" is not null)\n        {\n");
                sb.Append("            foreach (var item in dto.").Append(member.EscapedName).Append(")\n            {\n");
                sb.Append("                ").Append(localName).Append(".Add(").Append(GetTypeName(member.RelatedPersistable!)).Append(".FromDto(item));\n");
                sb.Append("            }\n        }\n");
                return;
            case MemberKind.Persistable:
                sb.Append("        var ").Append(localName).Append(" = dto.").Append(member.EscapedName).Append(" is null ? null! : ")
                    .Append(member.TypeName).Append(".FromDto(dto.").Append(member.EscapedName).Append(");\n");
                return;
            case MemberKind.ValueObject:
                if (member.FromPrimitiveKind == FromPrimitiveKind.Constructor)
                {
                    sb.Append("        var ").Append(localName).Append(" = new ").Append(member.TypeName).Append("(dto.").Append(member.EscapedName).Append(");\n");
                }
                else
                {
                    sb.Append("        var ").Append(localName).Append(" = ").Append(member.TypeName).Append('.').Append(member.FromPrimitiveName)
                        .Append("(dto.").Append(member.EscapedName).Append(");\n");
                }

                return;
            default:
                sb.Append("        var ").Append(localName).Append(" = dto.").Append(member.EscapedName).Append(";\n");
                return;
        }
    }

    private static void AppendKeyImplementation(StringBuilder sb, PersistableModel model)
    {
        sb.Append("    ").Append(model.KeyTypeName).Append(" global::Lilja.Persistence.IKeyed<").Append(model.KeyTypeName).Append(">.Key => ");
        AppendKeyExpression(sb, model.KeyMembers, static member => member.EscapedName);
        sb.Append(";\n\n");
    }

    private static void AppendGetKeyFromDto(StringBuilder sb, PersistableModel model)
    {
        sb.Append("    internal static ").Append(model.KeyTypeName).Append(" GetKeyFromDto(").Append(model.DtoTypeName).Append(" dto)\n    {\n");
        sb.Append("        return ");
        AppendKeyExpression(sb, model.KeyMembers, member => ConvertFromDtoExpression(member, "dto." + member.EscapedName));
        sb.Append(";\n    }\n\n");
    }

    private static void AppendKeyExpression(StringBuilder sb, ImmutableArray<MemberModel> keyMembers, Func<MemberModel, string> selector)
    {
        if (keyMembers.Length == 1)
        {
            sb.Append(selector(keyMembers[0]));
            return;
        }

        sb.Append('(');
        for (var i = 0; i < keyMembers.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            sb.Append(selector(keyMembers[i]));
        }

        sb.Append(')');
    }

    private static string ConvertFromDtoExpression(MemberModel member, string expression)
    {
        if (member.Kind != MemberKind.ValueObject)
        {
            return expression;
        }

        return member.FromPrimitiveKind == FromPrimitiveKind.Constructor
            ? $"new {member.TypeName}({expression})"
            : $"{member.TypeName}.{member.FromPrimitiveName}({expression})";
    }

    private static string GenerateStaging(PersistableModel model)
    {
        var sb = CreateSourceBuilder();
        using (AppendNamespace(sb, model.NamespaceName))
        {
            sb.Append("public sealed partial class ").Append(model.Name).Append("Staging : global::Lilja.Persistence.KeyedStaging<")
                .Append(model.TypeName).Append(", ").Append(model.KeyTypeName).Append(", ").Append(model.DtoTypeName).Append(">\n{\n");
            sb.Append("    protected override ").Append(model.TypeName).Append(" ToEntity(").Append(model.DtoTypeName).Append(" dto)\n    {\n");
            sb.Append("        return ").Append(model.TypeName).Append(".FromDto(dto);\n    }\n\n");
            sb.Append("    protected override ").Append(model.DtoTypeName).Append(" ToDto(").Append(model.TypeName).Append(" entity)\n    {\n");
            sb.Append("        return entity.ToDto();\n    }\n\n");
            sb.Append("    protected override ").Append(model.KeyTypeName).Append(" GetKey(").Append(model.DtoTypeName).Append(" dto)\n    {\n");
            sb.Append("        return ").Append(model.TypeName).Append(".GetKeyFromDto(dto);\n    }\n");
            sb.Append("}\n");
        }

        return sb.ToString();
    }

    private static string GenerateJsonRepository(PersistableModel model)
    {
        var sb = CreateSourceBuilder();
        using (AppendNamespace(sb, model.RepositoryNamespace))
        {
            if (model.IsKeyed)
            {
                sb.Append("public sealed class Json").Append(model.Name).Append("Repository : global::Lilja.Persistence.JsonKeyedRepository<")
                    .Append(model.KeyTypeName).Append(", ").Append(model.TypeName).Append(", ").Append(model.DtoTypeName).Append(">, ")
                    .Append(model.RepositoryInterfaceTypeName).Append("\n{\n");
                AppendKeyedRepositoryBody(sb, model, "json");
                sb.Append("}\n");
            }
            else
            {
                sb.Append("public sealed class Json").Append(model.Name).Append("Repository : global::Lilja.Persistence.JsonRepository<")
                    .Append(model.TypeName).Append(", ").Append(model.DtoTypeName).Append(">, ")
                    .Append(model.RepositoryInterfaceTypeName).Append("\n{\n");
                sb.Append("    public Json").Append(model.Name).Append("Repository()\n");
                sb.Append("        : base(global::System.IO.Path.Combine(global::UnityEngine.Application.persistentDataPath, \"")
                    .Append(model.StorageIdentifier).Append(".json\"))\n    {\n    }\n\n");
                AppendSingleRepositoryBody(sb, model);
                sb.Append("}\n");
            }
        }

        return sb.ToString();
    }

    private static string GenerateRepositoryInterface(PersistableModel model)
    {
        var sb = CreateSourceBuilder();
        using (AppendNamespace(sb, model.RepositoryNamespace))
        {
            sb.Append("public interface ").Append(model.RepositoryInterfaceName).Append("\n{\n");
            if (model.IsKeyed)
            {
                sb.Append("    global::Cysharp.Threading.Tasks.UniTask<").Append(model.TypeName).Append("> LoadAsync(")
                    .Append(model.KeyTypeName).Append(" key, global::System.Threading.CancellationToken ct = default);\n\n");
                sb.Append("    global::Cysharp.Threading.Tasks.UniTask<global::System.Collections.Generic.IReadOnlyList<")
                    .Append(model.TypeName).Append(">> LoadAllAsync(global::System.Threading.CancellationToken ct = default);\n\n");
            }
            else
            {
                sb.Append("    global::Cysharp.Threading.Tasks.UniTask<").Append(model.TypeName)
                    .Append("> LoadAsync(global::System.Threading.CancellationToken ct = default);\n\n");
            }

            sb.Append("    global::Cysharp.Threading.Tasks.UniTask SaveAsync(").Append(model.TypeName)
                .Append(" data, global::System.Threading.CancellationToken ct = default);\n");
            if (model.IsKeyed)
            {
                sb.Append("\n    bool Exists(").Append(model.KeyTypeName).Append(" key);\n");
            }

            sb.Append("}\n");
        }

        return sb.ToString();
    }

    private static string GenerateInMemoryRepository(PersistableModel model)
    {
        var sb = CreateSourceBuilder();
        using (AppendNamespace(sb, model.RepositoryNamespace))
        {
            if (model.IsKeyed)
            {
                sb.Append("public sealed class InMemory").Append(model.Name).Append("Repository : global::Lilja.Persistence.KeyedRepository<")
                    .Append(model.KeyTypeName).Append(", ").Append(model.TypeName).Append(">, ")
                    .Append(model.RepositoryInterfaceTypeName).Append("\n{\n");
                AppendInMemoryKeyedRepositoryBody(sb, model);
                sb.Append("}\n");
            }
            else
            {
                sb.Append("public sealed class InMemory").Append(model.Name).Append("Repository : global::Lilja.Persistence.Repository<")
                    .Append(model.TypeName).Append(">, ")
                    .Append(model.RepositoryInterfaceTypeName).Append("\n{\n");
                AppendInMemorySingleRepositoryBody(sb, model);
                sb.Append("}\n");
            }
        }

        return sb.ToString();
    }

    private static void AppendInMemoryKeyedRepositoryBody(StringBuilder sb, PersistableModel model)
    {
        sb.Append("    private readonly global::System.Collections.Generic.Dictionary<").Append(model.KeyTypeName).Append(", ")
            .Append(model.DtoTypeName).Append("> _values = new global::System.Collections.Generic.Dictionary<")
            .Append(model.KeyTypeName).Append(", ").Append(model.DtoTypeName).Append(">();\n\n");

        sb.Append("    public InMemory").Append(model.Name).Append("Repository()\n    {\n    }\n\n");

        sb.Append("    public InMemory").Append(model.Name).Append("Repository(global::System.Collections.Generic.IReadOnlyList<")
            .Append(model.TypeName).Append(">? initialValues)\n    {\n");
        sb.Append("        if (initialValues is null)\n        {\n            return;\n        }\n\n");
        sb.Append("        foreach (var value in initialValues)\n        {\n");
        sb.Append("            var key = ((global::Lilja.Persistence.IKeyed<").Append(model.KeyTypeName).Append(">)value).Key;\n");
        sb.Append("            _values[key] = value.ToDto();\n");
        sb.Append("        }\n");
        sb.Append("    }\n\n");

        sb.Append("    public override global::Cysharp.Threading.Tasks.UniTask<").Append(model.TypeName).Append("> LoadAsync(")
            .Append(model.KeyTypeName).Append(" key, global::System.Threading.CancellationToken ct = default)\n    {\n");
        sb.Append("        ct.ThrowIfCancellationRequested();\n");
        sb.Append("        if (_values.TryGetValue(key, out var dto))\n        {\n");
        sb.Append("            return global::Cysharp.Threading.Tasks.UniTask.FromResult(").Append(model.TypeName).Append(".FromDto(dto));\n");
        sb.Append("        }\n\n");
        sb.Append("        var defaultDto = new ").Append(model.DtoTypeName).Append("();\n");
        AppendKeyToDtoAssignments(sb, model, "defaultDto", "key");
        sb.Append("        return global::Cysharp.Threading.Tasks.UniTask.FromResult(").Append(model.TypeName).Append(".FromDto(defaultDto));\n");
        sb.Append("    }\n\n");

        sb.Append("    public override global::Cysharp.Threading.Tasks.UniTask<global::System.Collections.Generic.IReadOnlyList<")
            .Append(model.TypeName).Append(">> LoadAllAsync(global::System.Threading.CancellationToken ct = default)\n    {\n");
        sb.Append("        ct.ThrowIfCancellationRequested();\n");
        sb.Append("        var values = new global::System.Collections.Generic.List<").Append(model.TypeName).Append(">(_values.Count);\n");
        sb.Append("        foreach (var dto in _values.Values)\n        {\n");
        sb.Append("            values.Add(").Append(model.TypeName).Append(".FromDto(dto));\n");
        sb.Append("        }\n\n");
        sb.Append("        return global::Cysharp.Threading.Tasks.UniTask.FromResult((global::System.Collections.Generic.IReadOnlyList<")
            .Append(model.TypeName).Append(">)values);\n");
        sb.Append("    }\n\n");

        sb.Append("    public override global::Cysharp.Threading.Tasks.UniTask SaveAsync(").Append(model.TypeName)
            .Append(" data, global::System.Threading.CancellationToken ct = default)\n    {\n");
        sb.Append("        if (data is null)\n        {\n            throw new global::System.ArgumentNullException(nameof(data));\n        }\n\n");
        sb.Append("        ct.ThrowIfCancellationRequested();\n");
        sb.Append("        var key = ((global::Lilja.Persistence.IKeyed<").Append(model.KeyTypeName).Append(">)data).Key;\n");
        sb.Append("        _values[key] = data.ToDto();\n");
        sb.Append("        return global::Cysharp.Threading.Tasks.UniTask.CompletedTask;\n");
        sb.Append("    }\n\n");

        sb.Append("    public override bool Exists(").Append(model.KeyTypeName).Append(" key)\n    {\n");
        sb.Append("        return _values.ContainsKey(key);\n");
        sb.Append("    }\n");
    }

    private static void AppendInMemorySingleRepositoryBody(StringBuilder sb, PersistableModel model)
    {
        sb.Append("    private ").Append(model.DtoTypeName).Append("? _value;\n\n");

        sb.Append("    public InMemory").Append(model.Name).Append("Repository()\n    {\n    }\n\n");

        sb.Append("    public InMemory").Append(model.Name).Append("Repository(").Append(model.TypeName).Append("? initialValue)\n    {\n");
        sb.Append("        _value = initialValue?.ToDto();\n");
        sb.Append("    }\n\n");

        sb.Append("    public override global::Cysharp.Threading.Tasks.UniTask<").Append(model.TypeName)
            .Append("> LoadAsync(global::System.Threading.CancellationToken ct = default)\n    {\n");
        sb.Append("        ct.ThrowIfCancellationRequested();\n");
        sb.Append("        return global::Cysharp.Threading.Tasks.UniTask.FromResult(").Append(model.TypeName)
            .Append(".FromDto(_value ?? new ").Append(model.DtoTypeName).Append("()));\n");
        sb.Append("    }\n\n");

        sb.Append("    public override global::Cysharp.Threading.Tasks.UniTask SaveAsync(").Append(model.TypeName)
            .Append(" data, global::System.Threading.CancellationToken ct = default)\n    {\n");
        sb.Append("        if (data is null)\n        {\n            throw new global::System.ArgumentNullException(nameof(data));\n        }\n\n");
        sb.Append("        ct.ThrowIfCancellationRequested();\n");
        sb.Append("        _value = data.ToDto();\n");
        sb.Append("        return global::Cysharp.Threading.Tasks.UniTask.CompletedTask;\n");
        sb.Append("    }\n");
    }

    private static void AppendSingleRepositoryBody(StringBuilder sb, PersistableModel model)
    {
        sb.Append("    protected override ").Append(model.TypeName).Append(" CreateDefault()\n    {\n");
        sb.Append("        return ").Append(model.TypeName).Append(".FromDto(new ").Append(model.DtoTypeName).Append("());\n    }\n\n");
        sb.Append("    protected override ").Append(model.TypeName).Append(" FromDto(").Append(model.DtoTypeName).Append(" dto)\n    {\n");
        sb.Append("        return ").Append(model.TypeName).Append(".FromDto(dto);\n    }\n\n");
        sb.Append("    protected override ").Append(model.DtoTypeName).Append(" ToDto(").Append(model.TypeName).Append(" data)\n    {\n");
        sb.Append("        return data.ToDto();\n    }\n");
    }

    private static void AppendKeyedRepositoryBody(StringBuilder sb, PersistableModel model, string extension)
    {
        sb.Append("    protected override string FileExtension => \"").Append(extension).Append("\";\n\n");
        sb.Append("    protected override string GetDirectoryPath()\n    {\n");
        sb.Append("        return global::System.IO.Path.Combine(global::UnityEngine.Application.persistentDataPath, \"")
            .Append(model.StorageIdentifier).Append("\");\n    }\n\n");
        sb.Append("    protected override string GetFilePath(").Append(model.KeyTypeName).Append(" key)\n    {\n");
        sb.Append("        return global::System.IO.Path.Combine(GetDirectoryPath(), global::Lilja.Persistence.PersistenceFileName.Encode(key) + \".\" + FileExtension);\n    }\n\n");
        sb.Append("    protected override ").Append(model.TypeName).Append(" CreateDefault(").Append(model.KeyTypeName).Append(" key)\n    {\n");
        sb.Append("        var dto = new ").Append(model.DtoTypeName).Append("();\n");
        AppendKeyToDtoAssignments(sb, model, "dto", "key");
        sb.Append("        return ").Append(model.TypeName).Append(".FromDto(dto);\n    }\n\n");
        sb.Append("    protected override ").Append(model.TypeName).Append(" FromDto(").Append(model.DtoTypeName).Append(" dto)\n    {\n");
        sb.Append("        return ").Append(model.TypeName).Append(".FromDto(dto);\n    }\n\n");
        sb.Append("    protected override ").Append(model.DtoTypeName).Append(" ToDto(").Append(model.TypeName).Append(" data)\n    {\n");
        sb.Append("        return data.ToDto();\n    }\n");
    }

    private static void AppendKeyToDtoAssignments(StringBuilder sb, PersistableModel model, string dtoName, string keyName)
    {
        for (var i = 0; i < model.KeyMembers.Length; i++)
        {
            var member = model.KeyMembers[i];
            var keyExpression = model.KeyMembers.Length == 1 ? keyName : $"{keyName}.Item{i + 1}";
            if (member.Kind == MemberKind.ValueObject)
            {
                keyExpression += "." + member.ToPrimitiveName + "()";
            }

            sb.Append("        ").Append(dtoName).Append('.').Append(member.EscapedName).Append(" = ").Append(keyExpression).Append(";\n");
        }
    }

    private static string GenerateDtoFormatter(PersistableModel model)
    {
        var sb = CreateSourceBuilder();
        using (AppendNamespace(sb, model.FormatterNamespace))
        {
            sb.Append("public sealed class ").Append(model.Name).Append("DtoFormatter : global::MessagePack.Formatters.IMessagePackFormatter<")
                .Append(model.DtoTypeName).Append(">\n{\n");
            sb.Append("    private static global::MessagePack.Formatters.IMessagePackFormatter<T> ResolveFormatter<T>(global::MessagePack.MessagePackSerializerOptions options)\n    {\n");
            sb.Append("        return options.Resolver.GetFormatter<T>() ?? throw new global::MessagePack.MessagePackSerializationException($\"Formatter not found for {typeof(T).FullName}.\");\n    }\n\n");
            sb.Append("    public void Serialize(ref global::MessagePack.MessagePackWriter writer, ").Append(model.DtoTypeName)
                .Append(" value, global::MessagePack.MessagePackSerializerOptions options)\n    {\n");
            sb.Append("        if (value is null)\n        {\n            writer.WriteNil();\n            return;\n        }\n");
            sb.Append("        writer.WriteArrayHeader(").Append(model.PersistedMembers.Length).Append(");\n");
            foreach (var member in model.PersistedMembers)
            {
                sb.Append("        ResolveFormatter<").Append(member.DtoFieldTypeName).Append(">(options).Serialize(ref writer, value.")
                    .Append(member.EscapedName).Append(", options);\n");
            }

            sb.Append("    }\n\n");
            sb.Append("    public ").Append(model.DtoTypeName).Append(" Deserialize(ref global::MessagePack.MessagePackReader reader, global::MessagePack.MessagePackSerializerOptions options)\n    {\n");
            sb.Append("        if (reader.TryReadNil())\n        {\n            return null!;\n        }\n");
            sb.Append("        var value = new ").Append(model.DtoTypeName).Append("();\n");
            sb.Append("        var length = reader.ReadArrayHeader();\n");
            for (var i = 0; i < model.PersistedMembers.Length; i++)
            {
                var member = model.PersistedMembers[i];
                sb.Append("        if (length > ").Append(i).Append(")\n        {\n");
                sb.Append("            value.").Append(member.EscapedName).Append(" = ResolveFormatter<").Append(member.DtoFieldTypeName)
                    .Append(">(options).Deserialize(ref reader, options);\n");
                sb.Append("        }\n");
            }

            sb.Append("        for (var index = ").Append(model.PersistedMembers.Length).Append("; index < length; index++)\n        {\n            reader.Skip();\n        }\n");
            sb.Append("        return value;\n    }\n");
            sb.Append("}\n");
        }

        return sb.ToString();
    }

    private static string GenerateMessagePackRepository(PersistableModel model, IReadOnlyList<PersistableModel> allModels)
    {
        var sb = CreateSourceBuilder();
        using (AppendNamespace(sb, model.RepositoryNamespace))
        {
            if (model.IsKeyed)
            {
                sb.Append("public sealed class MessagePack").Append(model.Name).Append("Repository : global::Lilja.Persistence.KeyedRepository<")
                    .Append(model.KeyTypeName).Append(", ").Append(model.TypeName).Append(">, ")
                    .Append(model.RepositoryInterfaceTypeName).Append("\n{\n");
            }
            else
            {
                sb.Append("public sealed class MessagePack").Append(model.Name).Append("Repository : global::Lilja.Persistence.Repository<")
                    .Append(model.TypeName).Append(">, ")
                    .Append(model.RepositoryInterfaceTypeName).Append("\n{\n");
            }

            sb.Append("    private readonly global::MessagePack.MessagePackSerializerOptions _options;\n\n");
            sb.Append("    public MessagePack").Append(model.Name).Append("Repository()\n    {\n");
            AppendMessagePackResolver(sb, allModels);
            sb.Append("    }\n\n");

            if (model.IsKeyed)
            {
                AppendMessagePackKeyedMethods(sb, model);
            }
            else
            {
                AppendMessagePackSingleMethods(sb, model);
            }

            sb.Append("}\n");
        }

        return sb.ToString();
    }

    private static void AppendMessagePackResolver(StringBuilder sb, IReadOnlyList<PersistableModel> allModels)
    {
        sb.Append("        var resolver = global::MessagePack.Resolvers.CompositeResolver.Create(\n");
        sb.Append("            new global::MessagePack.Formatters.IMessagePackFormatter[]\n            {\n");
        foreach (var model in allModels)
        {
            sb.Append("                new global::").Append(model.FormatterNamespace).Append('.').Append(model.Name).Append("DtoFormatter(),\n");
        }

        sb.Append("            },\n");
        sb.Append("            new global::MessagePack.IFormatterResolver[]\n            {\n");
        sb.Append("                global::MessagePack.Resolvers.StandardResolver.Instance,\n");
        sb.Append("            });\n");
        sb.Append("        _options = global::MessagePack.MessagePackSerializerOptions.Standard.WithResolver(resolver);\n");
    }

    private static void AppendMessagePackSingleMethods(StringBuilder sb, PersistableModel model)
    {
        sb.Append("    public override global::Cysharp.Threading.Tasks.UniTask<").Append(model.TypeName).Append("> LoadAsync(global::System.Threading.CancellationToken ct = default)\n    {\n");
        sb.Append("        var path = global::System.IO.Path.Combine(global::UnityEngine.Application.persistentDataPath, \"").Append(model.StorageIdentifier).Append(".msgpack\");\n");
        sb.Append("        return global::Cysharp.Threading.Tasks.UniTask.RunOnThreadPool(() =>\n        {\n");
        sb.Append("            ct.ThrowIfCancellationRequested();\n");
        sb.Append("            if (!global::System.IO.File.Exists(path))\n            {\n                return ").Append(model.TypeName).Append(".FromDto(new ").Append(model.DtoTypeName).Append("());\n            }\n");
        sb.Append("            var bytes = global::System.IO.File.ReadAllBytes(path);\n");
        sb.Append("            var dto = global::MessagePack.MessagePackSerializer.Deserialize<").Append(model.DtoTypeName).Append(">(bytes, _options);\n");
        sb.Append("            return ").Append(model.TypeName).Append(".FromDto(dto);\n");
        sb.Append("        }, cancellationToken: ct);\n    }\n\n");
        sb.Append("    public override global::Cysharp.Threading.Tasks.UniTask SaveAsync(").Append(model.TypeName).Append(" data, global::System.Threading.CancellationToken ct = default)\n    {\n");
        sb.Append("        if (data is null) { throw new global::System.ArgumentNullException(nameof(data)); }\n");
        sb.Append("        var path = global::System.IO.Path.Combine(global::UnityEngine.Application.persistentDataPath, \"").Append(model.StorageIdentifier).Append(".msgpack\");\n");
        sb.Append("        return global::Cysharp.Threading.Tasks.UniTask.RunOnThreadPool(() =>\n        {\n");
        sb.Append("            ct.ThrowIfCancellationRequested();\n");
        sb.Append("            var bytes = global::MessagePack.MessagePackSerializer.Serialize(data.ToDto(), _options);\n");
        sb.Append("            global::Lilja.Persistence.AtomicFileWriter.WriteAllBytes(path, bytes);\n");
        sb.Append("        }, cancellationToken: ct);\n    }\n");
    }

    private static void AppendMessagePackKeyedMethods(StringBuilder sb, PersistableModel model)
    {
        sb.Append("    public override global::Cysharp.Threading.Tasks.UniTask<").Append(model.TypeName).Append("> LoadAsync(").Append(model.KeyTypeName).Append(" key, global::System.Threading.CancellationToken ct = default)\n    {\n");
        sb.Append("        var path = GetFilePath(key);\n");
        sb.Append("        return global::Cysharp.Threading.Tasks.UniTask.RunOnThreadPool(() =>\n        {\n");
        sb.Append("            ct.ThrowIfCancellationRequested();\n");
        sb.Append("            if (!global::System.IO.File.Exists(path))\n            {\n                var missing = new ").Append(model.DtoTypeName).Append("();\n");
        AppendKeyToDtoAssignments(sb, model, "missing", "key");
        sb.Append("                return ").Append(model.TypeName).Append(".FromDto(missing);\n            }\n");
        sb.Append("            var bytes = global::System.IO.File.ReadAllBytes(path);\n");
        sb.Append("            var dto = global::MessagePack.MessagePackSerializer.Deserialize<").Append(model.DtoTypeName).Append(">(bytes, _options);\n");
        sb.Append("            return ").Append(model.TypeName).Append(".FromDto(dto);\n");
        sb.Append("        }, cancellationToken: ct);\n    }\n\n");
        sb.Append("    public override global::Cysharp.Threading.Tasks.UniTask<global::System.Collections.Generic.IReadOnlyList<").Append(model.TypeName).Append(">> LoadAllAsync(global::System.Threading.CancellationToken ct = default)\n    {\n");
        sb.Append("        var directoryPath = GetDirectoryPath();\n");
        sb.Append("        return global::Cysharp.Threading.Tasks.UniTask.RunOnThreadPool<global::System.Collections.Generic.IReadOnlyList<").Append(model.TypeName).Append(">>(() =>\n        {\n");
        sb.Append("            ct.ThrowIfCancellationRequested();\n");
        sb.Append("            if (!global::System.IO.Directory.Exists(directoryPath))\n            {\n                return global::System.Array.Empty<").Append(model.TypeName).Append(">();\n            }\n");
        sb.Append("            var filePaths = global::System.IO.Directory.GetFiles(directoryPath, \"*.msgpack\");\n");
        sb.Append("            var dataList = new global::System.Collections.Generic.List<").Append(model.TypeName).Append(">(filePaths.Length);\n");
        sb.Append("            foreach (var filePath in filePaths)\n            {\n");
        sb.Append("                ct.ThrowIfCancellationRequested();\n");
        sb.Append("                var bytes = global::System.IO.File.ReadAllBytes(filePath);\n");
        sb.Append("                var dto = global::MessagePack.MessagePackSerializer.Deserialize<").Append(model.DtoTypeName).Append(">(bytes, _options);\n");
        sb.Append("                dataList.Add(").Append(model.TypeName).Append(".FromDto(dto));\n");
        sb.Append("            }\n");
        sb.Append("            return dataList;\n");
        sb.Append("        }, cancellationToken: ct);\n    }\n\n");
        sb.Append("    public override global::Cysharp.Threading.Tasks.UniTask SaveAsync(").Append(model.TypeName).Append(" data, global::System.Threading.CancellationToken ct = default)\n    {\n");
        sb.Append("        if (data is null) { throw new global::System.ArgumentNullException(nameof(data)); }\n");
        sb.Append("        var path = GetFilePath(((global::Lilja.Persistence.IKeyed<").Append(model.KeyTypeName).Append(">)data).Key);\n");
        sb.Append("        return global::Cysharp.Threading.Tasks.UniTask.RunOnThreadPool(() =>\n        {\n");
        sb.Append("            ct.ThrowIfCancellationRequested();\n");
        sb.Append("            var bytes = global::MessagePack.MessagePackSerializer.Serialize(data.ToDto(), _options);\n");
        sb.Append("            global::Lilja.Persistence.AtomicFileWriter.WriteAllBytes(path, bytes);\n");
        sb.Append("        }, cancellationToken: ct);\n    }\n\n");
        sb.Append("    public override bool Exists(").Append(model.KeyTypeName).Append(" key)\n    {\n");
        sb.Append("        return global::System.IO.File.Exists(GetFilePath(key));\n    }\n\n");
        sb.Append("    private static string GetDirectoryPath()\n    {\n");
        sb.Append("        return global::System.IO.Path.Combine(global::UnityEngine.Application.persistentDataPath, \"")
            .Append(model.StorageIdentifier).Append("\");\n    }\n\n");
        sb.Append("    private static string GetFilePath(").Append(model.KeyTypeName).Append(" key)\n    {\n");
        sb.Append("        return global::System.IO.Path.Combine(GetDirectoryPath(), global::Lilja.Persistence.PersistenceFileName.Encode(key) + \".msgpack\");\n    }\n");
    }

    private static string CreateStorageIdentifier(INamedTypeSymbol symbol)
    {
        return symbol.ContainingNamespace is null || symbol.ContainingNamespace.IsGlobalNamespace
            ? symbol.Name
            : symbol.ContainingNamespace.ToDisplayString() + "." + symbol.Name;
    }

    private static void AddSource(SourceProductionContext context, PersistableModel model, string fileName, string source)
    {
        context.AddSource(model.StorageIdentifier + "." + fileName, SourceText.From(source, Encoding.UTF8));
    }

    private static StringBuilder CreateSourceBuilder()
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        return sb;
    }

    private static NamespaceScope AppendNamespace(StringBuilder sb, string namespaceName)
    {
        if (!string.IsNullOrEmpty(namespaceName))
        {
            sb.Append("namespace ").Append(namespaceName).Append("\n{\n");
        }

        return new NamespaceScope(sb, namespaceName);
    }

    private readonly struct NamespaceScope : IDisposable
    {
        private readonly StringBuilder _sb;
        private readonly string _namespaceName;

        public NamespaceScope(StringBuilder sb, string namespaceName)
        {
            _sb = sb;
            _namespaceName = namespaceName;
        }

        public void Dispose()
        {
            if (!string.IsNullOrEmpty(_namespaceName))
            {
                _sb.Append("}\n");
            }
        }
    }

    private static string GetTypeName(ITypeSymbol symbol)
    {
        return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    private static string GetDtoNamespace(INamedTypeSymbol symbol)
    {
        var namespaceName = symbol.ContainingNamespace?.IsGlobalNamespace == false ? symbol.ContainingNamespace.ToDisplayString() : string.Empty;
        return string.IsNullOrEmpty(namespaceName)
            ? "Lilja.Persistence.Generated.Dtos"
            : "Lilja.Persistence.Generated.Dtos." + namespaceName;
    }

    private static string GetFormatterNamespace(INamedTypeSymbol symbol)
    {
        var namespaceName = symbol.ContainingNamespace?.IsGlobalNamespace == false ? symbol.ContainingNamespace.ToDisplayString() : string.Empty;
        return string.IsNullOrEmpty(namespaceName)
            ? "Lilja.Persistence.Generated.Formatters"
            : "Lilja.Persistence.Generated.Formatters." + namespaceName;
    }

    private static string GetDtoTypeName(ITypeSymbol symbol)
    {
        var named = (INamedTypeSymbol)symbol;
        return "global::" + GetDtoNamespace(named) + "." + named.Name + "Dto";
    }

    private static string GetStagingTypeName(ITypeSymbol symbol)
    {
        var named = (INamedTypeSymbol)symbol;
        var namespaceName = named.ContainingNamespace?.IsGlobalNamespace == false ? named.ContainingNamespace.ToDisplayString() : string.Empty;
        return string.IsNullOrEmpty(namespaceName)
            ? "global::" + named.Name + "Staging"
            : "global::" + namespaceName + "." + named.Name + "Staging";
    }

    private static ITypeSymbol GetMemberType(ISymbol member)
    {
        return member switch
        {
            IFieldSymbol field => field.Type,
            IPropertySymbol property => property.Type,
            _ => throw new InvalidOperationException()
        };
    }

    private static bool TryGetPersistIndex(ISymbol symbol, out int index)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (!IsAttribute(attribute, PersistAttributeName))
            {
                continue;
            }

            index = attribute.ConstructorArguments.Length == 1 ? (int)attribute.ConstructorArguments[0].Value! : -1;
            return true;
        }

        index = -1;
        return false;
    }

    private static bool IsRoot(INamedTypeSymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (!IsAttribute(attribute, PersistableAttributeName))
            {
                continue;
            }

            foreach (var argument in attribute.NamedArguments)
            {
                if (argument.Key == "IsRoot" && argument.Value.Value is bool value)
                {
                    return value;
                }
            }
        }

        return false;
    }

    private static bool HasAttribute(ISymbol symbol, string metadataName)
    {
        return symbol.GetAttributes().Any(attribute => IsAttribute(attribute, metadataName));
    }

    private static bool HasAttribute(ITypeSymbol symbol, string metadataName)
    {
        return symbol.GetAttributes().Any(attribute => IsAttribute(attribute, metadataName));
    }

    private static bool IsAttribute(AttributeData attribute, string metadataName)
    {
        return attribute.AttributeClass?.ToDisplayString() == metadataName;
    }

    private static bool IsPartial(INamedTypeSymbol symbol)
    {
        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax() is TypeDeclarationSyntax declaration &&
                declaration.Modifiers.Any(static modifier => modifier.Text == "partial"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasParameterlessConstructor(INamedTypeSymbol symbol)
    {
        return symbol.InstanceConstructors.Any(static constructor => constructor.Parameters.Length == 0);
    }

    private static bool TryGetKeyedStagingArguments(ITypeSymbol type, out INamedTypeSymbol entity, out ITypeSymbol key)
    {
        if (type is INamedTypeSymbol named &&
            named.OriginalDefinition.ToDisplayString() == KeyedStagingMetadataName.Replace('`', '<').Replace("2", "TEntity, TKey>"))
        {
            entity = (INamedTypeSymbol)named.TypeArguments[0];
            key = named.TypeArguments[1];
            return true;
        }

        if (type is INamedTypeSymbol namedType &&
            namedType.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::Lilja.Persistence.KeyedStaging<TEntity, TKey>")
        {
            entity = (INamedTypeSymbol)namedType.TypeArguments[0];
            key = namedType.TypeArguments[1];
            return true;
        }

        entity = default!;
        key = default!;
        return false;
    }

    private static bool TryGetListPersistableElement(ITypeSymbol type, out INamedTypeSymbol element)
    {
        if (type is INamedTypeSymbol { IsGenericType: true } named &&
            named.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Collections.Generic.List<T>" &&
            named.TypeArguments[0] is INamedTypeSymbol typeArgument &&
            HasAttribute(typeArgument, PersistableAttributeName))
        {
            element = typeArgument;
            return true;
        }

        element = default!;
        return false;
    }

    private static bool TryGetKeyTypeName(INamedTypeSymbol symbol, out string keyTypeName)
    {
        var keys = symbol.GetMembers()
            .Where(member => (member is IFieldSymbol || member is IPropertySymbol) && HasAttribute(member, KeyAttributeName))
            .Select(GetMemberType)
            .ToArray();

        if (keys.Length == 1)
        {
            keyTypeName = GetTypeName(keys[0]);
            return true;
        }

        if (keys.Length > 1)
        {
            keyTypeName = "(" + string.Join(", ", keys.Select(GetTypeName)) + ")";
            return true;
        }

        keyTypeName = string.Empty;
        return false;
    }

    private static bool TryGetValueObject(
        ITypeSymbol type,
        out ITypeSymbol primitiveType,
        out string toPrimitiveName,
        out string fromPrimitiveName,
        out FromPrimitiveKind fromPrimitiveKind)
    {
        var named = type as INamedTypeSymbol;
        var toPrimitive = named?.GetMembers()
            .OfType<IMethodSymbol>()
            .FirstOrDefault(method => method.Parameters.Length == 0 && method.ReturnsVoid == false && HasAttribute(method, ToPrimitiveAttributeName));

        if (named is null || toPrimitive is null)
        {
            primitiveType = default!;
            toPrimitiveName = string.Empty;
            fromPrimitiveName = string.Empty;
            fromPrimitiveKind = FromPrimitiveKind.StaticFactory;
            return false;
        }

        primitiveType = toPrimitive.ReturnType;
        var expectedPrimitiveType = primitiveType;
        toPrimitiveName = toPrimitive.Name;

        var factory = named.GetMembers()
            .OfType<IMethodSymbol>()
            .FirstOrDefault(method => method.IsStatic &&
                                      SymbolEqualityComparer.Default.Equals(method.ReturnType, type) &&
                                      method.Parameters.Length == 1 &&
                                      SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, expectedPrimitiveType) &&
                                      HasAttribute(method, FromPrimitiveAttributeName));

        if (factory is not null)
        {
            fromPrimitiveName = factory.Name;
            fromPrimitiveKind = FromPrimitiveKind.StaticFactory;
            return true;
        }

        var constructor = named.InstanceConstructors.FirstOrDefault(ctor =>
            ctor.Parameters.Length == 1 &&
            SymbolEqualityComparer.Default.Equals(ctor.Parameters[0].Type, expectedPrimitiveType) &&
            HasAttribute(ctor, FromPrimitiveAttributeName));

        if (constructor is not null)
        {
            fromPrimitiveName = ".ctor";
            fromPrimitiveKind = FromPrimitiveKind.Constructor;
            return true;
        }

        primitiveType = default!;
        toPrimitiveName = string.Empty;
        fromPrimitiveName = string.Empty;
        fromPrimitiveKind = FromPrimitiveKind.StaticFactory;
        return false;
    }

    private static string EscapeIdentifier(string name)
    {
        return SyntaxFacts.GetKeywordKind(name) == Microsoft.CodeAnalysis.CSharp.SyntaxKind.None ? name : "@" + name;
    }

    private static string ToParameterName(MemberModel member)
    {
        return "p" + member.Index + CreateIdentifierSuffix(member.Name);
    }

    private static string ToLocalName(MemberModel member)
    {
        return "local" + member.Index + CreateIdentifierSuffix(member.Name);
    }

    private static string CreateIdentifierSuffix(string name)
    {
        var builder = new StringBuilder(name.Length);
        var upperNext = true;
        foreach (var ch in name)
        {
            if (ch == '_' || !SyntaxFacts.IsIdentifierPartCharacter(ch))
            {
                upperNext = true;
                continue;
            }

            builder.Append(upperNext ? char.ToUpperInvariant(ch) : ch);
            upperNext = false;
        }

        return builder.Length == 0 ? "Value" : builder.ToString();
    }

    private sealed class PersistableAnalysis
    {
        public PersistableAnalysis(PersistableModel? model, ImmutableArray<Diagnostic> diagnostics)
        {
            Model = model;
            Diagnostics = diagnostics;
        }

        public PersistableModel? Model { get; }

        public ImmutableArray<Diagnostic> Diagnostics { get; }
    }

    private sealed class PersistableModel
    {
        public PersistableModel(INamedTypeSymbol symbol, bool isRoot, ImmutableArray<MemberModel> persistedMembers, ImmutableArray<MemberModel> keyMembers)
        {
            Symbol = symbol;
            IsRoot = isRoot;
            PersistedMembers = persistedMembers;
            KeyMembers = keyMembers;
            Name = symbol.Name;
            TypeName = GetTypeName(symbol);
            NamespaceName = symbol.ContainingNamespace?.IsGlobalNamespace == false ? symbol.ContainingNamespace.ToDisplayString() : string.Empty;
            StorageIdentifier = CreateStorageIdentifier(symbol);
            DtoName = Name + "Dto";
            DtoNamespace = GetDtoNamespace(symbol);
            DtoTypeName = "global::" + DtoNamespace + "." + DtoName;
            FormatterNamespace = GetFormatterNamespace(symbol);
            RepositoryNamespace = string.IsNullOrEmpty(NamespaceName) ? "Repositories" : NamespaceName + ".Repositories";
            RepositoryInterfaceName = "I" + Name + "Repository";
            RepositoryInterfaceTypeName = "global::" + RepositoryNamespace + "." + RepositoryInterfaceName;
            ConstructorMembers = persistedMembers.Where(static member => member.Kind != MemberKind.Staging).ToImmutableArray();
            StagingMembers = persistedMembers.Where(static member => member.Kind == MemberKind.Staging).ToImmutableArray();
            KeyTypeName = keyMembers.Length == 1
                ? keyMembers[0].TypeName
                : "(" + string.Join(", ", keyMembers.Select(static member => member.TypeName)) + ")";
        }

        public INamedTypeSymbol Symbol { get; }

        public string Name { get; }

        public string TypeName { get; }

        public string NamespaceName { get; }

        public string StorageIdentifier { get; }

        public string DtoName { get; }

        public string DtoNamespace { get; }

        public string DtoTypeName { get; }

        public string FormatterNamespace { get; }

        public string RepositoryNamespace { get; }

        public string RepositoryInterfaceName { get; }

        public string RepositoryInterfaceTypeName { get; }

        public bool IsRoot { get; }

        public bool IsKeyed => KeyMembers.Length > 0;

        public string KeyTypeName { get; }

        public ImmutableArray<MemberModel> PersistedMembers { get; }

        public ImmutableArray<MemberModel> ConstructorMembers { get; }

        public ImmutableArray<MemberModel> StagingMembers { get; }

        public ImmutableArray<MemberModel> KeyMembers { get; }
    }

    private sealed class MemberModel
    {
        private MemberModel(
            string name,
            string escapedName,
            string typeName,
            int index,
            bool isKey,
            MemberKind kind,
            string dtoFieldTypeName,
            ITypeSymbol? relatedPersistable,
            string toPrimitiveName,
            string fromPrimitiveName,
            FromPrimitiveKind fromPrimitiveKind)
        {
            Name = name;
            EscapedName = escapedName;
            TypeName = typeName;
            Index = index;
            IsKey = isKey;
            Kind = kind;
            DtoFieldTypeName = dtoFieldTypeName;
            RelatedPersistable = relatedPersistable;
            ToPrimitiveName = toPrimitiveName;
            FromPrimitiveName = fromPrimitiveName;
            FromPrimitiveKind = fromPrimitiveKind;
        }

        public string Name { get; }

        public string EscapedName { get; }

        public string TypeName { get; }

        public int Index { get; }

        public bool IsKey { get; }

        public MemberKind Kind { get; }

        public string DtoFieldTypeName { get; }

        public ITypeSymbol? RelatedPersistable { get; }

        public string ToPrimitiveName { get; }

        public string FromPrimitiveName { get; }

        public FromPrimitiveKind FromPrimitiveKind { get; }

        public static MemberModel Create(
            ISymbol symbol,
            ITypeSymbol type,
            int index,
            bool isKey,
            MemberKind kind,
            string dtoFieldTypeName,
            ITypeSymbol? relatedPersistable = null,
            string toPrimitiveName = "",
            string fromPrimitiveName = "",
            FromPrimitiveKind fromPrimitiveKind = FromPrimitiveKind.StaticFactory)
        {
            return new MemberModel(
                symbol.Name,
                EscapeIdentifier(symbol.Name),
                GetTypeName(type),
                index,
                isKey,
                kind,
                dtoFieldTypeName,
                relatedPersistable,
                toPrimitiveName,
                fromPrimitiveName,
                fromPrimitiveKind);
        }
    }

    private enum MemberKind
    {
        Value,
        ValueObject,
        Persistable,
        PersistableList,
        Staging,
    }

    private enum FromPrimitiveKind
    {
        Constructor,
        StaticFactory,
    }
}
