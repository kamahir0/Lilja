using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Lilja.Repository.Analyzer.Models;

namespace Lilja.Repository.Analyzer.Analysis;

/// <summary>
/// Entity解析ロジック。
/// </summary>
internal static class EntityAnalyzer
{
    private const string KeyAttributeFullName = "Lilja.Repository.KeyAttribute";
    private const string PersistAttributeFullName = "Lilja.Repository.PersistAttribute";
    private const string ToPrimitiveAttributeFullName = "Lilja.Repository.ToPrimitiveAttribute";
    private const string FromPrimitiveAttributeFullName = "Lilja.Repository.FromPrimitiveAttribute";

    private static readonly SymbolDisplayFormat FullyQualifiedNullableFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public static EntityAnalysisResult Analyze(INamedTypeSymbol classSymbol)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        if (!IsPartial(classSymbol))
        {
            diagnostics.Add(Diagnostic.Create(
                RepositoryDiagnostics.EntityMustBePartial,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name));
        }

        if (classSymbol.IsGenericType)
        {
            diagnostics.Add(Diagnostic.Create(
                RepositoryDiagnostics.GenericEntityIsNotSupported,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name));
        }

        var persistMembers = new List<EntityMemberInfo>();
        var keyMembers = new List<EntityMemberInfo>();
        var persistIndexes = new Dictionary<int, string>();

        foreach (var member in classSymbol.GetMembers())
        {
            switch (member)
            {
                case IFieldSymbol fieldSymbol when !fieldSymbol.IsImplicitlyDeclared:
                    AnalyzeField(fieldSymbol, persistMembers, keyMembers, persistIndexes, diagnostics);
                    break;
                case IPropertySymbol propertySymbol when !propertySymbol.IsImplicitlyDeclared:
                    AnalyzeProperty(propertySymbol, persistMembers, keyMembers, persistIndexes, diagnostics);
                    break;
            }
        }

        persistMembers.Sort((left, right) => left.Index.CompareTo(right.Index));

        if (persistMembers.Count > 0)
        {
            foreach (var keyMember in keyMembers)
            {
                if (!keyMember.IsPersisted)
                {
                    diagnostics.Add(Diagnostic.Create(
                        RepositoryDiagnostics.PersistedEntityKeyMustAlsoPersist,
                        GetMemberLocation(classSymbol, keyMember.Name),
                        keyMember.Name));
                }
            }
        }

        if (diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
        {
            return new EntityAnalysisResult(null, diagnostics.ToImmutable());
        }

        var ns = classSymbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : classSymbol.ContainingNamespace.ToDisplayString();
        var fullTypeName = classSymbol.ToDisplayString(FullyQualifiedNullableFormat);
        var needsConstructorGeneration =
            persistMembers.Count > 0 && !HasMatchingConstructor(classSymbol, persistMembers);

        var entity = new EntityInfo(
            ns,
            classSymbol.Name,
            fullTypeName,
            persistMembers,
            keyMembers,
            needsConstructorGeneration);

        return new EntityAnalysisResult(entity, diagnostics.ToImmutable());
    }

    private static void AnalyzeField(
        IFieldSymbol fieldSymbol,
        List<EntityMemberInfo> persistMembers,
        List<EntityMemberInfo> keyMembers,
        Dictionary<int, string> persistIndexes,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var hasKey = HasAttribute(fieldSymbol, KeyAttributeFullName);
        var persistAttribute = GetAttribute(fieldSymbol, PersistAttributeFullName);
        if (!hasKey && persistAttribute == null)
        {
            return;
        }

        if (fieldSymbol.IsStatic)
        {
            diagnostics.Add(Diagnostic.Create(
                RepositoryDiagnostics.StaticAnnotatedMember,
                fieldSymbol.Locations.FirstOrDefault(),
                fieldSymbol.Name));
            return;
        }

        AnalyzeMemberCore(
            fieldSymbol,
            fieldSymbol.Type,
            fieldSymbol.Name,
            EntityMemberKind.Field,
            hasKey,
            persistAttribute,
            persistMembers,
            keyMembers,
            persistIndexes,
            diagnostics);
    }

