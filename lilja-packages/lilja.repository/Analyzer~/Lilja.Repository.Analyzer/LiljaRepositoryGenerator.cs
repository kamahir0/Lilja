using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Lilja.Repository.Analyzer;

[Generator]
public sealed class LiljaRepositoryGenerator : IIncrementalGenerator
{
    private const string EntityAttributeName = "Lilja.Repository.EntityAttribute";
    private const string PersistAttributeName = "Lilja.Repository.PersistAttribute";
    private const string KeyAttributeName = "Lilja.Repository.KeyAttribute";
    private const string ToPrimitiveAttributeName = "Lilja.Repository.ToPrimitiveAttribute";
    private const string FromPrimitiveAttributeName = "Lilja.Repository.FromPrimitiveAttribute";

    private const int OptionInMemory = 1;
    private const int OptionJson = 2;
    private const int OptionMsgPack = 4;

    private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.FullyQualifiedFormat;

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var analyses = context.SyntaxProvider.ForAttributeWithMetadataName(
                EntityAttributeName,
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, _) => AnalyzeEntity((INamedTypeSymbol)ctx.TargetSymbol))
            .Collect();

        var hasMessagePack = context.CompilationProvider.Select(static (compilation, _) => MessagePackContract.HasCompatibleContract(compilation));
        var input = analyses.Combine(hasMessagePack);

        context.RegisterSourceOutput(input, static (context, pair) =>
        {
            var analyses = pair.Left;
            var hasMessagePack = pair.Right;
            var models = analyses.Where(static item => item.Model is not null).Select(static item => item.Model!).ToArray();

            foreach (var analysis in analyses)
            {
                foreach (var diagnostic in analysis.Diagnostics)
                {
                    context.ReportDiagnostic(diagnostic);
                }
            }

            ReportCycles(context, models);

            if (analyses.Any(static item => item.Diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)) ||
                HasEntityCycle(models))
            {
                return;
            }

            var needsMessagePack = models.Any(static item => (item.RepositoryOptions & OptionMsgPack) != 0);

            foreach (var model in models)
            {
                AddSource(context, model, $"{model.Name}Dto.g.cs", GenerateDto(model));
                AddSource(context, model, $"{model.Name}.RepositorySupport.g.cs", GenerateEntitySupport(model));

                if (model.RepositoryOptions != 0)
                {
                    if ((model.RepositoryOptions & OptionMsgPack) != 0 && !hasMessagePack)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.MessagePackNotAvailable, model.Location.ToLocation(), model.Name));
                    }

                    AddSource(context, model, $"I{model.Name}Repository.g.cs", GenerateRepositoryInterface(model));
                    AddSource(context, model, $"{model.Name}Repository.g.cs", GenerateRepositoryFactory(model, hasMessagePack, models));
                }

                if (needsMessagePack && hasMessagePack)
                {
                    AddSource(context, model, $"{model.Name}DtoFormatter.g.cs", GenerateDtoFormatter(model));
                }
            }
        });
    }

    private static EntityAnalysis AnalyzeEntity(INamedTypeSymbol symbol)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var location = GetPrimaryLocation(symbol);

        if (!IsPartial(symbol))
        {
            diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.EntityMustBePartial, location));
        }

        if (symbol.TypeParameters.Length > 0)
        {
            diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.GenericEntityNotSupported, location));
        }

        var members = new List<MemberModel>();
        var keyMembers = new List<MemberModel>();
        var usedIndexes = new HashSet<int>();
        var nextImplicitIndex = 0;
        var declarationOrder = 0;

        foreach (var member in symbol.GetMembers())
        {
            if (member is not IFieldSymbol && member is not IPropertySymbol)
            {
                continue;
            }

            var hasPersist = TryGetPersistIndex(member, out var explicitIndex);
            var hasKey = HasAttribute(member, KeyAttributeName);
            if (!hasPersist && !hasKey)
            {
                continue;
            }

            if (member.IsStatic)
            {
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.UnsupportedMember, GetPrimaryLocation(member), member.Name, "static members are not supported"));
                continue;
            }

            if (hasKey && !hasPersist)
            {
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.KeyMustBePersisted, GetPrimaryLocation(member), member.Name));
                continue;
            }

            if (hasPersist && explicitIndex < -1)
            {
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.PersistIndexMustBeNonNegative, GetPrimaryLocation(member)));
                continue;
            }

            var index = explicitIndex;
            if (hasPersist && explicitIndex >= 0 && !usedIndexes.Add(explicitIndex))
            {
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.PersistIndexMustBeUnique, GetPrimaryLocation(member), explicitIndex));
                continue;
            }

            if (!hasPersist)
            {
                continue;
            }

            if (explicitIndex < 0)
            {
                while (usedIndexes.Contains(nextImplicitIndex))
                {
                    nextImplicitIndex++;
                }

                index = nextImplicitIndex;
                usedIndexes.Add(index);
                nextImplicitIndex++;
            }

            if (!TryCreateMemberModel(member, index, declarationOrder, hasKey, diagnostics, out var memberModel))
            {
                continue;
            }

            members.Add(memberModel);
            if (hasKey)
            {
                keyMembers.Add(memberModel);
            }

            declarationOrder++;
        }

        members.Sort(static (left, right) =>
        {
            var indexComparison = left.Index.CompareTo(right.Index);
            return indexComparison != 0 ? indexComparison : left.DeclarationOrder.CompareTo(right.DeclarationOrder);
        });

        var persistedMembers = members.ToImmutableArray();
        var keyMembersArray = keyMembers.ToImmutableArray();

        var typeByMemberName = symbol.GetMembers()
            .Where(m => m is IFieldSymbol || m is IPropertySymbol)
            .ToDictionary(m => m.Name, GetMemberType, StringComparer.Ordinal);
        
        var orderedTypes = persistedMembers
            .Select(m => typeByMemberName[m.Name])
            .ToArray();

        var hasMatchingConstructor = HasMatchingConstructor(symbol, orderedTypes);
        var repositoryOptions = GetRepositoryOptions(symbol);
        var model = diagnostics.Any(static item => item.Severity == DiagnosticSeverity.Error)
            ? null
            : new EntityModel(
                symbol.Name,
                GetTypeName(symbol),
                symbol.ContainingNamespace.IsGlobalNamespace ? string.Empty : symbol.ContainingNamespace.ToDisplayString(),
                repositoryOptions,
                persistedMembers,
                keyMembersArray,
                hasMatchingConstructor,
                GetLocationInfo(symbol)
            );

        return new EntityAnalysis(model, diagnostics.ToImmutable());
    }

    private static bool TryCreateMemberModel(ISymbol member, int index, int declarationOrder, bool isKey, ImmutableArray<Diagnostic>.Builder diagnostics, out MemberModel model)
    {
        model = default!;
        var type = GetMemberType(member);
        var kind = MemberKind.Value;
        ITypeSymbol? relatedEntity = null;
        ValueObjectShape? valueObject = null;
        var dtoTypeName = GetTypeName(type);

        if (TryGetListEntity(type, out var listEntity))
        {
            kind = MemberKind.EntityList;
            relatedEntity = listEntity;
            dtoTypeName = "global::System.Collections.Generic.List<" + GetDtoTypeName(listEntity) + ">";
        }
        else if (HasAttribute(type, EntityAttributeName))
        {
            kind = MemberKind.Entity;
            relatedEntity = type;
            dtoTypeName = GetDtoTypeName(type);
        }
        else if (TryGetValueObject(type, diagnostics, GetPrimaryLocation(member), out valueObject))
        {
            kind = MemberKind.ValueObject;
            dtoTypeName = valueObject!.PrimitiveTypeName;
        }

        if (member is IPropertySymbol property && !IsSupportedAutoProperty(property))
        {
            diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.UnsupportedMember, GetPrimaryLocation(member), member.Name, "only auto-properties are supported"));
            return false;
        }

        var relatedEntityTypeName = relatedEntity is null ? null : GetTypeName(relatedEntity);
        var relatedEntityDtoTypeName = relatedEntity is null ? null : GetDtoTypeName(relatedEntity);
        model = new MemberModel(member.Name, EscapeIdentifier(member.Name), GetTypeName(type), dtoTypeName, index, declarationOrder, isKey, kind, relatedEntityTypeName, relatedEntityDtoTypeName, valueObject);
        return true;
    }

    private static string GenerateDto(EntityModel model)
    {
        var sb = CreateSourceBuilder();
        using (AppendNamespace(sb, model.DtoNamespace))
        {
            sb.Append("[global::System.Serializable]\n");
            sb.Append("public sealed class ").Append(model.DtoName).Append("\n{\n");
            foreach (var member in model.PersistedMembers)
            {
                sb.Append("    public ").Append(member.DtoTypeName).Append(' ').Append(ToDtoFieldName(member)).Append(" = default!;\n");
            }

            sb.Append("}\n");
        }

        return sb.ToString();
    }

    private static string GenerateEntitySupport(EntityModel model)
    {
        var hasMatchingConstructor = model.HasMatchingConstructor;
        var sb = CreateSourceBuilder();
        using (AppendNamespace(sb, model.NamespaceName))
        {
            sb.Append("public partial class ").Append(model.Name).Append("\n{\n");
            if (!hasMatchingConstructor)
            {
                AppendRestoreConstructor(sb, model);
            }

            AppendToDto(sb, model);
            AppendFromDto(sb, model, hasMatchingConstructor);
            if (model.IsKeyed)
            {
                AppendKeySupport(sb, model);
            }
            else
            {
                AppendDefaultDtoFactory(sb, model);
            }

            sb.Append("}\n");
        }

        return sb.ToString();
    }

    private static void AppendRestoreConstructor(StringBuilder sb, EntityModel model)
    {
        sb.Append("    private ").Append(model.Name).Append("(global::Lilja.Repository.RestoreToken _");
        foreach (var member in model.PersistedMembers)
        {
            sb.Append(", ").Append(member.TypeName).Append(' ').Append(ToParameterName(member));
        }

        sb.Append(")\n    {\n");
        foreach (var member in model.PersistedMembers)
        {
            sb.Append("        ").Append(member.AccessName).Append(" = ").Append(ToParameterName(member)).Append(";\n");
        }

        sb.Append("    }\n\n");
    }

    private static void AppendToDto(StringBuilder sb, EntityModel model)
    {
        sb.Append("    public ").Append(model.DtoTypeName).Append(" ToDto()\n    {\n");
        sb.Append("        var dto = new ").Append(model.DtoTypeName).Append("();\n");
        foreach (var member in model.PersistedMembers)
        {
            AppendToDtoAssignment(sb, member);
        }

        sb.Append("        return dto;\n");
        sb.Append("    }\n\n");
    }

    private static void AppendToDtoAssignment(StringBuilder sb, MemberModel member)
    {
        var fieldName = ToDtoFieldName(member);
        switch (member.Kind)
        {
            case MemberKind.Entity:
                sb.Append("        dto.").Append(fieldName).Append(" = ").Append(member.AccessName).Append(" is null ? null! : ").Append(member.AccessName).Append(".ToDto();\n");
                return;
            case MemberKind.EntityList:
                sb.Append("        dto.").Append(fieldName).Append(" = new global::System.Collections.Generic.List<").Append(member.RelatedEntityDtoTypeName!).Append(">();\n");
                sb.Append("        if (").Append(member.AccessName).Append(" is not null)\n        {\n");
                sb.Append("            foreach (var item in ").Append(member.AccessName).Append(")\n            {\n");
                sb.Append("                dto.").Append(fieldName).Append(".Add(item.ToDto());\n");
                sb.Append("            }\n        }\n");
                return;
            case MemberKind.ValueObject:
                sb.Append("        dto.").Append(fieldName).Append(" = ").Append(member.AccessName).Append('.').Append(member.ValueObject!.ToPrimitiveName).Append("();\n");
                return;
            default:
                sb.Append("        dto.").Append(fieldName).Append(" = ").Append(member.AccessName).Append(";\n");
                return;
        }
    }

    private static void AppendFromDto(StringBuilder sb, EntityModel model, bool hasMatchingConstructor)
    {
        sb.Append("    public static ").Append(model.TypeName).Append(" FromDto(").Append(model.DtoTypeName).Append(" dto)\n    {\n");
        sb.Append("        if (dto is null)\n        {\n            throw new global::System.ArgumentNullException(nameof(dto));\n        }\n\n");
        foreach (var member in model.PersistedMembers)
        {
            AppendFromDtoLocal(sb, member);
        }

        sb.Append("        return new ").Append(model.TypeName).Append('(');
        if (!hasMatchingConstructor)
        {
            sb.Append("default(global::Lilja.Repository.RestoreToken)");
            if (model.PersistedMembers.Length > 0)
            {
                sb.Append(", ");
            }
        }

        for (var i = 0; i < model.PersistedMembers.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            sb.Append(ToLocalName(model.PersistedMembers[i]));
        }

        sb.Append(");\n");
        sb.Append("    }\n\n");
    }

    private static void AppendFromDtoLocal(StringBuilder sb, MemberModel member)
    {
        var localName = ToLocalName(member);
        var fieldName = ToDtoFieldName(member);
        switch (member.Kind)
        {
            case MemberKind.Entity:
                sb.Append("        var ").Append(localName).Append(" = dto.").Append(fieldName).Append(" is null ? null! : ").Append(member.RelatedEntityTypeName!).Append(".FromDto(dto.").Append(fieldName).Append(");\n");
                return;
            case MemberKind.EntityList:
                sb.Append("        var ").Append(localName).Append(" = new ").Append(member.TypeName).Append("();\n");
                sb.Append("        if (dto.").Append(fieldName).Append(" is not null)\n        {\n");
                sb.Append("            foreach (var item in dto.").Append(fieldName).Append(")\n            {\n");
                sb.Append("                ").Append(localName).Append(".Add(").Append(member.RelatedEntityTypeName!).Append(".FromDto(item));\n");
                sb.Append("            }\n        }\n");
                return;
            case MemberKind.ValueObject:
                if (member.ValueObject!.FromPrimitiveKind == FromPrimitiveKind.Constructor)
                {
                    sb.Append("        var ").Append(localName).Append(" = new ").Append(member.TypeName).Append("(dto.").Append(fieldName).Append(");\n");
                }
                else
                {
                    sb.Append("        var ").Append(localName).Append(" = ").Append(member.TypeName).Append('.').Append(member.ValueObject.FromPrimitiveName).Append("(dto.").Append(fieldName).Append(");\n");
                }
                return;
            default:
                sb.Append("        var ").Append(localName).Append(" = dto.").Append(fieldName).Append(";\n");
                return;
        }
    }

    private static void AppendKeySupport(StringBuilder sb, EntityModel model)
    {
        sb.Append("    internal static ").Append(model.KeyTypeName).Append(" __RepositoryGetKey(").Append(model.TypeName).Append(" entity)\n    {\n");
        sb.Append("        return ");
        AppendKeyExpression(sb, model.KeyMembers, member => "entity." + member.AccessName, false);
        sb.Append(";\n    }\n\n");

        sb.Append("    internal static ").Append(model.KeyTypeName).Append(" __RepositoryGetKeyFromDto(").Append(model.DtoTypeName).Append(" dto)\n    {\n");
        sb.Append("        return ");
        AppendKeyExpression(sb, model.KeyMembers, member => ConvertFromDtoExpression(member, "dto." + ToDtoFieldName(member)), true);
        sb.Append(";\n    }\n\n");

        sb.Append("    internal static ").Append(model.DtoTypeName).Append(" __RepositoryCreateDefaultDto(").Append(model.KeyTypeName).Append(" key)\n    {\n");
        sb.Append("        var dto = new ").Append(model.DtoTypeName).Append("();\n");
        for (var i = 0; i < model.KeyMembers.Length; i++)
        {
            var member = model.KeyMembers[i];
            var keyExpr = model.KeyMembers.Length == 1 ? "key" : "key.Item" + (i + 1);
            if (member.Kind == MemberKind.ValueObject)
            {
                keyExpr += "." + member.ValueObject!.ToPrimitiveName + "()";
            }

            sb.Append("        dto.").Append(ToDtoFieldName(member)).Append(" = ").Append(keyExpr).Append(";\n");
        }

        sb.Append("        return dto;\n");
        sb.Append("    }\n\n");
    }

    private static void AppendDefaultDtoFactory(StringBuilder sb, EntityModel model)
    {
        sb.Append("    internal static ").Append(model.DtoTypeName).Append(" __RepositoryCreateDefaultDto()\n    {\n");
        sb.Append("        return new ").Append(model.DtoTypeName).Append("();\n");
        sb.Append("    }\n\n");
    }

    private static string GenerateRepositoryInterface(EntityModel model)
    {
        var sb = CreateSourceBuilder();
        using (AppendNamespace(sb, model.RepositoryNamespace))
        {
            sb.Append("public interface ").Append(model.RepositoryInterfaceName).Append("\n{\n");
            if (model.IsKeyed)
            {
                sb.Append("    global::Cysharp.Threading.Tasks.UniTask LoadAsync(global::System.Threading.CancellationToken ct = default);\n\n");
                sb.Append("    global::Cysharp.Threading.Tasks.UniTask SaveAsync(global::System.Threading.CancellationToken ct = default);\n\n");
                sb.Append("    ").Append(model.TypeName).Append(" Get(").Append(model.KeyTypeName).Append(" key);\n\n");
                sb.Append("    bool TryGet(").Append(model.KeyTypeName).Append(" key, out ").Append(model.TypeName).Append(" entity);\n\n");
                sb.Append("    global::System.Collections.Generic.IReadOnlyList<").Append(model.TypeName).Append("> All();\n\n");
                sb.Append("    void Update(").Append(model.TypeName).Append(" entity);\n\n");
                sb.Append("    bool Delete(").Append(model.KeyTypeName).Append(" key);\n\n");
                sb.Append("    bool Exists(").Append(model.KeyTypeName).Append(" key);\n\n");
                sb.Append("    void Clear();\n");
            }
            else
            {
                sb.Append("    global::Cysharp.Threading.Tasks.UniTask LoadAsync(global::System.Threading.CancellationToken ct = default);\n\n");
                sb.Append("    global::Cysharp.Threading.Tasks.UniTask SaveAsync(global::System.Threading.CancellationToken ct = default);\n\n");
                sb.Append("    ").Append(model.TypeName).Append(" Get();\n\n");
                sb.Append("    bool TryGet(out ").Append(model.TypeName).Append(" entity);\n\n");
                sb.Append("    void Update(").Append(model.TypeName).Append(" entity);\n\n");
                sb.Append("    bool Delete();\n\n");
                sb.Append("    bool Exists();\n\n");
                sb.Append("    void Clear();\n");
            }

            sb.Append("}\n");
        }

        return sb.ToString();
    }

    private static string GenerateRepositoryFactory(EntityModel model, bool hasMessagePack, IReadOnlyList<EntityModel> allModels)
    {
        var sb = CreateSourceBuilder();
        using (AppendNamespace(sb, model.RepositoryNamespace))
        {
            sb.Append("public static class ").Append(model.Name).Append("Repository\n{\n");
            if ((model.RepositoryOptions & OptionInMemory) != 0)
            {
                AppendInMemoryFactory(sb, model);
            }

            if ((model.RepositoryOptions & OptionJson) != 0)
            {
                AppendJsonFactory(sb, model);
            }

            if ((model.RepositoryOptions & OptionMsgPack) != 0 && hasMessagePack)
            {
                AppendMessagePackFactory(sb, model, allModels);
            }

            sb.Append("}\n");
        }

        return sb.ToString();
    }

    private static void AppendInMemoryFactory(StringBuilder sb, EntityModel model)
    {
        sb.Append("    public static class InMemory\n    {\n");
        if (model.IsKeyed)
        {
            sb.Append("        public static ").Append(model.RepositoryInterfaceName).Append(" Create()\n        {\n            return new Impl(null);\n        }\n\n");
            sb.Append("        public static ").Append(model.RepositoryInterfaceName).Append(" Create(global::System.Collections.Generic.IReadOnlyList<").Append(model.TypeName).Append(">? initialValues)\n        {\n            return new Impl(initialValues);\n        }\n\n");
            sb.Append("        private sealed class Impl : global::Lilja.Repository.InMemoryKeyedRepository<").Append(model.KeyTypeName).Append(", ").Append(model.TypeName).Append(", ").Append(model.DtoTypeName).Append(">, ").Append(model.RepositoryInterfaceName).Append("\n        {\n");
            sb.Append("            public Impl(global::System.Collections.Generic.IReadOnlyList<").Append(model.TypeName).Append(">? initialValues)\n");
            sb.Append("                : base(entity => entity.ToDto(), dto => ").Append(model.TypeName).Append(".FromDto(dto), ").Append(model.TypeName).Append(".__RepositoryGetKey, ").Append(model.TypeName).Append(".__RepositoryGetKeyFromDto, ").Append(model.TypeName).Append(".__RepositoryCreateDefaultDto, initialValues)\n            {\n            }\n        }\n");
        }
        else
        {
            sb.Append("        public static ").Append(model.RepositoryInterfaceName).Append(" Create()\n        {\n            return new Impl(null);\n        }\n\n");
            sb.Append("        public static ").Append(model.RepositoryInterfaceName).Append(" Create(").Append(model.TypeName).Append("? initialValue)\n        {\n            return new Impl(initialValue);\n        }\n\n");
            sb.Append("        private sealed class Impl : global::Lilja.Repository.InMemoryRepository<").Append(model.TypeName).Append(", ").Append(model.DtoTypeName).Append(">, ").Append(model.RepositoryInterfaceName).Append("\n        {\n");
            sb.Append("            public Impl(").Append(model.TypeName).Append("? initialValue)\n");
            sb.Append("                : base(entity => entity.ToDto(), dto => ").Append(model.TypeName).Append(".FromDto(dto), ").Append(model.TypeName).Append(".__RepositoryCreateDefaultDto, initialValue)\n            {\n            }\n        }\n");
        }

        sb.Append("    }\n\n");
    }

    private static void AppendJsonFactory(StringBuilder sb, EntityModel model)
    {
        sb.Append("    public static class Json\n    {\n");
        sb.Append("        public static ").Append(model.RepositoryInterfaceName).Append(" Create()\n        {\n            return new Impl();\n        }\n\n");
        if (model.IsKeyed)
        {
            sb.Append("        private sealed class Impl : global::Lilja.Repository.JsonKeyedRepository<").Append(model.KeyTypeName).Append(", ").Append(model.TypeName).Append(", ").Append(model.DtoTypeName).Append(">, ").Append(model.RepositoryInterfaceName).Append("\n        {\n");
            sb.Append("            public Impl()\n                : base(global::System.IO.Path.Combine(global::UnityEngine.Application.persistentDataPath, \"").Append(model.StorageIdentifier).Append("\"), entity => entity.ToDto(), dto => ").Append(model.TypeName).Append(".FromDto(dto), ").Append(model.TypeName).Append(".__RepositoryGetKey, ").Append(model.TypeName).Append(".__RepositoryGetKeyFromDto, ").Append(model.TypeName).Append(".__RepositoryCreateDefaultDto)\n            {\n            }\n        }\n");
        }
        else
        {
            sb.Append("        private sealed class Impl : global::Lilja.Repository.JsonRepository<").Append(model.TypeName).Append(", ").Append(model.DtoTypeName).Append(">, ").Append(model.RepositoryInterfaceName).Append("\n        {\n");
            sb.Append("            public Impl()\n                : base(global::System.IO.Path.Combine(global::UnityEngine.Application.persistentDataPath, \"").Append(model.StorageIdentifier).Append(".json\"), entity => entity.ToDto(), dto => ").Append(model.TypeName).Append(".FromDto(dto), ").Append(model.TypeName).Append(".__RepositoryCreateDefaultDto)\n            {\n            }\n        }\n");
        }

        sb.Append("    }\n\n");
    }

    private static void AppendMessagePackFactory(StringBuilder sb, EntityModel model, IReadOnlyList<EntityModel> allModels)
    {
        sb.Append("    public static class MessagePack\n    {\n");
        sb.Append("        public static ").Append(model.RepositoryInterfaceName).Append(" Create()\n        {\n            return new Impl();\n        }\n\n");
        sb.Append("        private sealed class Impl : ").Append(model.RepositoryInterfaceName).Append("\n        {\n");
        sb.Append("            private readonly global::MessagePack.MessagePackSerializerOptions _options;\n\n");
        sb.Append("#if UNITY_EDITOR\n");
        sb.Append("            private readonly global::Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryState _repositoryState;\n");
        sb.Append("#endif\n\n");
        if (model.IsKeyed)
        {
            sb.Append("            private readonly global::System.Collections.Generic.Dictionary<").Append(model.KeyTypeName).Append(", ").Append(model.DtoTypeName).Append("> _values = new global::System.Collections.Generic.Dictionary<").Append(model.KeyTypeName).Append(", ").Append(model.DtoTypeName).Append(">();\n\n");
        }
        else
        {
            sb.Append("            private bool _hasValue;\n");
            sb.Append("            private ").Append(model.DtoTypeName).Append("? _value;\n\n");
        }

        sb.Append("            public Impl()\n            {\n");
        sb.Append("                var resolver = global::MessagePack.Resolvers.CompositeResolver.Create(\n");
        sb.Append("                    new global::MessagePack.Formatters.IMessagePackFormatter[]\n                    {\n");
        foreach (var formatterModel in allModels)
        {
            sb.Append("                        new global::").Append(formatterModel.FormatterNamespace).Append('.').Append(formatterModel.Name).Append("DtoFormatter(),\n");
        }

        sb.Append("                    },\n");
        sb.Append("                    new global::MessagePack.IFormatterResolver[] { global::MessagePack.Resolvers.StandardResolver.Instance });\n");
        sb.Append("                _options = global::MessagePack.MessagePackSerializerOptions.Standard.WithResolver(resolver);\n");
        sb.Append("#if UNITY_EDITOR\n");
        sb.Append("                _repositoryState = global::Lilja.Repository.Diagnostics.RepositoryTracker.Track(this, global::Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType.MessagePack, \"").Append(model.StorageIdentifier).Append("\", \"").Append(model.Name).Append("Repository\", ").Append(model.IsKeyed ? "true" : "false").Append(");\n");
        sb.Append("#endif\n");
        sb.Append("            }\n\n");

        if (model.IsKeyed)
        {
            AppendMessagePackKeyedMethods(sb, model);
        }
        else
        {
            AppendMessagePackSingleMethods(sb, model);
        }

        sb.Append("        }\n");
        sb.Append("    }\n\n");
    }

    private static void AppendMessagePackSingleMethods(StringBuilder sb, EntityModel model)
    {
        sb.Append("            public async global::Cysharp.Threading.Tasks.UniTask LoadAsync(global::System.Threading.CancellationToken ct = default)\n            {\n");
        sb.Append("                var path = global::System.IO.Path.Combine(global::UnityEngine.Application.persistentDataPath, \"").Append(model.StorageIdentifier).Append(".msgpack\");\n");
        sb.Append("                var dto = await global::Cysharp.Threading.Tasks.UniTask.RunOnThreadPool(() =>\n                {\n");
        sb.Append("                    ct.ThrowIfCancellationRequested();\n");
        sb.Append("                    if (!global::System.IO.File.Exists(path))\n                    {\n                        return null;\n                    }\n");
        sb.Append("                    return global::MessagePack.MessagePackSerializer.Deserialize<").Append(model.DtoTypeName).Append(">(global::System.IO.File.ReadAllBytes(path), _options);\n");
        sb.Append("                }, cancellationToken: ct);\n\n");
        sb.Append("                _hasValue = dto is not null;\n");
        sb.Append("                _value = dto;\n");
        sb.Append("#if UNITY_EDITOR\n");
        sb.Append("                if (_hasValue) _repositoryState.SetValue(_value); else _repositoryState.Clear();\n");
        sb.Append("#endif\n");
        sb.Append("            }\n\n");
        sb.Append("            public global::Cysharp.Threading.Tasks.UniTask SaveAsync(global::System.Threading.CancellationToken ct = default)\n            {\n");
        sb.Append("                var path = global::System.IO.Path.Combine(global::UnityEngine.Application.persistentDataPath, \"").Append(model.StorageIdentifier).Append(".msgpack\");\n");
        sb.Append("                var hasValue = _hasValue;\n");
        sb.Append("                var value = _value;\n");
        sb.Append("                return global::Cysharp.Threading.Tasks.UniTask.RunOnThreadPool(() =>\n                {\n");
        sb.Append("                    ct.ThrowIfCancellationRequested();\n");
        sb.Append("                    if (!hasValue || value is null)\n                    {\n                        global::Lilja.Repository.AtomicFileWriter.DeleteIfExists(path);\n                        return;\n                    }\n");
        sb.Append("                    global::Lilja.Repository.AtomicFileWriter.WriteAllBytes(path, global::MessagePack.MessagePackSerializer.Serialize(value, _options));\n");
        sb.Append("                }, cancellationToken: ct);\n            }\n\n");
        sb.Append("            public ").Append(model.TypeName).Append(" Get()\n            {\n                if (!_hasValue || _value is null) throw new global::System.InvalidOperationException(\"Repository has no value.\");\n                return ").Append(model.TypeName).Append(".FromDto(_value);\n            }\n\n");
        sb.Append("            public bool TryGet(out ").Append(model.TypeName).Append(" entity)\n            {\n                if (!_hasValue || _value is null)\n                {\n                    entity = null!;\n                    return false;\n                }\n\n                entity = ").Append(model.TypeName).Append(".FromDto(_value);\n                return true;\n            }\n\n");
        sb.Append("            public void Update(").Append(model.TypeName).Append(" entity)\n            {\n                if (entity is null) throw new global::System.ArgumentNullException(nameof(entity));\n                _value = entity.ToDto();\n                _hasValue = true;\n");
        sb.Append("#if UNITY_EDITOR\n");
        sb.Append("                _repositoryState.SetValue(_value);\n");
        sb.Append("#endif\n");
        sb.Append("            }\n\n");
        sb.Append("            public bool Delete()\n            {\n                var existed = _hasValue;\n                _hasValue = false;\n                _value = null;\n");
        sb.Append("#if UNITY_EDITOR\n");
        sb.Append("                _repositoryState.Clear();\n");
        sb.Append("#endif\n");
        sb.Append("                return existed;\n            }\n\n");
        sb.Append("            public bool Exists()\n            {\n");
        sb.Append("                return _hasValue;\n            }\n\n");
        sb.Append("            public void Clear()\n            {\n                _hasValue = false;\n                _value = null;\n");
        sb.Append("#if UNITY_EDITOR\n");
        sb.Append("                _repositoryState.Clear();\n");
        sb.Append("#endif\n");
        sb.Append("            }\n");
    }

    private static void AppendMessagePackKeyedMethods(StringBuilder sb, EntityModel model)
    {
        sb.Append("            public async global::Cysharp.Threading.Tasks.UniTask LoadAsync(global::System.Threading.CancellationToken ct = default)\n            {\n");
        sb.Append("                var directory = GetDirectoryPath();\n");
        sb.Append("                var tempValues = await global::Cysharp.Threading.Tasks.UniTask.RunOnThreadPool(() =>\n                {\n");
        sb.Append("                    ct.ThrowIfCancellationRequested();\n");
        sb.Append("                    var tempDict = new global::System.Collections.Generic.Dictionary<").Append(model.KeyTypeName).Append(", ").Append(model.DtoTypeName).Append(">();\n");
        sb.Append("                    if (!global::System.IO.Directory.Exists(directory)) return tempDict;\n");
        sb.Append("                    var files = global::System.IO.Directory.GetFiles(directory, \"*.msgpack\");\n");
        sb.Append("                    foreach (var file in files)\n                    {\n");
        sb.Append("                        ct.ThrowIfCancellationRequested();\n");
        sb.Append("                        var dto = global::MessagePack.MessagePackSerializer.Deserialize<").Append(model.DtoTypeName).Append(">(global::System.IO.File.ReadAllBytes(file), _options);\n");
        sb.Append("                        if (dto is not null)\n                        {\n                            var key = ").Append(model.TypeName).Append(".__RepositoryGetKeyFromDto(dto);\n                            tempDict[key] = dto;\n");
        sb.Append("                        }\n");
        sb.Append("                    }\n");
        sb.Append("                    return tempDict;\n");
        sb.Append("                }, cancellationToken: ct);\n\n");
        sb.Append("                _values.Clear();\n");
        sb.Append("#if UNITY_EDITOR\n");
        sb.Append("                _repositoryState.Clear();\n");
        sb.Append("#endif\n");
        sb.Append("                foreach (var kvp in tempValues)\n                {\n");
        sb.Append("                    _values[kvp.Key] = kvp.Value;\n");
        sb.Append("#if UNITY_EDITOR\n");
        sb.Append("                    _repositoryState.SetRecord(kvp.Key, kvp.Value);\n");
        sb.Append("#endif\n");
        sb.Append("                }\n");
        sb.Append("            }\n\n");
        sb.Append("            public global::Cysharp.Threading.Tasks.UniTask SaveAsync(global::System.Threading.CancellationToken ct = default)\n            {\n                var directory = GetDirectoryPath();\n                var snapshot = new global::System.Collections.Generic.Dictionary<").Append(model.KeyTypeName).Append(", ").Append(model.DtoTypeName).Append(">(_values);\n");
        sb.Append("                return global::Cysharp.Threading.Tasks.UniTask.RunOnThreadPool(() =>\n                {\n");
        sb.Append("                    ct.ThrowIfCancellationRequested();\n");
        sb.Append("                    global::System.IO.Directory.CreateDirectory(directory);\n");
        sb.Append("                    var expectedFiles = new global::System.Collections.Generic.HashSet<string>();\n");
        sb.Append("                    foreach (var item in snapshot)\n                    {\n                        var path = GetFilePath(directory, item.Key);\n                        expectedFiles.Add(global::System.IO.Path.GetFullPath(path));\n                        global::Lilja.Repository.AtomicFileWriter.WriteAllBytes(path, global::MessagePack.MessagePackSerializer.Serialize(item.Value, _options));\n                    }\n");
        sb.Append("                    foreach (var file in global::System.IO.Directory.GetFiles(directory, \"*.msgpack\"))\n                    {\n                        if (!expectedFiles.Contains(global::System.IO.Path.GetFullPath(file)))\n                        {\n                            global::Lilja.Repository.AtomicFileWriter.DeleteIfExists(file);\n                        }\n                    }\n");
        sb.Append("                }, cancellationToken: ct);\n            }\n\n");
        sb.Append("            public ").Append(model.TypeName).Append(" Get(").Append(model.KeyTypeName).Append(" key)\n            {\n                if (!_values.TryGetValue(key, out var dto)) throw new global::System.Collections.Generic.KeyNotFoundException($\"Repository record was not found. Key: {key}\");\n                return ").Append(model.TypeName).Append(".FromDto(dto);\n            }\n\n");
        sb.Append("            public bool TryGet(").Append(model.KeyTypeName).Append(" key, out ").Append(model.TypeName).Append(" entity)\n            {\n                if (!_values.TryGetValue(key, out var dto))\n                {\n                    entity = null!;\n                    return false;\n                }\n\n                entity = ").Append(model.TypeName).Append(".FromDto(dto);\n                return true;\n            }\n\n");
        sb.Append("            public global::System.Collections.Generic.IReadOnlyList<").Append(model.TypeName).Append("> All()\n            {\n                var values = new global::System.Collections.Generic.List<").Append(model.TypeName).Append(">(_values.Count);\n                foreach (var dto in _values.Values)\n                {\n                    values.Add(").Append(model.TypeName).Append(".FromDto(dto));\n                }\n\n                return values;\n            }\n\n");
        sb.Append("            public void Update(").Append(model.TypeName).Append(" entity)\n            {\n                if (entity is null) throw new global::System.ArgumentNullException(nameof(entity));\n                var key = ").Append(model.TypeName).Append(".__RepositoryGetKey(entity);\n                var dto = entity.ToDto();\n                _values[key] = dto;\n");
        sb.Append("#if UNITY_EDITOR\n");
        sb.Append("                _repositoryState.SetRecord(key, dto);\n");
        sb.Append("#endif\n");
        sb.Append("            }\n\n");
        sb.Append("            public bool Delete(").Append(model.KeyTypeName).Append(" key)\n            {\n                var removed = _values.Remove(key);\n");
        sb.Append("#if UNITY_EDITOR\n");
        sb.Append("                if (removed) _repositoryState.RemoveRecord(key);\n");
        sb.Append("#endif\n");
        sb.Append("                return removed;\n            }\n\n");
        sb.Append("            public bool Exists(").Append(model.KeyTypeName).Append(" key)\n            {\n                return _values.ContainsKey(key);\n            }\n\n");
        sb.Append("            public void Clear()\n            {\n                _values.Clear();\n");
        sb.Append("#if UNITY_EDITOR\n");
        sb.Append("                _repositoryState.Clear();\n");
        sb.Append("#endif\n");
        sb.Append("            }\n\n");
        sb.Append("            private static string GetDirectoryPath()\n            {\n                return global::System.IO.Path.Combine(global::UnityEngine.Application.persistentDataPath, \"").Append(model.StorageIdentifier).Append("\");\n            }\n\n");
        sb.Append("            private static string GetFilePath(").Append(model.KeyTypeName).Append(" key)\n            {\n                return global::System.IO.Path.Combine(GetDirectoryPath(), global::Lilja.Repository.RepositoryFileName.Encode(key) + \".msgpack\");\n            }\n");
        sb.Append("\n            private static string GetFilePath(string directory, ").Append(model.KeyTypeName).Append(" key)\n            {\n                return global::System.IO.Path.Combine(directory, global::Lilja.Repository.RepositoryFileName.Encode(key) + \".msgpack\");\n            }\n");
    }

    private static string GenerateDtoFormatter(EntityModel model)
    {
        var sb = CreateSourceBuilder();
        using (AppendNamespace(sb, model.FormatterNamespace))
        {
            sb.Append("public sealed class ").Append(model.Name).Append("DtoFormatter : global::MessagePack.Formatters.IMessagePackFormatter<").Append(model.DtoTypeName).Append(">\n{\n");
            sb.Append("    private static global::MessagePack.Formatters.IMessagePackFormatter<T> ResolveFormatter<T>(global::MessagePack.MessagePackSerializerOptions options)\n    {\n");
            sb.Append("        return options.Resolver.GetFormatter<T>() ?? throw new global::MessagePack.MessagePackSerializationException($\"Formatter not found for {typeof(T).FullName}.\");\n    }\n\n");
            sb.Append("    public void Serialize(ref global::MessagePack.MessagePackWriter writer, ").Append(model.DtoTypeName).Append(" value, global::MessagePack.MessagePackSerializerOptions options)\n    {\n");
            sb.Append("        if (value is null)\n        {\n            writer.WriteNil();\n            return;\n        }\n");
            sb.Append("        writer.WriteMapHeader(").Append(model.PersistedMembers.Length).Append(");\n");
            foreach (var member in model.PersistedMembers)
            {
                sb.Append("        writer.Write(\"").Append(ToDtoFieldName(member)).Append("\");\n");
                sb.Append("        ResolveFormatter<").Append(member.DtoTypeName).Append(">(options).Serialize(ref writer, value.").Append(ToDtoFieldName(member)).Append(", options);\n");
            }

            sb.Append("    }\n\n");
            sb.Append("    public ").Append(model.DtoTypeName).Append(" Deserialize(ref global::MessagePack.MessagePackReader reader, global::MessagePack.MessagePackSerializerOptions options)\n    {\n");
            sb.Append("        if (reader.TryReadNil()) return null!;\n");
            sb.Append("        var value = new ").Append(model.DtoTypeName).Append("();\n");
            sb.Append("        var length = reader.ReadMapHeader();\n");
            sb.Append("        for (var i = 0; i < length; i++)\n        {\n");
            sb.Append("            var key = reader.ReadString();\n");
            sb.Append("            switch (key)\n            {\n");
            foreach (var member in model.PersistedMembers)
            {
                sb.Append("                case \"").Append(ToDtoFieldName(member)).Append("\":\n");
                sb.Append("                    value.").Append(ToDtoFieldName(member)).Append(" = ResolveFormatter<").Append(member.DtoTypeName).Append(">(options).Deserialize(ref reader, options);\n");
                sb.Append("                    break;\n");
            }

            sb.Append("                default:\n");
            sb.Append("                    reader.Skip();\n");
            sb.Append("                    break;\n");
            sb.Append("            }\n        }\n");
            sb.Append("        return value;\n    }\n");
            sb.Append("}\n");
        }

        return sb.ToString();
    }

    private static bool TryGetValueObject(ITypeSymbol type, ImmutableArray<Diagnostic>.Builder diagnostics, Location location, out ValueObjectShape? shape)
    {
        shape = null;
        var methods = type.GetMembers().OfType<IMethodSymbol>().ToArray();
        var toPrimitive = methods.Where(static method => method.MethodKind != MethodKind.Constructor && HasAttribute(method, ToPrimitiveAttributeName)).ToArray();
        var fromMethods = methods.Where(static method => method.MethodKind != MethodKind.Constructor && HasAttribute(method, FromPrimitiveAttributeName)).ToArray();
        var fromConstructors = type.GetMembers().OfType<IMethodSymbol>().Where(static method => method.MethodKind == MethodKind.Constructor && HasAttribute(method, FromPrimitiveAttributeName)).ToArray();
        if (toPrimitive.Length == 0 && fromMethods.Length == 0 && fromConstructors.Length == 0)
        {
            return false;
        }

        if (toPrimitive.Length != 1 || toPrimitive[0].IsStatic || toPrimitive[0].Parameters.Length != 0 || toPrimitive[0].ReturnsVoid)
        {
            diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.InvalidToPrimitiveDefinition, location, type.Name));
            return false;
        }

        var primitiveType = toPrimitive[0].ReturnType;
        if (fromMethods.Length + fromConstructors.Length != 1)
        {
            diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.InvalidFromPrimitiveDefinition, location, type.Name));
            return false;
        }

        if (fromMethods.Length == 1)
        {
            var method = fromMethods[0];
            if (!method.IsStatic || method.Parameters.Length != 1 || !SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, primitiveType) || !SymbolEqualityComparer.Default.Equals(method.ReturnType, type))
            {
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.InvalidFromPrimitiveDefinition, location, type.Name));
                return false;
            }

            shape = new ValueObjectShape(toPrimitive[0].Name, GetTypeName(primitiveType), method.Name, FromPrimitiveKind.StaticFactory);
            return true;
        }

        var constructor = fromConstructors[0];
        if (constructor.Parameters.Length != 1 || !SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type, primitiveType))
        {
            diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.InvalidFromPrimitiveDefinition, location, type.Name));
            return false;
        }

        shape = new ValueObjectShape(toPrimitive[0].Name, GetTypeName(primitiveType), string.Empty, FromPrimitiveKind.Constructor);
        return true;
    }

    private static void ReportCycles(SourceProductionContext context, IReadOnlyList<EntityModel> models)
    {
        var byTypeName = models.ToDictionary(static item => item.TypeName, StringComparer.Ordinal);
        foreach (var model in models)
        {
            if (HasCycle(model, byTypeName, new HashSet<string>(StringComparer.Ordinal), out var cycleTypeName))
            {
                var cycleModel = byTypeName[cycleTypeName];
                context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.EntityCycleNotSupported, model.Location.ToLocation(), cycleModel.Name));
            }
        }
    }

    private static bool HasEntityCycle(IReadOnlyList<EntityModel> models)
    {
        var byTypeName = models.ToDictionary(static item => item.TypeName, StringComparer.Ordinal);
        return models.Any(model => HasCycle(model, byTypeName, new HashSet<string>(StringComparer.Ordinal), out _));
    }

    private static bool HasCycle(EntityModel model, Dictionary<string, EntityModel> byTypeName, HashSet<string> stack, out string cycleTypeName)
    {
        if (!stack.Add(model.TypeName))
        {
            cycleTypeName = model.TypeName;
            return true;
        }

        foreach (var member in model.PersistedMembers)
        {
            if (member.RelatedEntityTypeName is string related && byTypeName.TryGetValue(related, out var child) && HasCycle(child, byTypeName, stack, out cycleTypeName))
            {
                return true;
            }
        }

        stack.Remove(model.TypeName);
        cycleTypeName = default!;
        return false;
    }

    private static void AppendKeyExpression(StringBuilder sb, ImmutableArray<MemberModel> keyMembers, Func<MemberModel, string> selector, bool converted)
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

        return member.ValueObject!.FromPrimitiveKind == FromPrimitiveKind.Constructor
            ? $"new {member.TypeName}({expression})"
            : $"{member.TypeName}.{member.ValueObject.FromPrimitiveName}({expression})";
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

    private static int GetRepositoryOptions(INamedTypeSymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (!IsAttribute(attribute, EntityAttributeName))
            {
                continue;
            }

            return attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is not null
                ? Convert.ToInt32(attribute.ConstructorArguments[0].Value)
                : 0;
        }

        return 0;
    }

    private static bool TryGetListEntity(ITypeSymbol type, out ITypeSymbol entityType)
    {
        entityType = default!;
        if (type is not INamedTypeSymbol named || named.TypeArguments.Length != 1)
        {
            return false;
        }

        if (named.OriginalDefinition.ToDisplayString(TypeFormat) != "global::System.Collections.Generic.List<T>")
        {
            return false;
        }

        var elementType = named.TypeArguments[0];
        if (!HasAttribute(elementType, EntityAttributeName))
        {
            return false;
        }

        entityType = elementType;
        return true;
    }

    private static bool HasMatchingConstructor(INamedTypeSymbol symbol, ITypeSymbol[] parameterTypes)
    {
        foreach (var constructor in symbol.InstanceConstructors)
        {
            if (constructor.Parameters.Length != parameterTypes.Length)
            {
                continue;
            }

            var matches = true;
            for (var i = 0; i < parameterTypes.Length; i++)
            {
                if (!SymbolEqualityComparer.Default.Equals(constructor.Parameters[i].Type, parameterTypes[i]))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPartial(INamedTypeSymbol symbol)
    {
        foreach (var syntaxReference in symbol.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is ClassDeclarationSyntax declaration &&
                declaration.Modifiers.Any(static modifier => modifier.ValueText == "partial"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSupportedAutoProperty(IPropertySymbol property)
    {
        if (property.IsIndexer)
        {
            return false;
        }

        foreach (var syntaxReference in property.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is not PropertyDeclarationSyntax syntax || syntax.ExpressionBody is not null || syntax.AccessorList is null)
            {
                return false;
            }

            foreach (var accessor in syntax.AccessorList.Accessors)
            {
                if (accessor.Body is not null || accessor.ExpressionBody is not null)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static ITypeSymbol GetMemberType(ISymbol member)
    {
        return member switch
        {
            IFieldSymbol field => field.Type,
            IPropertySymbol property => property.Type,
            _ => throw new InvalidOperationException(),
        };
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

    private static Location GetPrimaryLocation(ISymbol symbol)
    {
        return symbol.Locations.FirstOrDefault() ?? Location.None;
    }

    private static string GetTypeName(ITypeSymbol symbol)
    {
        return symbol.ToDisplayString(TypeFormat);
    }

    private static string GetDtoTypeName(ITypeSymbol symbol)
    {
        var named = (INamedTypeSymbol)symbol;
        var ns = GetDtoNamespace(named);
        return "global::" + ns + "." + named.Name + "Dto";
    }

    private static string GetDtoNamespace(INamedTypeSymbol symbol)
    {
        var ns = symbol.ContainingNamespace.IsGlobalNamespace ? string.Empty : symbol.ContainingNamespace.ToDisplayString();
        return string.IsNullOrEmpty(ns) ? "Lilja.Repository.Generated.Dtos" : "Lilja.Repository.Generated.Dtos." + ns;
    }

    private static string GetFormatterNamespace(INamedTypeSymbol symbol)
    {
        var ns = symbol.ContainingNamespace.IsGlobalNamespace ? string.Empty : symbol.ContainingNamespace.ToDisplayString();
        return string.IsNullOrEmpty(ns) ? "Lilja.Repository.Generated.Formatters" : "Lilja.Repository.Generated.Formatters." + ns;
    }

    private static string CreateStorageIdentifier(INamedTypeSymbol symbol)
    {
        return symbol.ContainingNamespace.IsGlobalNamespace ? symbol.Name : symbol.ContainingNamespace.ToDisplayString() + "." + symbol.Name;
    }

    private static string EscapeIdentifier(string name)
    {
        return Microsoft.CodeAnalysis.CSharp.SyntaxFacts.GetKeywordKind(name) == Microsoft.CodeAnalysis.CSharp.SyntaxKind.None ? name : "@" + name;
    }

    private static string ToDtoFieldName(MemberModel member)
    {
        return EscapeIdentifier(ToPascalCase(member.Name.TrimStart('_')));
    }

    private static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name) || char.IsUpper(name[0]))
        {
            return name;
        }

        return char.ToUpperInvariant(name[0]) + name.Substring(1);
    }

    private static string ToParameterName(MemberModel member)
    {
        return "p" + member.Index + ToPascalCase(member.Name.TrimStart('_'));
    }

    private static string ToLocalName(MemberModel member)
    {
        return "local" + member.Index + ToPascalCase(member.Name.TrimStart('_'));
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

    private static void AddSource(SourceProductionContext context, EntityModel model, string fileName, string source)
    {
        context.AddSource(model.StorageIdentifier + "." + fileName, SourceText.From(source, Encoding.UTF8));
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

    private static LocationInfo GetLocationInfo(ISymbol symbol)
    {
        var loc = symbol.Locations.FirstOrDefault();
        if (loc is null || !loc.IsInSource)
        {
            return new LocationInfo(string.Empty, 0, 0);
        }
        return new LocationInfo(loc.SourceTree!.FilePath, loc.SourceSpan.Start, loc.SourceSpan.Length);
    }

    private static string GetDtoNamespace(string namespaceName)
    {
        return string.IsNullOrEmpty(namespaceName) ? "Lilja.Repository.Generated.Dtos" : "Lilja.Repository.Generated.Dtos." + namespaceName;
    }

    private static string GetFormatterNamespace(string namespaceName)
    {
        return string.IsNullOrEmpty(namespaceName) ? "Lilja.Repository.Generated.Formatters" : "Lilja.Repository.Generated.Formatters." + namespaceName;
    }

    private static string CreateStorageIdentifier(string name, string namespaceName)
    {
        return string.IsNullOrEmpty(namespaceName) ? name : namespaceName + "." + name;
    }

    private sealed class EntityAnalysis
    {
        public EntityAnalysis(EntityModel? model, ImmutableArray<Diagnostic> diagnostics)
        {
            Model = model;
            Diagnostics = diagnostics;
        }

        public EntityModel? Model { get; }

        public ImmutableArray<Diagnostic> Diagnostics { get; }
    }

    private sealed class LocationInfo : IEquatable<LocationInfo>
    {
        public LocationInfo(string filePath, int startPosition, int length)
        {
            FilePath = filePath;
            StartPosition = startPosition;
            Length = length;
        }

        public string FilePath { get; }
        public int StartPosition { get; }
        public int Length { get; }

        public Location ToLocation()
        {
            if (string.IsNullOrEmpty(FilePath))
            {
                return Location.None;
            }
            return Location.Create(FilePath, new Microsoft.CodeAnalysis.Text.TextSpan(StartPosition, Length), default);
        }

        public bool Equals(LocationInfo? other)
        {
            if (other is null) return false;
            return FilePath == other.FilePath && StartPosition == other.StartPosition && Length == other.Length;
        }

        public override bool Equals(object? obj) => Equals(obj as LocationInfo);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + FilePath.GetHashCode();
                hash = hash * 23 + StartPosition;
                hash = hash * 23 + Length;
                return hash;
            }
        }
    }

    private sealed class EntityModel : IEquatable<EntityModel>
    {
        public EntityModel(
            string name,
            string typeName,
            string namespaceName,
            int repositoryOptions,
            ImmutableArray<MemberModel> persistedMembers,
            ImmutableArray<MemberModel> keyMembers,
            bool hasMatchingConstructor,
            LocationInfo location)
        {
            Name = name;
            TypeName = typeName;
            NamespaceName = namespaceName;
            RepositoryOptions = repositoryOptions;
            PersistedMembers = persistedMembers;
            KeyMembers = keyMembers;
            HasMatchingConstructor = hasMatchingConstructor;
            Location = location;

            StorageIdentifier = CreateStorageIdentifier(name, namespaceName);
            DtoNamespace = GetDtoNamespace(namespaceName);
            DtoName = Name + "Dto";
            DtoTypeName = "global::" + DtoNamespace + "." + DtoName;
            FormatterNamespace = GetFormatterNamespace(namespaceName);
            RepositoryNamespace = string.IsNullOrEmpty(NamespaceName) ? "Repositories" : NamespaceName + ".Repositories";
            RepositoryInterfaceName = "I" + Name + "Repository";
            KeyTypeName = keyMembers.Length == 1
                ? keyMembers[0].TypeName
                : "(" + string.Join(", ", keyMembers.Select(static item => item.TypeName)) + ")";
        }

        public string Name { get; }
        public string TypeName { get; }
        public string NamespaceName { get; }
        public int RepositoryOptions { get; }
        public ImmutableArray<MemberModel> PersistedMembers { get; }
        public ImmutableArray<MemberModel> KeyMembers { get; }
        public bool HasMatchingConstructor { get; }
        public LocationInfo Location { get; }

        public string StorageIdentifier { get; }
        public string DtoNamespace { get; }
        public string DtoName { get; }
        public string DtoTypeName { get; }
        public string FormatterNamespace { get; }
        public string RepositoryNamespace { get; }
        public string RepositoryInterfaceName { get; }
        public string KeyTypeName { get; }
        public bool IsKeyed => KeyMembers.Length > 0;

        public bool Equals(EntityModel? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Name == other.Name &&
                   TypeName == other.TypeName &&
                   NamespaceName == other.NamespaceName &&
                   RepositoryOptions == other.RepositoryOptions &&
                   HasMatchingConstructor == other.HasMatchingConstructor &&
                   SequenceEqual(PersistedMembers, other.PersistedMembers) &&
                   SequenceEqual(KeyMembers, other.KeyMembers);
        }

        private static bool SequenceEqual<T>(ImmutableArray<T> first, ImmutableArray<T> second) where T : IEquatable<T>
        {
            if (first.Length != second.Length) return false;
            for (var i = 0; i < first.Length; i++)
            {
                if (!first[i].Equals(second[i])) return false;
            }
            return true;
        }

        public override bool Equals(object? obj) => Equals(obj as EntityModel);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + Name.GetHashCode();
                hash = hash * 23 + TypeName.GetHashCode();
                hash = hash * 23 + NamespaceName.GetHashCode();
                hash = hash * 23 + RepositoryOptions;
                hash = hash * 23 + (HasMatchingConstructor ? 1 : 0);
                foreach (var member in PersistedMembers)
                {
                    hash = hash * 23 + member.GetHashCode();
                }
                return hash;
            }
        }
    }

    private sealed class MemberModel : IEquatable<MemberModel>
    {
        public MemberModel(
            string name,
            string accessName,
            string typeName,
            string dtoTypeName,
            int index,
            int declarationOrder,
            bool isKey,
            MemberKind kind,
            string? relatedEntityTypeName,
            string? relatedEntityDtoTypeName,
            ValueObjectShape? valueObject)
        {
            Name = name;
            AccessName = accessName;
            TypeName = typeName;
            DtoTypeName = dtoTypeName;
            Index = index;
            DeclarationOrder = declarationOrder;
            IsKey = isKey;
            Kind = kind;
            RelatedEntityTypeName = relatedEntityTypeName;
            RelatedEntityDtoTypeName = relatedEntityDtoTypeName;
            ValueObject = valueObject;
        }

        public string Name { get; }
        public string AccessName { get; }
        public string TypeName { get; }
        public string DtoTypeName { get; }
        public int Index { get; }
        public int DeclarationOrder { get; }
        public bool IsKey { get; }
        public MemberKind Kind { get; }
        public string? RelatedEntityTypeName { get; }
        public string? RelatedEntityDtoTypeName { get; }
        public ValueObjectShape? ValueObject { get; }

        public bool Equals(MemberModel? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Name == other.Name &&
                   AccessName == other.AccessName &&
                   TypeName == other.TypeName &&
                   DtoTypeName == other.DtoTypeName &&
                   Index == other.Index &&
                   DeclarationOrder == other.DeclarationOrder &&
                   IsKey == other.IsKey &&
                   Kind == other.Kind &&
                   RelatedEntityTypeName == other.RelatedEntityTypeName &&
                   RelatedEntityDtoTypeName == other.RelatedEntityDtoTypeName &&
                   Equals(ValueObject, other.ValueObject);
        }

        public override bool Equals(object? obj) => Equals(obj as MemberModel);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + Name.GetHashCode();
                hash = hash * 23 + AccessName.GetHashCode();
                hash = hash * 23 + TypeName.GetHashCode();
                hash = hash * 23 + DtoTypeName.GetHashCode();
                hash = hash * 23 + Index;
                hash = hash * 23 + DeclarationOrder;
                hash = hash * 23 + (IsKey ? 1 : 0);
                hash = hash * 23 + Kind.GetHashCode();
                hash = hash * 23 + (RelatedEntityTypeName?.GetHashCode() ?? 0);
                hash = hash * 23 + (RelatedEntityDtoTypeName?.GetHashCode() ?? 0);
                hash = hash * 23 + (ValueObject?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }

    private sealed class ValueObjectShape : IEquatable<ValueObjectShape>
    {
        public ValueObjectShape(string toPrimitiveName, string primitiveTypeName, string fromPrimitiveName, FromPrimitiveKind fromPrimitiveKind)
        {
            ToPrimitiveName = toPrimitiveName;
            PrimitiveTypeName = primitiveTypeName;
            FromPrimitiveName = fromPrimitiveName;
            FromPrimitiveKind = fromPrimitiveKind;
        }

        public string ToPrimitiveName { get; }
        public string PrimitiveTypeName { get; }
        public string FromPrimitiveName { get; }
        public FromPrimitiveKind FromPrimitiveKind { get; }

        public bool Equals(ValueObjectShape? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return ToPrimitiveName == other.ToPrimitiveName &&
                   PrimitiveTypeName == other.PrimitiveTypeName &&
                   FromPrimitiveName == other.FromPrimitiveName &&
                   FromPrimitiveKind == other.FromPrimitiveKind;
        }

        public override bool Equals(object? obj) => Equals(obj as ValueObjectShape);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + ToPrimitiveName.GetHashCode();
                hash = hash * 23 + PrimitiveTypeName.GetHashCode();
                hash = hash * 23 + FromPrimitiveName.GetHashCode();
                hash = hash * 23 + FromPrimitiveKind.GetHashCode();
                return hash;
            }
        }
    }

    private enum MemberKind
    {
        Value,
        ValueObject,
        Entity,
        EntityList,
    }

    private enum FromPrimitiveKind
    {
        Constructor,
        StaticFactory,
    }
}
