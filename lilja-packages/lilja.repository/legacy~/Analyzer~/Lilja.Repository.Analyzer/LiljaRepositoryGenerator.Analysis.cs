using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Lilja.Repository.Analyzer;

public sealed partial class LiljaRepositoryGenerator
{
    /// <summary>
    /// 単一のエンティティ宣言を解析し、コード生成に必要なモデルを作成します。
    /// </summary>
    /// <param name="entitySymbol">解析するエンティティシンボル。</param>
    /// <returns>解析中に検出された診断を含む生成モデル。</returns>
    private static EntityAnalysis AnalyzeEntity(INamedTypeSymbol entitySymbol)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var entityLocation = GetPrimaryLocation(entitySymbol);

        ValidateEntity(entitySymbol, entityLocation, diagnostics);

        var keyMembers = new List<MemberModel>();
        var persistedMembers = new List<MemberModel>();

        foreach (var member in entitySymbol.GetMembers())
        {
            AnalyzeAnnotatedMember(member, diagnostics, keyMembers, persistedMembers);
        }

        ValidatePersistedMembers(keyMembers, persistedMembers, diagnostics);
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

    /// <summary>
    /// partial 宣言や未対応のジェネリック型引数など、エンティティレベルの制約を検証します。
    /// </summary>
    /// <param name="entitySymbol">解析対象のエンティティ。</param>
    /// <param name="entityLocation">診断に使う位置情報。</param>
    /// <param name="diagnostics">解析エラーを格納する診断先。</param>
    private static void ValidateEntity(
        INamedTypeSymbol entitySymbol,
        Location entityLocation,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        if (!IsPartial(entitySymbol))
        {
            diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.EntityMustBePartial, entityLocation));
        }

        if (entitySymbol.TypeParameters.Length > 0)
        {
            diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.GenericEntityNotSupported, entityLocation));
        }
    }

    /// <summary>
    /// メンバーに付いたリポジトリ注釈を調べ、対応しているメタデータを記録します。
    /// </summary>
    /// <param name="member">確認するメンバー。</param>
    /// <param name="diagnostics">解析エラーを格納する診断先。</param>
    /// <param name="keyMembers">収集されたキーメンバー。</param>
    /// <param name="persistedMembers">収集された永続化対象メンバー。</param>
    private static void AnalyzeAnnotatedMember(
        ISymbol member,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        List<MemberModel> keyMembers,
        List<MemberModel> persistedMembers)
    {
        var hasKey = HasAttribute(member, KeyAttributeMetadataName);
        var hasPersist = TryGetPersistIndex(member, out var persistIndex);
        if (!hasKey && !hasPersist)
        {
            return;
        }

        var memberLocation = GetPrimaryLocation(member);
        if (member.IsStatic)
        {
            diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.StaticMemberNotSupported, memberLocation));
            return;
        }

        var model = TryCreateSupportedMemberModel(member, hasKey, hasPersist, persistIndex, diagnostics, memberLocation);
        if (model is null)
        {
            return;
        }

        if (hasKey)
        {
            keyMembers.Add(model);
        }

        if (hasPersist)
        {
            persistedMembers.Add(model);
        }
    }

    /// <summary>
    /// 対応しているフィールドまたは自動実装プロパティを、ジェネレーター用のメンバーモデルへ変換します。
    /// </summary>
    /// <param name="member">モデル化するメンバー。</param>
    /// <param name="hasKey">メンバーが <c>[Key]</c> 属性を持つかどうか。</param>
    /// <param name="hasPersist">メンバーが <c>[Persist]</c> 属性を持つかどうか。</param>
    /// <param name="persistIndex">存在する場合の、宣言された永続化インデックス。</param>
    /// <param name="diagnostics">解析エラーを格納する診断先。</param>
    /// <param name="memberLocation">診断に使う位置情報。</param>
    /// <returns>メンバーモデル。メンバーが未対応の場合は <see langword="null"/>。</returns>
    private static MemberModel? TryCreateSupportedMemberModel(
        ISymbol member,
        bool hasKey,
        bool hasPersist,
        int? persistIndex,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        Location memberLocation)
    {
        return member switch
        {
            IFieldSymbol fieldSymbol when !fieldSymbol.IsImplicitlyDeclared
                => CreateMemberModel(fieldSymbol, hasKey, hasPersist, persistIndex, diagnostics),
            IFieldSymbol
                => null,
            IPropertySymbol propertySymbol when IsSupportedAutoProperty(propertySymbol)
                => CreateMemberModel(propertySymbol, hasKey, hasPersist, persistIndex, diagnostics),
            IPropertySymbol
                => ReportUnsupportedMember(memberLocation, diagnostics),
            _
                => ReportUnsupportedMember(memberLocation, diagnostics),
        };
    }

    /// <summary>
    /// メンバーが未対応であるため、生成されるリポジトリに参加できないことを報告します。
    /// </summary>
    /// <param name="memberLocation">診断に使う位置情報。</param>
    /// <param name="diagnostics">解析エラーを格納する診断先。</param>
    /// <returns>常に <see langword="null"/>。</returns>
    private static MemberModel? ReportUnsupportedMember(
        Location memberLocation,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.OnlyAutoPropertiesSupported, memberLocation));
        return null;
    }

    /// <summary>
    /// 注釈付きメンバーをすべて収集したあとで、メンバー横断の永続化ルールを検証します。
    /// </summary>
    /// <param name="keyMembers">収集されたキーメンバー。</param>
    /// <param name="persistedMembers">収集された永続化対象メンバー。</param>
    /// <param name="diagnostics">解析エラーを格納する診断先。</param>
    private static void ValidatePersistedMembers(
        IReadOnlyList<MemberModel> keyMembers,
        List<MemberModel> persistedMembers,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        foreach (var duplicateGroup in persistedMembers
                     .GroupBy(static member => member.PersistIndex!.Value)
                     .Where(static group => group.Count() > 1))
        {
            foreach (var duplicate in duplicateGroup)
            {
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.PersistIndexMustBeUnique, duplicate.Location));
            }
        }

        if (persistedMembers.Count == 0)
        {
            return;
        }

        foreach (var keyMember in keyMembers)
        {
            if (!keyMember.HasPersist)
            {
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.PersistedKeysMustAlsoBePersisted, keyMember.Location));
            }
        }
    }

    /// <summary>
    /// 値オブジェクト形状の情報も含めて、フィールドまたは自動実装プロパティ用のジェネレーターモデルを作成します。
    /// </summary>
    /// <param name="member">モデル化するメンバー。</param>
    /// <param name="hasKey">メンバーが <c>[Key]</c> 属性を持つかどうか。</param>
    /// <param name="hasPersist">メンバーが <c>[Persist]</c> 属性を持つかどうか。</param>
    /// <param name="persistIndex">存在する場合の、宣言された永続化インデックス。</param>
    /// <param name="diagnostics">解析エラーを格納する診断先。</param>
    /// <returns>内容が設定されたメンバーモデル。メンバーが未対応の場合は <see langword="null"/>。</returns>
    private static MemberModel? CreateMemberModel(
        ISymbol member,
        bool hasKey,
        bool hasPersist,
        int? persistIndex,
        ImmutableArray<Diagnostic>.Builder diagnostics)
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

    /// <summary>
    /// 永続化対象メンバーをプリミティブな DTO フィールドへ展開するための値オブジェクト変換メタデータを解析します。
    /// </summary>
    /// <param name="typeSymbol">確認対象のメンバー型。</param>
    /// <param name="location">診断に使う位置情報。</param>
    /// <param name="diagnostics">解析エラーを格納する診断先。</param>
    /// <returns>検出された値オブジェクト形状。型が直接永続化を使う場合は <see langword="null"/>。</returns>
    private static ValueObjectShape? AnalyzeValueObject(
        ITypeSymbol typeSymbol,
        Location location,
        ImmutableArray<Diagnostic>.Builder diagnostics)
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

    /// <summary>
    /// ファクトリまたはコンストラクタのシグネチャがプリミティブ DTO の形と一致するかを確認します。
    /// </summary>
    /// <param name="parameters">比較する引数一覧。</param>
    /// <param name="primitiveParts"><c>[ToPrimitive]</c> によって生成されたプリミティブ要素。</param>
    /// <returns>シグネチャが一致する場合は <see langword="true"/>、それ以外は <see langword="false"/>。</returns>
    private static bool ParametersMatchPrimitiveParts(
        ImmutableArray<IParameterSymbol> parameters,
        ImmutableArray<PrimitivePartModel> primitiveParts)
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

    /// <summary>
    /// プリミティブ表現を DTO フィールドの構成要素へ分解し、タプルは個別の要素へ展開します。
    /// </summary>
    /// <param name="typeSymbol"><c>[ToPrimitive]</c> が公開するプリミティブ戻り値型。</param>
    /// <returns>シリアライズに使うプリミティブ DTO 要素。</returns>
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

    private static ImmutableArray<DtoFieldModel> BuildDtoFields(
        string memberName,
        ITypeSymbol memberType,
        ValueObjectShape? valueObjectShape)
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
}