    private static void AnalyzeProperty(
        IPropertySymbol propertySymbol,
        List<EntityMemberInfo> persistMembers,
        List<EntityMemberInfo> keyMembers,
        Dictionary<int, string> persistIndexes,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var hasKey = HasAttribute(propertySymbol, KeyAttributeFullName);
        var persistAttribute = GetAttribute(propertySymbol, PersistAttributeFullName);
        if (!hasKey && persistAttribute == null)
        {
            return;
        }

        if (propertySymbol.IsStatic)
        {
            diagnostics.Add(Diagnostic.Create(
                RepositoryDiagnostics.StaticAnnotatedMember,
                propertySymbol.Locations.FirstOrDefault(),
                propertySymbol.Name));
            return;
        }

        if (!IsSupportedAutoProperty(propertySymbol))
        {
            diagnostics.Add(Diagnostic.Create(
                RepositoryDiagnostics.PropertyMustBeAutoProperty,
                propertySymbol.Locations.FirstOrDefault(),
                propertySymbol.Name));
            return;
        }

        AnalyzeMemberCore(
            propertySymbol,
            propertySymbol.Type,
            propertySymbol.Name,
            EntityMemberKind.Property,
            hasKey,
            persistAttribute,
            persistMembers,
            keyMembers,
            persistIndexes,
            diagnostics);
    }

    private static void AnalyzeMemberCore(
        ISymbol symbol,
        ITypeSymbol typeSymbol,
        string name,
        EntityMemberKind kind,
        bool hasKey,
        AttributeData? persistAttribute,
        List<EntityMemberInfo> persistMembers,
        List<EntityMemberInfo> keyMembers,
        Dictionary<int, string> persistIndexes,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var isPersisted = persistAttribute != null;
        var index = -1;
        if (persistAttribute != null &&
            persistAttribute.ConstructorArguments.Length > 0 &&
            persistAttribute.ConstructorArguments[0].Value is int parsedIndex)
        {
            index = parsedIndex;
        }

        if (isPersisted)
        {
            if (persistIndexes.TryGetValue(index, out var existingMemberName))
            {
                diagnostics.Add(Diagnostic.Create(
                    RepositoryDiagnostics.DuplicatePersistIndex,
                    symbol.Locations.FirstOrDefault(),
                    index,
                    symbol.ContainingType.Name));
            }
            else
            {
                persistIndexes.Add(index, name);
            }
        }

        var valueObjectInfo = AnalyzeValueObject(typeSymbol, symbol.Locations.FirstOrDefault(), diagnostics);
        var memberInfo = new EntityMemberInfo(
            name,
            typeSymbol.ToDisplayString(FullyQualifiedNullableFormat),
            index,
            hasKey,
            isPersisted,
            kind,
            valueObjectInfo);

        if (isPersisted)
        {
            persistMembers.Add(memberInfo);
        }

        if (hasKey)
        {
            keyMembers.Add(memberInfo);
        }
    }

    private static bool HasMatchingConstructor(
        INamedTypeSymbol classSymbol,
        IReadOnlyList<EntityMemberInfo> persistMembers)
    {
        foreach (var constructor in classSymbol.Constructors)
        {
            if (constructor.IsImplicitlyDeclared || constructor.IsStatic)
            {
                continue;
            }

            if (constructor.Parameters.Length != persistMembers.Count)
            {
                continue;
            }

            var allMatch = true;
            for (var i = 0; i < persistMembers.Count; i++)
            {
                var parameterType = constructor.Parameters[i].Type.ToDisplayString(FullyQualifiedNullableFormat);
                if (parameterType != persistMembers[i].TypeName)
                {
                    allMatch = false;
                    break;
                }
            }

            if (allMatch)
            {
                return true;
            }
        }

        return false;
    }

