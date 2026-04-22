using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Lilja.Repository.Analyzer;

[Generator]
public sealed class LiljaRepositoryGenerator : IIncrementalGenerator
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

    private static EntityAnalysis AnalyzeEntity(INamedTypeSymbol entitySymbol)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var entityLocation = GetPrimaryLocation(entitySymbol);

        if (!IsPartial(entitySymbol))
        {
            diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.EntityMustBePartial, entityLocation));
        }

        if (entitySymbol.TypeParameters.Length > 0)
        {
            diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.GenericEntityNotSupported, entityLocation));
        }

        var keyMembers = new List<MemberModel>();
        var persistedMembers = new List<MemberModel>();

        foreach (var member in entitySymbol.GetMembers())
        {
            var hasKey = HasAttribute(member, KeyAttributeMetadataName);
            var hasPersist = TryGetPersistIndex(member, out var persistIndex);
            if (!hasKey && !hasPersist)
            {
                continue;
            }

            var memberLocation = GetPrimaryLocation(member);
            if (member.IsStatic)
            {
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.StaticMemberNotSupported, memberLocation));
                continue;
            }

            if (member is IFieldSymbol fieldSymbol)
            {
                if (fieldSymbol.IsImplicitlyDeclared)
                {
                    continue;
                }

                var model = CreateMemberModel(fieldSymbol, hasKey, hasPersist, persistIndex, diagnostics);
                if (model is null)
                {
                    continue;
                }

                if (hasKey)
                {
                    keyMembers.Add(model);
                }

                if (hasPersist)
                {
                    persistedMembers.Add(model);
                }

                continue;
            }

            if (member is IPropertySymbol propertySymbol)
            {
                if (!IsSupportedAutoProperty(propertySymbol))
                {
                    diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.OnlyAutoPropertiesSupported, memberLocation));
                    continue;
                }

                var model = CreateMemberModel(propertySymbol, hasKey, hasPersist, persistIndex, diagnostics);
                if (model is null)
                {
                    continue;
                }

                if (hasKey)
                {
                    keyMembers.Add(model);
                }

                if (hasPersist)
                {
                    persistedMembers.Add(model);
                }

                continue;
            }

            diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.OnlyAutoPropertiesSupported, memberLocation));
        }

        foreach (var duplicateGroup in persistedMembers.GroupBy(static member => member.PersistIndex!.Value).Where(static group => group.Count() > 1))
        {
            foreach (var duplicate in duplicateGroup)
            {
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.PersistIndexMustBeUnique, duplicate.Location));
            }
        }

        if (persistedMembers.Count > 0)
        {
            foreach (var keyMember in keyMembers)
            {
                if (!keyMember.HasPersist)
                {
                    diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.PersistedKeysMustAlsoBePersisted, keyMember.Location));
                }
            }
        }

        persistedMembers.Sort(static (left, right) => left.PersistIndex!.Value.CompareTo(right.PersistIndex!.Value));

        var namespaceName = entitySymbol.ContainingNamespace.IsGlobalNamespace ? string.Empty : entitySymbol.ContainingNamespace.ToDisplayString();
        var storageIdentifier = string.IsNullOrEmpty(namespaceName) ? entitySymbol.Name : namespaceName + "." + entitySymbol.Name;
        var constructorParameterTypes = persistedMembers.Select(static member => member.TypeSymbol).ToArray();
        var needsGeneratedConstructor = persistedMembers.Count > 0 && !HasMatchingConstructor(entitySymbol, constructorParameterTypes);

        return new EntityAnalysis(
            new EntityModel(
                entitySymbol,
                namespaceName,
                storageIdentifier,
                keyMembers.ToImmutableArray(),
                persistedMembers.ToImmutableArray(),
                needsGeneratedConstructor),
            diagnostics.ToImmutable());
    }

    private static MemberModel? CreateMemberModel(ISymbol member, bool hasKey, bool hasPersist, int? persistIndex, ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var typeSymbol = member switch
        {
            IFieldSymbol fieldSymbol => fieldSymbol.Type,
            IPropertySymbol propertySymbol => propertySymbol.Type,
            _ => null,
        };

        if (typeSymbol is null)
        {
            return null;
        }

        ValueObjectShape? valueObjectShape = null;
        if (hasPersist)
        {
            var diagnosticCount = diagnostics.Count;
            valueObjectShape = AnalyzeValueObject(typeSymbol, GetPrimaryLocation(member), diagnostics);
            if (diagnostics.Count != diagnosticCount && valueObjectShape is null)
            {
                return null;
            }
        }

        var dtoFields = BuildDtoFields(member.Name, typeSymbol, valueObjectShape);
        return new MemberModel(
            member.Name,
            EscapeIdentifier(member.Name),
            typeSymbol,
            GetTypeName(typeSymbol),
            member is IPropertySymbol,
            hasKey,
            hasPersist,
            persistIndex,
            valueObjectShape,
            dtoFields,
            GetPrimaryLocation(member));
    }

    private static ValueObjectShape? AnalyzeValueObject(ITypeSymbol typeSymbol, Location location, ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var methodMembers = typeSymbol.GetMembers().OfType<IMethodSymbol>().Where(static method => method.MethodKind != MethodKind.Constructor).ToArray();
        var toPrimitiveCandidates = methodMembers.Where(static method => HasAttribute(method, ToPrimitiveAttributeMetadataName)).ToArray();
        var fromPrimitiveMethods = methodMembers.Where(static method => HasAttribute(method, FromPrimitiveAttributeMetadataName)).ToArray();
        var fromPrimitiveConstructors = typeSymbol.GetMembers().OfType<IMethodSymbol>()
            .Where(static method => method.MethodKind == MethodKind.Constructor && HasAttribute(method, FromPrimitiveAttributeMetadataName))
            .ToArray();

        var hasAnyValueObjectAttribute = toPrimitiveCandidates.Length > 0 || fromPrimitiveMethods.Length > 0 || fromPrimitiveConstructors.Length > 0;
        if (!hasAnyValueObjectAttribute)
        {
            return null;
        }

        if (toPrimitiveCandidates.Length != 1)
        {
            diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.InvalidToPrimitiveDefinition, location));
            return null;
        }

        var toPrimitiveMethod = toPrimitiveCandidates[0];
        if (toPrimitiveMethod.IsStatic || toPrimitiveMethod.Parameters.Length != 0 || toPrimitiveMethod.ReturnsVoid)
        {
            diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.InvalidToPrimitiveDefinition, location));
            return null;
        }

        var primitiveParts = GetPrimitiveParts(toPrimitiveMethod.ReturnType);
        if (primitiveParts.Length == 0)
        {
            diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.InvalidToPrimitiveDefinition, location));
            return null;
        }

        if (fromPrimitiveConstructors.Length + fromPrimitiveMethods.Length != 1)
        {
            diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.InvalidFromPrimitiveDefinition, location));
            return null;
        }

        if (fromPrimitiveMethods.Length == 1)
        {
            var factoryMethod = fromPrimitiveMethods[0];
            if (!factoryMethod.IsStatic ||
                factoryMethod.TypeParameters.Length > 0 ||
                !SymbolEqualityComparer.Default.Equals(factoryMethod.ReturnType, typeSymbol) ||
                !ParametersMatchPrimitiveParts(factoryMethod.Parameters, primitiveParts))
            {
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.InvalidFromPrimitiveDefinition, location));
                return null;
            }

            return new ValueObjectShape(
                toPrimitiveMethod.Name,
                ValueObjectCreationKind.StaticFactory,
                EscapeIdentifier(factoryMethod.Name),
                primitiveParts);
        }

        var constructor = fromPrimitiveConstructors[0];
        if (!ParametersMatchPrimitiveParts(constructor.Parameters, primitiveParts))
        {
            diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.InvalidFromPrimitiveDefinition, location));
            return null;
        }

        return new ValueObjectShape(
            toPrimitiveMethod.Name,
            ValueObjectCreationKind.Constructor,
            string.Empty,
            primitiveParts);
    }

    private static bool ParametersMatchPrimitiveParts(ImmutableArray<IParameterSymbol> parameters, ImmutableArray<PrimitivePartModel> primitiveParts)
    {
        if (parameters.Length != primitiveParts.Length)
        {
            return false;
        }

        for (var index = 0; index < parameters.Length; index++)
        {
            if (!SymbolEqualityComparer.Default.Equals(parameters[index].Type, primitiveParts[index].TypeSymbol))
            {
                return false;
            }
        }

        return true;
    }

    private static ImmutableArray<PrimitivePartModel> GetPrimitiveParts(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is INamedTypeSymbol namedType && namedType.IsTupleType)
        {
            var builder = ImmutableArray.CreateBuilder<PrimitivePartModel>(namedType.TupleElements.Length);
            for (var index = 0; index < namedType.TupleElements.Length; index++)
            {
                var tupleElement = namedType.TupleElements[index];
                var elementName = string.IsNullOrEmpty(tupleElement.Name) ? $"Item{index + 1}" : tupleElement.Name;
                builder.Add(new PrimitivePartModel(
                    tupleElement.Type,
                    GetTypeName(tupleElement.Type),
                    EscapeIdentifier(elementName),
                    elementName));
            }

            return builder.ToImmutable();
        }

        return ImmutableArray.Create(new PrimitivePartModel(typeSymbol, GetTypeName(typeSymbol), string.Empty, string.Empty));
    }

    private static ImmutableArray<DtoFieldModel> BuildDtoFields(string memberName, ITypeSymbol memberType, ValueObjectShape? valueObjectShape)
    {
        var baseFieldName = EscapeIdentifier(ToPascalCase(memberName.TrimStart('_')));

        if (valueObjectShape is null)
        {
            return ImmutableArray.Create(new DtoFieldModel(baseFieldName, GetTypeName(memberType), string.Empty));
        }

        if (valueObjectShape.PrimitiveParts.Length == 1)
        {
            var part = valueObjectShape.PrimitiveParts[0];
            return ImmutableArray.Create(new DtoFieldModel(baseFieldName, part.TypeName, string.Empty));
        }

        var builder = ImmutableArray.CreateBuilder<DtoFieldModel>(valueObjectShape.PrimitiveParts.Length);
        foreach (var part in valueObjectShape.PrimitiveParts)
        {
            var fieldName = EscapeIdentifier(baseFieldName + "_" + part.DtoSuffixName);
            builder.Add(new DtoFieldModel(fieldName, part.TypeName, part.AccessName));
        }

        return builder.ToImmutable();
    }

    private static bool HasMatchingConstructor(INamedTypeSymbol entitySymbol, ITypeSymbol[] parameterTypes)
    {
        foreach (var constructor in entitySymbol.InstanceConstructors)
        {
            if (constructor.Parameters.Length != parameterTypes.Length)
            {
                continue;
            }

            var matches = true;
            for (var index = 0; index < parameterTypes.Length; index++)
            {
                if (!SymbolEqualityComparer.Default.Equals(constructor.Parameters[index].Type, parameterTypes[index]))
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

    private static bool IsSupportedAutoProperty(IPropertySymbol propertySymbol)
    {
        if (propertySymbol.IsIndexer)
        {
            return false;
        }

        foreach (var syntaxReference in propertySymbol.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is not PropertyDeclarationSyntax propertySyntax)
            {
                return false;
            }

            if (propertySyntax.ExpressionBody is not null || propertySyntax.AccessorList is null)
            {
                return false;
            }

            foreach (var accessor in propertySyntax.AccessorList.Accessors)
            {
                if (accessor.Body is not null || accessor.ExpressionBody is not null)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsPartial(INamedTypeSymbol entitySymbol)
    {
        foreach (var syntaxReference in entitySymbol.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is ClassDeclarationSyntax classDeclaration &&
                classDeclaration.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.PartialKeyword)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAttribute(ISymbol symbol, string metadataName)
    {
        foreach (var attributeData in symbol.GetAttributes())
        {
            if (attributeData.AttributeClass?.ToDisplayString() == metadataName)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetPersistIndex(ISymbol symbol, out int? index)
    {
        foreach (var attributeData in symbol.GetAttributes())
        {
            if (attributeData.AttributeClass?.ToDisplayString() != PersistAttributeMetadataName)
            {
                continue;
            }

            if (attributeData.ConstructorArguments.Length == 1 &&
                attributeData.ConstructorArguments[0].Value is int persistIndex)
            {
                index = persistIndex;
                return true;
            }
        }

        index = null;
        return false;
    }

    private static Location GetPrimaryLocation(ISymbol symbol)
    {
        return symbol.Locations.FirstOrDefault() ?? Location.None;
    }

    private static string GetTypeName(ITypeSymbol typeSymbol)
    {
        return typeSymbol.ToDisplayString(FullyQualifiedTypeFormat);
    }

    private static string EscapeIdentifier(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            return identifier;
        }

        return SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None ||
               SyntaxFacts.GetContextualKeywordKind(identifier) != SyntaxKind.None
            ? "@" + identifier
            : identifier;
    }

    private static string ToPascalCase(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            return identifier;
        }

        if (identifier.Length == 1)
        {
            return identifier.ToUpperInvariant();
        }

        return char.ToUpperInvariant(identifier[0]) + identifier.Substring(1);
    }

    private static string ToCamelCase(string identifier)
    {
        var trimmed = identifier.TrimStart('_');
        if (string.IsNullOrEmpty(trimmed))
        {
            trimmed = identifier;
        }

        if (string.IsNullOrEmpty(trimmed))
        {
            trimmed = "value";
        }

        if (trimmed.Length == 1)
        {
            return EscapeIdentifier(trimmed.ToLowerInvariant());
        }

        return EscapeIdentifier(char.ToLowerInvariant(trimmed[0]) + trimmed.Substring(1));
    }

    private static void EmitEntity(SourceProductionContext context, EntityModel model, bool hasMessagePack)
    {
        AddSource(context, model, $"I{model.EntityName}Repository.g.cs", GenerateInterface(model));
        AddSource(context, model, $"InMemory{model.EntityName}Repository.g.cs", GenerateInMemoryRepository(model));
        if (model.IsKeyed)
        {
            AddSource(context, model, $"{model.EntityName}.KeyAccessor.g.cs", GenerateKeyAccessorPartial(model));
        }

        if (!model.IsPersisted)
        {
            return;
        }

        AddSource(context, model, $"{model.EntityName}Dto.g.cs", GenerateDto(model));
        AddSource(context, model, $"{model.EntityName}StorageEnvelope.g.cs", GenerateStorageEnvelope(model));
        AddSource(context, model, $"Json{model.EntityName}Repository.g.cs", GenerateJsonRepository(model));
        AddSource(context, model, $"{model.EntityName}.Converter.g.cs", GenerateConverterPartial(model));

        if (!hasMessagePack)
        {
            return;
        }

        AddSource(context, model, $"{model.EntityName}DtoFormatter.g.cs", GenerateDtoFormatter(model));
        AddSource(context, model, $"{model.EntityName}StorageEnvelopeFormatter.g.cs", GenerateStorageEnvelopeFormatter(model));
        AddSource(context, model, $"MessagePack{model.EntityName}Repository.g.cs", GenerateMessagePackRepository(model));
    }

    private static void AddSource(SourceProductionContext context, EntityModel model, string fileName, string source)
    {
        var hintName = string.IsNullOrEmpty(model.NamespaceName) ? fileName : model.StorageIdentifier + "." + fileName;
        context.AddSource(hintName, SourceText.From(source, Encoding.UTF8));
    }

    private static string GenerateInterface(EntityModel model)
    {
        var sb = CreateSourceBuilder();
        AppendNamespaceStart(sb, model.RepositoryNamespace);
        sb.Append("public interface I").Append(model.EntityName).AppendLine("Repository");
        sb.AppendLine("{");
        sb.AppendLine("    global::Cysharp.Threading.Tasks.UniTask InitializeAsync(global::System.Threading.CancellationToken ct = default);");
        if (model.IsKeyed)
        {
            sb.Append("    ").Append(model.EntityTypeName).Append("? Read(global::Lilja.Repository.IReadOnlyTx tx, ").Append(model.KeyTypeName).AppendLine(" key);");
            sb.Append("    void Create(global::Lilja.Repository.IReadWriteTx tx, ").Append(model.EntityTypeName).AppendLine(" entity);");
            sb.Append("    void Update(global::Lilja.Repository.IReadWriteTx tx, ").Append(model.EntityTypeName).AppendLine(" entity);");
            sb.Append("    void Delete(global::Lilja.Repository.IReadWriteTx tx, ").Append(model.KeyTypeName).AppendLine(" key);");
            sb.Append("    global::System.Collections.Generic.IReadOnlyList<").Append(model.EntityTypeName).AppendLine("> All(global::Lilja.Repository.IReadOnlyTx tx);");
        }
        else
        {
            sb.Append("    ").Append(model.EntityTypeName).AppendLine("? Read(global::Lilja.Repository.IReadOnlyTx tx);");
            sb.Append("    void Create(global::Lilja.Repository.IReadWriteTx tx, ").Append(model.EntityTypeName).AppendLine(" entity);");
            sb.Append("    void Update(global::Lilja.Repository.IReadWriteTx tx, ").Append(model.EntityTypeName).AppendLine(" entity);");
            sb.AppendLine("    void Delete(global::Lilja.Repository.IReadWriteTx tx);");
        }

        sb.AppendLine("}");
        AppendNamespaceEnd(sb, model.RepositoryNamespace);
        return sb.ToString();
    }

    private static string GenerateInMemoryRepository(EntityModel model)
    {
        var sb = CreateSourceBuilder();
        AppendNamespaceStart(sb, model.RepositoryNamespace);
        if (model.IsKeyed)
        {
            sb.Append("public sealed class InMemory").Append(model.EntityName).Append("Repository : global::Lilja.Repository.InMemoryKeyedRepositoryBase<")
                .Append(model.EntityTypeName).Append(", ").Append(model.KeyTypeName).Append(">, I").Append(model.EntityName).AppendLine("Repository");
            sb.AppendLine("{");
            AppendTrackerConstructor(sb, $"InMemory{model.EntityName}Repository", "InMemory");
            sb.Append("    protected override ").Append(model.KeyTypeName).Append(" GetKey(").Append(model.EntityTypeName).AppendLine(" entity)");
            sb.AppendLine("    {");
            sb.Append("        return ").Append(model.EntityTypeName).AppendLine(".GetKey(entity);");
            sb.AppendLine("    }");
            sb.AppendLine("}");
        }
        else
        {
            sb.Append("public sealed class InMemory").Append(model.EntityName).Append("Repository : global::Lilja.Repository.InMemorySingletonRepositoryBase<")
                .Append(model.EntityTypeName).Append(">, I").Append(model.EntityName).AppendLine("Repository");
            sb.AppendLine("{");
            AppendTrackerConstructor(sb, $"InMemory{model.EntityName}Repository", "InMemory");
            sb.AppendLine("}");
        }

        AppendNamespaceEnd(sb, model.RepositoryNamespace);
        return sb.ToString();
    }

    private static string GenerateJsonRepository(EntityModel model)
    {
        var sb = CreateSourceBuilder();
        AppendNamespaceStart(sb, model.RepositoryNamespace);
        if (model.IsKeyed)
        {
            sb.Append("public sealed class Json").Append(model.EntityName).Append("Repository : global::Lilja.Repository.PersistedKeyedRepositoryBase<")
                .Append(model.EntityTypeName).Append(", ").Append(model.KeyTypeName).Append(", ").Append(model.DtoTypeName).Append(">, I")
                .Append(model.EntityName).AppendLine("Repository");
            sb.AppendLine("{");
            AppendPersistedConstructor(sb, $"Json{model.EntityName}Repository", "json", "Json", model.StorageIdentifier);
            AppendToDtoOverride(sb, model);
            AppendFromDtoOverride(sb, model);
            sb.Append("    protected override ").Append(model.KeyTypeName).Append(" GetKeyFromDto(").Append(model.DtoTypeName).AppendLine(" dto)");
            sb.AppendLine("    {");
            sb.Append("        return ").Append(model.EntityTypeName).AppendLine(".GetKeyFromDto(dto);");
            sb.AppendLine("    }");
            AppendJsonKeyedLoadSave(sb, model);
            sb.AppendLine("}");
        }
        else
        {
            sb.Append("public sealed class Json").Append(model.EntityName).Append("Repository : global::Lilja.Repository.PersistedSingletonRepositoryBase<")
                .Append(model.EntityTypeName).Append(", ").Append(model.DtoTypeName).Append(">, I").Append(model.EntityName).AppendLine("Repository");
            sb.AppendLine("{");
            AppendPersistedConstructor(sb, $"Json{model.EntityName}Repository", "json", "Json", model.StorageIdentifier);
            AppendToDtoOverride(sb, model);
            AppendFromDtoOverride(sb, model);
            AppendJsonSingletonLoadSave(sb, model);
            sb.AppendLine("}");
        }

        AppendNamespaceEnd(sb, model.RepositoryNamespace);
        return sb.ToString();
    }

    private static string GenerateMessagePackRepository(EntityModel model)
    {
        var sb = CreateSourceBuilder();
        AppendNamespaceStart(sb, model.RepositoryNamespace);
        if (model.IsKeyed)
        {
            sb.Append("public sealed class MessagePack").Append(model.EntityName).Append("Repository : global::Lilja.Repository.PersistedKeyedRepositoryBase<")
                .Append(model.EntityTypeName).Append(", ").Append(model.KeyTypeName).Append(", ").Append(model.DtoTypeName).Append(">, I")
                .Append(model.EntityName).AppendLine("Repository");
            sb.AppendLine("{");
            sb.AppendLine("    private readonly global::MessagePack.MessagePackSerializerOptions _options;");
            AppendMessagePackConstructor(sb, $"MessagePack{model.EntityName}Repository", model);
            AppendToDtoOverride(sb, model);
            AppendFromDtoOverride(sb, model);
            sb.Append("    protected override ").Append(model.KeyTypeName).Append(" GetKeyFromDto(").Append(model.DtoTypeName).AppendLine(" dto)");
            sb.AppendLine("    {");
            sb.Append("        return ").Append(model.EntityTypeName).AppendLine(".GetKeyFromDto(dto);");
            sb.AppendLine("    }");
            AppendMessagePackKeyedLoadSave(sb, model);
            sb.AppendLine("}");
        }
        else
        {
            sb.Append("public sealed class MessagePack").Append(model.EntityName).Append("Repository : global::Lilja.Repository.PersistedSingletonRepositoryBase<")
                .Append(model.EntityTypeName).Append(", ").Append(model.DtoTypeName).Append(">, I").Append(model.EntityName).AppendLine("Repository");
            sb.AppendLine("{");
            sb.AppendLine("    private readonly global::MessagePack.MessagePackSerializerOptions _options;");
            AppendMessagePackConstructor(sb, $"MessagePack{model.EntityName}Repository", model);
            AppendToDtoOverride(sb, model);
            AppendFromDtoOverride(sb, model);
            AppendMessagePackSingletonLoadSave(sb, model);
            sb.AppendLine("}");
        }

        AppendNamespaceEnd(sb, model.RepositoryNamespace);
        return sb.ToString();
    }

    private static string GenerateDto(EntityModel model)
    {
        var sb = CreateSourceBuilder();
        AppendNamespaceStart(sb, model.DtoNamespace);
        sb.AppendLine("[global::System.Serializable]");
        sb.Append("public sealed class ").Append(model.DtoTypeNameWithoutNamespace).AppendLine();
        sb.AppendLine("{");
        foreach (var member in model.PersistedMembers)
        {
            foreach (var dtoField in member.DtoFields)
            {
                sb.Append("    public ").Append(dtoField.TypeName).Append(' ').Append(dtoField.Name).AppendLine(" = default!;");
            }
        }

        sb.AppendLine("}");
        AppendNamespaceEnd(sb, model.DtoNamespace);
        return sb.ToString();
    }

    private static string GenerateStorageEnvelope(EntityModel model)
    {
        var sb = CreateSourceBuilder();
        AppendNamespaceStart(sb, model.StorageNamespace);
        sb.AppendLine("[global::System.Serializable]");
        sb.Append("internal sealed class ").Append(model.StorageEnvelopeTypeNameWithoutNamespace).AppendLine();
        sb.AppendLine("{");
        if (model.IsKeyed)
        {
            sb.Append("    public global::System.Collections.Generic.List<").Append(model.DtoTypeName).Append("> Items = new global::System.Collections.Generic.List<")
                .Append(model.DtoTypeName).AppendLine(">();");
        }
        else
        {
            sb.AppendLine("    public bool HasValue;");
            sb.Append("    public ").Append(model.DtoTypeName).AppendLine("? Item;");
        }

        sb.AppendLine("}");
        AppendNamespaceEnd(sb, model.StorageNamespace);
        return sb.ToString();
    }

    private static string GenerateConverterPartial(EntityModel model)
    {
        var sb = CreateSourceBuilder();
        AppendNamespaceStart(sb, model.NamespaceName);
        sb.Append("public partial class ").Append(model.EntityName).AppendLine();
        sb.AppendLine("{");
        sb.Append("    internal static ").Append(model.DtoTypeName).Append(" ToDto(").Append(model.EntityTypeName).AppendLine(" entity)");
        sb.AppendLine("    {");
        for (var index = 0; index < model.PersistedMembers.Length; index++)
        {
            var member = model.PersistedMembers[index];
            if (member.ValueObjectShape?.PrimitiveParts.Length is not > 1)
            {
                continue;
            }

            sb.Append("        var primitive").Append(index).Append(" = entity.").Append(member.AccessibleName).Append('.').Append(member.ValueObjectShape!.ToPrimitiveMethodName).AppendLine("();");
        }

        sb.Append("        return new ").Append(model.DtoTypeName).AppendLine();
        sb.AppendLine("        {");
        for (var memberIndex = 0; memberIndex < model.PersistedMembers.Length; memberIndex++)
        {
            var member = model.PersistedMembers[memberIndex];
            for (var fieldIndex = 0; fieldIndex < member.DtoFields.Length; fieldIndex++)
            {
                var dtoField = member.DtoFields[fieldIndex];
                sb.Append("            ").Append(dtoField.Name).Append(" = ").Append(GetToDtoExpression(model, member, memberIndex, fieldIndex)).AppendLine(",");
            }
        }

        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.Append("    internal static ").Append(model.EntityTypeName).Append(" FromDto(").Append(model.DtoTypeName).AppendLine(" dto)");
        sb.AppendLine("    {");
        sb.Append("        return new ").Append(model.EntityTypeName).Append('(');
        for (var index = 0; index < model.PersistedMembers.Length; index++)
        {
            if (index > 0)
            {
                sb.Append(", ");
            }

            sb.Append(GetFromDtoArgumentExpression(model.PersistedMembers[index]));
        }

        sb.AppendLine(");");
        sb.AppendLine("    }");

        if (model.NeedsGeneratedConstructor)
        {
            sb.AppendLine();
            sb.Append("    private ").Append(model.EntityName).Append('(');
            for (var index = 0; index < model.PersistedMembers.Length; index++)
            {
                var member = model.PersistedMembers[index];
                if (index > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(member.TypeName).Append(' ').Append(ToCamelCase(member.Name));
            }

            sb.AppendLine(")");
            sb.AppendLine("    {");
            foreach (var member in model.PersistedMembers)
            {
                sb.Append("        this.").Append(member.AccessibleName).Append(" = ").Append(ToCamelCase(member.Name)).AppendLine(";");
            }

            sb.AppendLine("    }");
        }

        sb.AppendLine("}");
        AppendNamespaceEnd(sb, model.NamespaceName);
        return sb.ToString();
    }

    private static string GenerateKeyAccessorPartial(EntityModel model)
    {
        var sb = CreateSourceBuilder();
        AppendNamespaceStart(sb, model.NamespaceName);
        sb.Append("public partial class ").Append(model.EntityName).AppendLine();
        sb.AppendLine("{");
        sb.Append("    internal static ").Append(model.KeyTypeName).Append(" GetKey(").Append(model.EntityTypeName).AppendLine(" entity)");
        sb.AppendLine("    {");
        sb.Append("        return ").Append(GetEntityKeyExpression(model)).AppendLine(";");
        sb.AppendLine("    }");
        if (model.IsPersisted)
        {
            sb.AppendLine();
            sb.Append("    internal static ").Append(model.KeyTypeName).Append(" GetKeyFromDto(").Append(model.DtoTypeName).AppendLine(" dto)");
            sb.AppendLine("    {");
            sb.Append("        return ").Append(GetDtoKeyExpression(model)).AppendLine(";");
            sb.AppendLine("    }");
        }

        sb.AppendLine("}");
        AppendNamespaceEnd(sb, model.NamespaceName);
        return sb.ToString();
    }

    private static string GenerateDtoFormatter(EntityModel model)
    {
        var sb = CreateSourceBuilder();
        AppendNamespaceStart(sb, model.FormatterNamespace);
        sb.Append("public sealed class ").Append(model.DtoFormatterTypeNameWithoutNamespace).Append(" : global::MessagePack.Formatters.IMessagePackFormatter<")
            .Append(model.DtoTypeName).AppendLine(">");
        sb.AppendLine("{");
        sb.AppendLine("    private static global::MessagePack.Formatters.IMessagePackFormatter<T> ResolveFormatter<T>(global::MessagePack.MessagePackSerializerOptions options)");
        sb.AppendLine("    {");
        sb.AppendLine("        return options.Resolver.GetFormatter<T>() ?? throw new global::MessagePack.MessagePackSerializationException($\"Formatter not found for {typeof(T).FullName}.\");");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.Append("    public void Serialize(ref global::MessagePack.MessagePackWriter writer, ").Append(model.DtoTypeName)
            .AppendLine(" value, global::MessagePack.MessagePackSerializerOptions options)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (value is null)");
        sb.AppendLine("        {");
        sb.AppendLine("            writer.WriteNil();");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.Append("        writer.WriteArrayHeader(").Append(model.AllDtoFields.Length).AppendLine(");");
        foreach (var dtoField in model.AllDtoFields)
        {
            sb.Append("        ResolveFormatter<").Append(dtoField.TypeName).Append(">(options).Serialize(ref writer, value.")
                .Append(dtoField.Name).AppendLine(", options);");
        }

        sb.AppendLine("    }");
        sb.AppendLine();
        sb.Append("    public ").Append(model.DtoTypeName).Append(" Deserialize(ref global::MessagePack.MessagePackReader reader, global::MessagePack.MessagePackSerializerOptions options)")
            .AppendLine();
        sb.AppendLine("    {");
        sb.AppendLine("        if (reader.TryReadNil())");
        sb.AppendLine("        {");
        sb.AppendLine("            return null!;");
        sb.AppendLine("        }");
        sb.Append("        var value = new ").Append(model.DtoTypeName).AppendLine("();");
        sb.AppendLine("        var length = reader.ReadArrayHeader();");
        for (var index = 0; index < model.AllDtoFields.Length; index++)
        {
            var dtoField = model.AllDtoFields[index];
            sb.Append("        if (length > ").Append(index).AppendLine(")");
            sb.AppendLine("        {");
            sb.Append("            value.").Append(dtoField.Name).Append(" = ResolveFormatter<").Append(dtoField.TypeName)
                .AppendLine(">(options).Deserialize(ref reader, options);");
            sb.AppendLine("        }");
        }

        sb.Append("        for (var index = ").Append(model.AllDtoFields.Length).AppendLine("; index < length; index++)");
        sb.AppendLine("        {");
        sb.AppendLine("            reader.Skip();");
        sb.AppendLine("        }");
        sb.AppendLine("        return value;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        AppendNamespaceEnd(sb, model.FormatterNamespace);
        return sb.ToString();
    }

    private static string GenerateStorageEnvelopeFormatter(EntityModel model)
    {
        var sb = CreateSourceBuilder();
        AppendNamespaceStart(sb, model.FormatterNamespace);
        sb.Append("internal sealed class ").Append(model.StorageEnvelopeFormatterTypeNameWithoutNamespace).Append(" : global::MessagePack.Formatters.IMessagePackFormatter<")
            .Append(model.StorageEnvelopeTypeName).AppendLine(">");
        sb.AppendLine("{");
        sb.AppendLine("    private static global::MessagePack.Formatters.IMessagePackFormatter<T> ResolveFormatter<T>(global::MessagePack.MessagePackSerializerOptions options)");
        sb.AppendLine("    {");
        sb.AppendLine("        return options.Resolver.GetFormatter<T>() ?? throw new global::MessagePack.MessagePackSerializationException($\"Formatter not found for {typeof(T).FullName}.\");");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.Append("    public void Serialize(ref global::MessagePack.MessagePackWriter writer, ").Append(model.StorageEnvelopeTypeName)
            .AppendLine(" value, global::MessagePack.MessagePackSerializerOptions options)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (value is null)");
        sb.AppendLine("        {");
        sb.AppendLine("            writer.WriteNil();");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        if (model.IsKeyed)
        {
            sb.AppendLine("        writer.WriteArrayHeader(1);");
            sb.Append("        ResolveFormatter<global::System.Collections.Generic.List<").Append(model.DtoTypeName)
                .Append(">>(options).Serialize(ref writer, value.Items, options);").AppendLine();
        }
        else
        {
            sb.AppendLine("        writer.WriteArrayHeader(2);");
            sb.AppendLine("        ResolveFormatter<bool>(options).Serialize(ref writer, value.HasValue, options);");
            sb.Append("        ResolveFormatter<").Append(model.DtoTypeName).AppendLine("?>(options).Serialize(ref writer, value.Item, options);");
        }

        sb.AppendLine("    }");
        sb.AppendLine();
        sb.Append("    public ").Append(model.StorageEnvelopeTypeName).Append(" Deserialize(ref global::MessagePack.MessagePackReader reader, global::MessagePack.MessagePackSerializerOptions options)")
            .AppendLine();
        sb.AppendLine("    {");
        sb.AppendLine("        if (reader.TryReadNil())");
        sb.AppendLine("        {");
        sb.AppendLine("            return null!;");
        sb.AppendLine("        }");
        sb.Append("        var value = new ").Append(model.StorageEnvelopeTypeName).AppendLine("();");
        sb.AppendLine("        var length = reader.ReadArrayHeader();");
        if (model.IsKeyed)
        {
            sb.AppendLine("        if (length > 0)");
            sb.AppendLine("        {");
            sb.Append("            value.Items = ResolveFormatter<global::System.Collections.Generic.List<").Append(model.DtoTypeName)
                .Append(">>(options).Deserialize(ref reader, options);").AppendLine();
            sb.AppendLine("        }");
            sb.AppendLine("        for (var index = 1; index < length; index++)");
        }
        else
        {
            sb.AppendLine("        if (length > 0)");
            sb.AppendLine("        {");
            sb.AppendLine("            value.HasValue = ResolveFormatter<bool>(options).Deserialize(ref reader, options);");
            sb.AppendLine("        }");
            sb.AppendLine("        if (length > 1)");
            sb.AppendLine("        {");
            sb.Append("            value.Item = ResolveFormatter<").Append(model.DtoTypeName).AppendLine("?>(options).Deserialize(ref reader, options);");
            sb.AppendLine("        }");
            sb.AppendLine("        for (var index = 2; index < length; index++)");
        }

        sb.AppendLine("        {");
        sb.AppendLine("            reader.Skip();");
        sb.AppendLine("        }");
        sb.AppendLine("        return value;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        AppendNamespaceEnd(sb, model.FormatterNamespace);
        return sb.ToString();
    }

    private static void AppendTrackerConstructor(StringBuilder sb, string typeName, string repositoryType)
    {
        sb.Append("    public ").Append(typeName).AppendLine("()");
        sb.AppendLine("    {");
        sb.AppendLine("#if UNITY_EDITOR");
        sb.Append("        global::Lilja.Repository.Diagnostics.RepositoryTracker.Track(this, global::Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType.")
            .Append(repositoryType).AppendLine(");");
        sb.AppendLine("#endif");
        sb.AppendLine("    }");
    }

    private static void AppendPersistedConstructor(StringBuilder sb, string typeName, string extension, string repositoryType, string storageIdentifier)
    {
        sb.Append("    public ").Append(typeName).AppendLine("()");
        sb.Append("        : base(global::System.IO.Path.Combine(global::UnityEngine.Application.persistentDataPath, \"").Append(storageIdentifier).Append('.')
            .Append(extension).AppendLine("\"))");
        sb.AppendLine("    {");
        sb.AppendLine("#if UNITY_EDITOR");
        sb.Append("        global::Lilja.Repository.Diagnostics.RepositoryTracker.Track(this, global::Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType.")
            .Append(repositoryType).AppendLine(");");
        sb.AppendLine("#endif");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void AppendMessagePackConstructor(StringBuilder sb, string typeName, EntityModel model)
    {
        sb.Append("    public ").Append(typeName).AppendLine("()");
        sb.Append("        : base(global::System.IO.Path.Combine(global::UnityEngine.Application.persistentDataPath, \"").Append(model.StorageIdentifier).AppendLine(".msgpack\"))");
        sb.AppendLine("    {");
        sb.AppendLine("        var resolver = global::MessagePack.Resolvers.CompositeResolver.Create(");
        sb.AppendLine("            new global::MessagePack.Formatters.IMessagePackFormatter[]");
        sb.AppendLine("            {");
        sb.Append("                new global::").Append(model.FormatterNamespace).Append('.').Append(model.StorageEnvelopeFormatterTypeNameWithoutNamespace).AppendLine("(),");
        sb.Append("                new global::").Append(model.FormatterNamespace).Append('.').Append(model.DtoFormatterTypeNameWithoutNamespace).AppendLine("(),");
        sb.AppendLine("            },");
        sb.AppendLine("            new global::MessagePack.IFormatterResolver[]");
        sb.AppendLine("            {");
        sb.AppendLine("                global::MessagePack.Resolvers.StandardResolver.Instance,");
        sb.AppendLine("            });");
        sb.AppendLine("        _options = global::MessagePack.MessagePackSerializerOptions.Standard.WithResolver(resolver);");
        sb.AppendLine("#if UNITY_EDITOR");
        sb.AppendLine("        global::Lilja.Repository.Diagnostics.RepositoryTracker.Track(this, global::Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType.MessagePack);");
        sb.AppendLine("#endif");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void AppendToDtoOverride(StringBuilder sb, EntityModel model)
    {
        sb.Append("    protected override ").Append(model.DtoTypeName).Append(" ToDto(").Append(model.EntityTypeName).AppendLine(" entity)");
        sb.AppendLine("    {");
        sb.Append("        return ").Append(model.EntityTypeName).AppendLine(".ToDto(entity);");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void AppendFromDtoOverride(StringBuilder sb, EntityModel model)
    {
        sb.Append("    protected override ").Append(model.EntityTypeName).Append(" FromDto(").Append(model.DtoTypeName).AppendLine(" dto)");
        sb.AppendLine("    {");
        sb.Append("        return ").Append(model.EntityTypeName).AppendLine(".FromDto(dto);");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void AppendJsonKeyedLoadSave(StringBuilder sb, EntityModel model)
    {
        sb.Append("    protected override global::Cysharp.Threading.Tasks.UniTask<global::System.Collections.Generic.IReadOnlyList<").Append(model.DtoTypeName)
            .AppendLine(">?> LoadItemsAsync(global::System.Threading.CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine("        return global::Cysharp.Threading.Tasks.UniTask.RunOnThreadPool(() =>");
        sb.AppendLine("        {");
        sb.AppendLine("            ct.ThrowIfCancellationRequested();");
        sb.AppendLine("            if (!global::System.IO.File.Exists(FilePath))");
        sb.AppendLine("            {");
        sb.AppendLine("                return null;");
        sb.AppendLine("            }");
        sb.AppendLine("            var raw = global::System.IO.File.ReadAllText(FilePath);");
        sb.AppendLine("            if (string.IsNullOrWhiteSpace(raw))");
        sb.AppendLine("            {");
        sb.AppendLine("                return null;");
        sb.AppendLine("            }");
        sb.Append("            var envelope = global::UnityEngine.JsonUtility.FromJson<").Append(model.StorageEnvelopeTypeName).AppendLine(">(raw);");
        sb.Append("            return (global::System.Collections.Generic.IReadOnlyList<").Append(model.DtoTypeName).AppendLine(">?)envelope?.Items;");
        sb.AppendLine("        }, cancellationToken: ct);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.Append("    protected override global::Cysharp.Threading.Tasks.UniTask SaveItemsAsync(global::System.Collections.Generic.IReadOnlyList<").Append(model.DtoTypeName)
            .AppendLine("> items, global::System.Threading.CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine("        return global::Cysharp.Threading.Tasks.UniTask.RunOnThreadPool(() =>");
        sb.AppendLine("        {");
        sb.AppendLine("            ct.ThrowIfCancellationRequested();");
        sb.Append("            var envelope = new ").Append(model.StorageEnvelopeTypeName).AppendLine();
        sb.AppendLine("            {");
        sb.Append("                Items = new global::System.Collections.Generic.List<").Append(model.DtoTypeName).AppendLine(">(items),");
        sb.AppendLine("            };");
        sb.AppendLine("            var json = global::UnityEngine.JsonUtility.ToJson(envelope, false);");
        sb.AppendLine("            global::Lilja.Repository.AtomicFileWriter.WriteAllText(FilePath, json);");
        sb.AppendLine("        }, cancellationToken: ct);");
        sb.AppendLine("    }");
    }

    private static void AppendJsonSingletonLoadSave(StringBuilder sb, EntityModel model)
    {
        sb.Append("    protected override global::Cysharp.Threading.Tasks.UniTask<").Append(model.DtoTypeName)
            .AppendLine("?> LoadValueAsync(global::System.Threading.CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine("        return global::Cysharp.Threading.Tasks.UniTask.RunOnThreadPool(() =>");
        sb.AppendLine("        {");
        sb.AppendLine("            ct.ThrowIfCancellationRequested();");
        sb.AppendLine("            if (!global::System.IO.File.Exists(FilePath))");
        sb.AppendLine("            {");
        sb.AppendLine("                return null;");
        sb.AppendLine("            }");
        sb.AppendLine("            var raw = global::System.IO.File.ReadAllText(FilePath);");
        sb.AppendLine("            if (string.IsNullOrWhiteSpace(raw))");
        sb.AppendLine("            {");
        sb.AppendLine("                return null;");
        sb.AppendLine("            }");
        sb.Append("            var envelope = global::UnityEngine.JsonUtility.FromJson<").Append(model.StorageEnvelopeTypeName).AppendLine(">(raw);");
        sb.AppendLine("            if (envelope is null || !envelope.HasValue)");
        sb.AppendLine("            {");
        sb.AppendLine("                return null;");
        sb.AppendLine("            }");
        sb.AppendLine("            return envelope.Item;");
        sb.AppendLine("        }, cancellationToken: ct);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.Append("    protected override global::Cysharp.Threading.Tasks.UniTask SaveValueAsync(").Append(model.DtoTypeName)
            .AppendLine("? value, global::System.Threading.CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine("        return global::Cysharp.Threading.Tasks.UniTask.RunOnThreadPool(() =>");
        sb.AppendLine("        {");
        sb.AppendLine("            ct.ThrowIfCancellationRequested();");
        sb.Append("            var envelope = new ").Append(model.StorageEnvelopeTypeName).AppendLine();
        sb.AppendLine("            {");
        sb.AppendLine("                HasValue = value is not null,");
        sb.AppendLine("                Item = value,");
        sb.AppendLine("            };");
        sb.AppendLine("            var json = global::UnityEngine.JsonUtility.ToJson(envelope, false);");
        sb.AppendLine("            global::Lilja.Repository.AtomicFileWriter.WriteAllText(FilePath, json);");
        sb.AppendLine("        }, cancellationToken: ct);");
        sb.AppendLine("    }");
    }

    private static void AppendMessagePackKeyedLoadSave(StringBuilder sb, EntityModel model)
    {
        sb.Append("    protected override global::Cysharp.Threading.Tasks.UniTask<global::System.Collections.Generic.IReadOnlyList<").Append(model.DtoTypeName)
            .AppendLine(">?> LoadItemsAsync(global::System.Threading.CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine("        return global::Cysharp.Threading.Tasks.UniTask.RunOnThreadPool(() =>");
        sb.AppendLine("        {");
        sb.AppendLine("            ct.ThrowIfCancellationRequested();");
        sb.AppendLine("            if (!global::System.IO.File.Exists(FilePath))");
        sb.AppendLine("            {");
        sb.AppendLine("                return null;");
        sb.AppendLine("            }");
        sb.AppendLine("            var bytes = global::System.IO.File.ReadAllBytes(FilePath);");
        sb.Append("            var envelope = global::MessagePack.MessagePackSerializer.Deserialize<").Append(model.StorageEnvelopeTypeName).AppendLine(">(bytes, _options);");
        sb.Append("            return (global::System.Collections.Generic.IReadOnlyList<").Append(model.DtoTypeName).AppendLine(">?)envelope?.Items;");
        sb.AppendLine("        }, cancellationToken: ct);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.Append("    protected override global::Cysharp.Threading.Tasks.UniTask SaveItemsAsync(global::System.Collections.Generic.IReadOnlyList<").Append(model.DtoTypeName)
            .AppendLine("> items, global::System.Threading.CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine("        return global::Cysharp.Threading.Tasks.UniTask.RunOnThreadPool(() =>");
        sb.AppendLine("        {");
        sb.AppendLine("            ct.ThrowIfCancellationRequested();");
        sb.Append("            var envelope = new ").Append(model.StorageEnvelopeTypeName).AppendLine();
        sb.AppendLine("            {");
        sb.Append("                Items = new global::System.Collections.Generic.List<").Append(model.DtoTypeName).AppendLine(">(items),");
        sb.AppendLine("            };");
        sb.AppendLine("            var bytes = global::MessagePack.MessagePackSerializer.Serialize(envelope, _options);");
        sb.AppendLine("            global::Lilja.Repository.AtomicFileWriter.WriteAllBytes(FilePath, bytes);");
        sb.AppendLine("        }, cancellationToken: ct);");
        sb.AppendLine("    }");
    }

    private static void AppendMessagePackSingletonLoadSave(StringBuilder sb, EntityModel model)
    {
        sb.Append("    protected override global::Cysharp.Threading.Tasks.UniTask<").Append(model.DtoTypeName)
            .AppendLine("?> LoadValueAsync(global::System.Threading.CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine("        return global::Cysharp.Threading.Tasks.UniTask.RunOnThreadPool(() =>");
        sb.AppendLine("        {");
        sb.AppendLine("            ct.ThrowIfCancellationRequested();");
        sb.AppendLine("            if (!global::System.IO.File.Exists(FilePath))");
        sb.AppendLine("            {");
        sb.AppendLine("                return null;");
        sb.AppendLine("            }");
        sb.AppendLine("            var bytes = global::System.IO.File.ReadAllBytes(FilePath);");
        sb.Append("            var envelope = global::MessagePack.MessagePackSerializer.Deserialize<").Append(model.StorageEnvelopeTypeName).AppendLine(">(bytes, _options);");
        sb.AppendLine("            if (envelope is null || !envelope.HasValue)");
        sb.AppendLine("            {");
        sb.AppendLine("                return null;");
        sb.AppendLine("            }");
        sb.AppendLine("            return envelope.Item;");
        sb.AppendLine("        }, cancellationToken: ct);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.Append("    protected override global::Cysharp.Threading.Tasks.UniTask SaveValueAsync(").Append(model.DtoTypeName)
            .AppendLine("? value, global::System.Threading.CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine("        return global::Cysharp.Threading.Tasks.UniTask.RunOnThreadPool(() =>");
        sb.AppendLine("        {");
        sb.AppendLine("            ct.ThrowIfCancellationRequested();");
        sb.Append("            var envelope = new ").Append(model.StorageEnvelopeTypeName).AppendLine();
        sb.AppendLine("            {");
        sb.AppendLine("                HasValue = value is not null,");
        sb.AppendLine("                Item = value,");
        sb.AppendLine("            };");
        sb.AppendLine("            var bytes = global::MessagePack.MessagePackSerializer.Serialize(envelope, _options);");
        sb.AppendLine("            global::Lilja.Repository.AtomicFileWriter.WriteAllBytes(FilePath, bytes);");
        sb.AppendLine("        }, cancellationToken: ct);");
        sb.AppendLine("    }");
    }

    private static string GetToDtoExpression(EntityModel model, MemberModel member, int memberIndex, int fieldIndex)
    {
        if (member.ValueObjectShape is null)
        {
            return "entity." + member.AccessibleName;
        }

        if (member.ValueObjectShape.PrimitiveParts.Length == 1)
        {
            return "entity." + member.AccessibleName + "." + member.ValueObjectShape.ToPrimitiveMethodName + "()";
        }

        return "primitive" + memberIndex + "." + member.DtoFields[fieldIndex].TupleAccessName;
    }

    private static string GetFromDtoArgumentExpression(MemberModel member)
    {
        if (member.ValueObjectShape is null)
        {
            return "dto." + member.DtoFields[0].Name;
        }

        if (member.ValueObjectShape.PrimitiveParts.Length == 1)
        {
            return CreateValueObjectExpression(member, new[] { "dto." + member.DtoFields[0].Name });
        }

        var arguments = member.DtoFields.Select(static field => "dto." + field.Name).ToArray();
        return CreateValueObjectExpression(member, arguments);
    }

    private static string CreateValueObjectExpression(MemberModel member, IReadOnlyList<string> argumentExpressions)
    {
        var arguments = string.Join(", ", argumentExpressions);
        if (member.ValueObjectShape!.CreationKind == ValueObjectCreationKind.StaticFactory)
        {
            return member.TypeName + "." + member.ValueObjectShape.CreationMemberName + "(" + arguments + ")";
        }

        return "new " + member.TypeName + "(" + arguments + ")";
    }

    private static string GetEntityKeyExpression(EntityModel model)
    {
        if (model.KeyMembers.Length == 1)
        {
            return "entity." + model.KeyMembers[0].AccessibleName;
        }

        return "(" + string.Join(", ", model.KeyMembers.Select(static member => "entity." + member.AccessibleName)) + ")";
    }

    private static string GetDtoKeyExpression(EntityModel model)
    {
        if (model.KeyMembers.Length == 1)
        {
            return GetFromDtoArgumentExpression(model.KeyMembers[0]);
        }

        return "(" + string.Join(", ", model.KeyMembers.Select(GetFromDtoArgumentExpression)) + ")";
    }

    private static StringBuilder CreateSourceBuilder()
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        return sb;
    }

    private static void AppendNamespaceStart(StringBuilder sb, string namespaceName)
    {
        if (string.IsNullOrEmpty(namespaceName))
        {
            return;
        }

        sb.Append("namespace ").Append(namespaceName).AppendLine();
        sb.AppendLine("{");
    }

    private static void AppendNamespaceEnd(StringBuilder sb, string namespaceName)
    {
        if (string.IsNullOrEmpty(namespaceName))
        {
            return;
        }

        sb.AppendLine("}");
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

    private sealed class EntityModel
    {
        public EntityModel(
            INamedTypeSymbol symbol,
            string namespaceName,
            string storageIdentifier,
            ImmutableArray<MemberModel> keyMembers,
            ImmutableArray<MemberModel> persistedMembers,
            bool needsGeneratedConstructor)
        {
            Symbol = symbol;
            NamespaceName = namespaceName;
            StorageIdentifier = storageIdentifier;
            KeyMembers = keyMembers;
            PersistedMembers = persistedMembers;
            NeedsGeneratedConstructor = needsGeneratedConstructor;
            EntityName = symbol.Name;
            EntityTypeName = GetTypeName(symbol);
            RepositoryNamespace = string.IsNullOrEmpty(namespaceName) ? "Repositories" : namespaceName + ".Repositories";
            DtoNamespace = string.IsNullOrEmpty(namespaceName) ? "Lilja.Repository.Generated.Dtos" : "Lilja.Repository.Generated.Dtos." + namespaceName;
            StorageNamespace = string.IsNullOrEmpty(namespaceName) ? "Lilja.Repository.Generated.Storage" : "Lilja.Repository.Generated.Storage." + namespaceName;
            FormatterNamespace = string.IsNullOrEmpty(namespaceName) ? "Lilja.Repository.Generated.Formatters" : "Lilja.Repository.Generated.Formatters." + namespaceName;
            DtoTypeNameWithoutNamespace = EntityName + "Dto";
            StorageEnvelopeTypeNameWithoutNamespace = EntityName + "StorageEnvelope";
            DtoFormatterTypeNameWithoutNamespace = EntityName + "DtoFormatter";
            StorageEnvelopeFormatterTypeNameWithoutNamespace = EntityName + "StorageEnvelopeFormatter";
            DtoTypeName = "global::" + DtoNamespace + "." + DtoTypeNameWithoutNamespace;
            StorageEnvelopeTypeName = "global::" + StorageNamespace + "." + StorageEnvelopeTypeNameWithoutNamespace;
            KeyTypeName = keyMembers.Length == 1
                ? keyMembers[0].TypeName
                : "(" + string.Join(", ", keyMembers.Select(static member => member.TypeName)) + ")";
            var dtoFieldBuilder = ImmutableArray.CreateBuilder<DtoFieldModel>();
            foreach (var member in persistedMembers)
            {
                dtoFieldBuilder.AddRange(member.DtoFields);
            }

            AllDtoFields = dtoFieldBuilder.ToImmutable();
        }

        public INamedTypeSymbol Symbol { get; }

        public string NamespaceName { get; }

        public string StorageIdentifier { get; }

        public string EntityName { get; }

        public string EntityTypeName { get; }

        public string RepositoryNamespace { get; }

        public string DtoNamespace { get; }

        public string StorageNamespace { get; }

        public string FormatterNamespace { get; }

        public string DtoTypeName { get; }

        public string DtoTypeNameWithoutNamespace { get; }

        public string StorageEnvelopeTypeName { get; }

        public string StorageEnvelopeTypeNameWithoutNamespace { get; }

        public string DtoFormatterTypeNameWithoutNamespace { get; }

        public string StorageEnvelopeFormatterTypeNameWithoutNamespace { get; }

        public string KeyTypeName { get; }

        public ImmutableArray<MemberModel> KeyMembers { get; }

        public ImmutableArray<MemberModel> PersistedMembers { get; }

        public ImmutableArray<DtoFieldModel> AllDtoFields { get; }

        public bool NeedsGeneratedConstructor { get; }

        public bool IsPersisted => PersistedMembers.Length > 0;

        public bool IsKeyed => KeyMembers.Length > 0;
    }

    private sealed class MemberModel
    {
        public MemberModel(
            string name,
            string accessibleName,
            ITypeSymbol typeSymbol,
            string typeName,
            bool isProperty,
            bool hasKey,
            bool hasPersist,
            int? persistIndex,
            ValueObjectShape? valueObjectShape,
            ImmutableArray<DtoFieldModel> dtoFields,
            Location location)
        {
            Name = name;
            AccessibleName = accessibleName;
            TypeSymbol = typeSymbol;
            TypeName = typeName;
            IsProperty = isProperty;
            HasKey = hasKey;
            HasPersist = hasPersist;
            PersistIndex = persistIndex;
            ValueObjectShape = valueObjectShape;
            DtoFields = dtoFields;
            Location = location;
        }

        public string Name { get; }

        public string AccessibleName { get; }

        public ITypeSymbol TypeSymbol { get; }

        public string TypeName { get; }

        public bool IsProperty { get; }

        public bool HasKey { get; }

        public bool HasPersist { get; }

        public int? PersistIndex { get; }

        public ValueObjectShape? ValueObjectShape { get; }

        public ImmutableArray<DtoFieldModel> DtoFields { get; }

        public Location Location { get; }
    }

    private sealed class ValueObjectShape
    {
        public ValueObjectShape(string toPrimitiveMethodName, ValueObjectCreationKind creationKind, string creationMemberName, ImmutableArray<PrimitivePartModel> primitiveParts)
        {
            ToPrimitiveMethodName = toPrimitiveMethodName;
            CreationKind = creationKind;
            CreationMemberName = creationMemberName;
            PrimitiveParts = primitiveParts;
        }

        public string ToPrimitiveMethodName { get; }

        public ValueObjectCreationKind CreationKind { get; }

        public string CreationMemberName { get; }

        public ImmutableArray<PrimitivePartModel> PrimitiveParts { get; }
    }

    private sealed class PrimitivePartModel
    {
        public PrimitivePartModel(ITypeSymbol typeSymbol, string typeName, string accessName, string dtoSuffixName)
        {
            TypeSymbol = typeSymbol;
            TypeName = typeName;
            AccessName = accessName;
            DtoSuffixName = dtoSuffixName;
        }

        public ITypeSymbol TypeSymbol { get; }

        public string TypeName { get; }

        public string AccessName { get; }

        public string DtoSuffixName { get; }
    }

    private sealed class DtoFieldModel
    {
        public DtoFieldModel(string name, string typeName, string tupleAccessName)
        {
            Name = name;
            TypeName = typeName;
            TupleAccessName = tupleAccessName;
        }

        public string Name { get; }

        public string TypeName { get; }

        public string TupleAccessName { get; }
    }

    private enum ValueObjectCreationKind
    {
        Constructor,
        StaticFactory,
    }
}