    private static ValueObjectInfo AnalyzeValueObject(
        ITypeSymbol typeSymbol,
        Location? diagnosticLocation,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var toPrimitiveCandidates = typeSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(method => HasAttribute(method, ToPrimitiveAttributeFullName))
            .ToArray();

        if (toPrimitiveCandidates.Length == 0)
        {
            return ValueObjectInfo.None;
        }

        if (toPrimitiveCandidates.Length != 1 ||
            toPrimitiveCandidates[0].IsStatic ||
            toPrimitiveCandidates[0].Parameters.Length != 0)
        {
            diagnostics.Add(Diagnostic.Create(
                RepositoryDiagnostics.InvalidToPrimitive,
                diagnosticLocation,
                typeSymbol.Name));
            return ValueObjectInfo.None;
        }

        var toPrimitiveMethod = toPrimitiveCandidates[0];
        var tupleElements = GetTupleElements(toPrimitiveMethod.ReturnType);

        var fromPrimitiveMethods = typeSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(method => method.MethodKind != MethodKind.Constructor &&
                             HasAttribute(method, FromPrimitiveAttributeFullName))
            .ToArray();
        var fromPrimitiveConstructors = ((INamedTypeSymbol)typeSymbol).Constructors
            .Where(constructor => HasAttribute(constructor, FromPrimitiveAttributeFullName))
            .ToArray();

        var hasSingleStaticFactory =
            fromPrimitiveMethods.Length == 1 &&
            fromPrimitiveConstructors.Length == 0 &&
            fromPrimitiveMethods[0].IsStatic &&
            SymbolEqualityComparer.Default.Equals(fromPrimitiveMethods[0].ReturnType, typeSymbol) &&
            ParametersMatchTupleElements(fromPrimitiveMethods[0].Parameters, tupleElements);

        if (hasSingleStaticFactory)
        {
            return new ValueObjectInfo(
                true,
                toPrimitiveMethod.Name,
                fromPrimitiveMethods[0].Name,
                true,
                tupleElements);
        }

        var hasSingleConstructor =
            fromPrimitiveMethods.Length == 0 &&
            fromPrimitiveConstructors.Length == 1 &&
            ParametersMatchTupleElements(fromPrimitiveConstructors[0].Parameters, tupleElements);

        if (hasSingleConstructor)
        {
            return new ValueObjectInfo(
                true,
                toPrimitiveMethod.Name,
                string.Empty,
                false,
                tupleElements);
        }

        diagnostics.Add(Diagnostic.Create(
            RepositoryDiagnostics.InvalidFromPrimitive,
            diagnosticLocation,
            typeSymbol.Name));
        return ValueObjectInfo.None;
    }

    private static IReadOnlyList<TupleElementInfo> GetTupleElements(ITypeSymbol returnType)
    {
        if (returnType is INamedTypeSymbol namedType && namedType.IsTupleType)
        {
            var elements = new List<TupleElementInfo>(namedType.TupleElements.Length);
            for (var i = 0; i < namedType.TupleElements.Length; i++)
            {
                var element = namedType.TupleElements[i];
                var name = string.IsNullOrWhiteSpace(element.Name) ? $"Item{i + 1}" : element.Name;
                elements.Add(new TupleElementInfo(
                    element.Type.ToDisplayString(FullyQualifiedNullableFormat),
                    name));
            }

            return elements;
        }

        return new[]
        {
            new TupleElementInfo(returnType.ToDisplayString(FullyQualifiedNullableFormat), "Value"),
        };
    }

    private static bool ParametersMatchTupleElements(
        ImmutableArray<IParameterSymbol> parameters,
        IReadOnlyList<TupleElementInfo> tupleElements)
    {
        if (parameters.Length != tupleElements.Count)
        {
            return false;
        }

        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].Type.ToDisplayString(FullyQualifiedNullableFormat) != tupleElements[i].TypeName)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSupportedAutoProperty(IPropertySymbol propertySymbol)
    {
        if (propertySymbol.IsIndexer)
        {
            return false;
        }

        foreach (var syntaxReference in propertySymbol.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is not PropertyDeclarationSyntax declaration)
            {
                return false;
            }

            if (declaration.ExpressionBody != null || declaration.AccessorList == null)
            {
                return false;
            }

            foreach (var accessor in declaration.AccessorList.Accessors)
            {
                if (accessor.Body != null || accessor.ExpressionBody != null)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsPartial(INamedTypeSymbol classSymbol)
    {
        foreach (var syntaxReference in classSymbol.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is ClassDeclarationSyntax declaration &&
                declaration.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PartialKeyword)))
            {
                return true;
            }
        }

        return false;
    }

    private static Location? GetMemberLocation(INamedTypeSymbol classSymbol, string memberName)
    {
        foreach (var member in classSymbol.GetMembers())
        {
            if (member.Name == memberName)
            {
                return member.Locations.FirstOrDefault();
            }
        }

        return classSymbol.Locations.FirstOrDefault();
    }

    private static bool HasAttribute(ISymbol symbol, string attributeFullName)
    {
        return GetAttribute(symbol, attributeFullName) != null;
    }

    private static AttributeData? GetAttribute(ISymbol symbol, string attributeFullName)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == attributeFullName)
            {
                return attribute;
            }
        }

        return null;
    }
}
