using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Lilja.Repository.Analyzer.Models;

namespace Lilja.Repository.Analyzer.Analysis;

/// <summary>
/// Entity解析ロジック。
/// </summary>
internal static class EntityAnalyzer
{
    private const string EntityAttributeFullName = "Lilja.Repository.EntityAttribute";
    private const string KeyAttributeFullName = "Lilja.Repository.KeyAttribute";
    private const string PersistAttributeFullName = "Lilja.Repository.PersistAttribute";
    private const string ToPrimitiveAttributeFullName = "Lilja.Repository.ToPrimitiveAttribute";
    private const string FromPrimitiveAttributeFullName = "Lilja.Repository.FromPrimitiveAttribute";

    /// <summary>
    /// シンボルからEntity情報を解析する。
    /// </summary>
    public static EntityInfo? Analyze(INamedTypeSymbol classSymbol, Compilation compilation)
    {
        // Entity属性チェック
        if (!HasAttribute(classSymbol, EntityAttributeFullName))
        {
            return null;
        }

        var fields = new List<Models.FieldInfo>();

        foreach (var member in classSymbol.GetMembers())
        {
            if (member is not IFieldSymbol fieldSymbol)
            {
                continue;
            }

            // Persist属性を持つフィールドのみ対象
            var persistAttr = GetAttribute(fieldSymbol, PersistAttributeFullName);
            if (persistAttr == null)
            {
                continue;
            }

            // インデックス取得
            var index = 0;
            if (persistAttr.ConstructorArguments.Length > 0 &&
                persistAttr.ConstructorArguments[0].Value is int idx)
            {
                index = idx;
            }

            // Key属性チェック
            var isKey = HasAttribute(fieldSymbol, KeyAttributeFullName);

            // ValueObject検出
            var valueObjectInfo = AnalyzeValueObject(fieldSymbol.Type);

            var fieldInfo = new Models.FieldInfo(
                fieldSymbol.Name,
                GetPrimitiveTypeName(fieldSymbol.Type),
                fieldSymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                index,
                isKey,
                valueObjectInfo
            );

            fields.Add(fieldInfo);
        }

        if (fields.Count == 0)
        {
            return null;
        }

        // インデックス順にソート
        fields.Sort((a, b) => a.Index.CompareTo(b.Index));

        // キーフィールドを抽出（インデックス順）
        var keyFields = new List<Models.FieldInfo>();
        foreach (var field in fields)
        {
            if (field.IsKey)
            {
                keyFields.Add(field);
            }
        }

        var ns = classSymbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : classSymbol.ContainingNamespace.ToDisplayString();

        // 既存コンストラクタの存在チェック
        var needsConstructorGeneration = !HasMatchingConstructor(classSymbol, fields);

        return new EntityInfo(ns, classSymbol.Name, fields, keyFields, needsConstructorGeneration);
    }

    /// <summary>
    /// Persist属性フィールドと同じシグネチャのコンストラクタが存在するかチェック。
    /// </summary>
    private static bool HasMatchingConstructor(INamedTypeSymbol classSymbol, List<Models.FieldInfo> fields)
    {
        foreach (var ctor in classSymbol.Constructors)
        {
            if (ctor.IsImplicitlyDeclared || ctor.IsStatic)
            {
                continue;
            }

            var parameters = ctor.Parameters;
            if (parameters.Length != fields.Count)
            {
                continue;
            }

            var match = true;
            for (int i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                var param = parameters[i];

                // 型を比較（フル修飾名で比較）
                var paramTypeName = param.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (paramTypeName != field.FullTypeName)
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return true;
            }
        }
        return false;
    }

    private static ValueObjectInfo AnalyzeValueObject(ITypeSymbol typeSymbol)
    {
        // ToPrimitive属性を持つメソッドを探す
        string toPrimitiveMethodName = null;
        var tupleElements = new List<TupleElementInfo>();

        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is not IMethodSymbol methodSymbol)
            {
                continue;
            }

            var toPrimitiveAttr = GetAttribute(methodSymbol, ToPrimitiveAttributeFullName);
            if (toPrimitiveAttr == null)
            {
                continue;
            }

            toPrimitiveMethodName = methodSymbol.Name;

            // 戻り値の型を解析
            var returnType = methodSymbol.ReturnType;

            if (returnType is INamedTypeSymbol namedType && namedType.IsTupleType)
            {
                // タプル型
                foreach (var element in namedType.TupleElements)
                {
                    tupleElements.Add(new TupleElementInfo(
                        GetPrimitiveTypeName(element.Type),
                        element.Name
                    ));
                }
            }
            else
            {
                // 単一プリミティブ型
                tupleElements.Add(new TupleElementInfo(
                    GetPrimitiveTypeName(returnType),
                    "Value"
                ));
            }
            break;
        }

        if (toPrimitiveMethodName == null)
        {
            return ValueObjectInfo.None;
        }

        // FromPrimitive属性を持つstaticメソッドまたはコンストラクタを探す
        string fromPrimitiveMethodName = null;
        var isFromPrimitiveStatic = false;

        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is IMethodSymbol methodSymbol)
            {
                var fromPrimitiveAttr = GetAttribute(methodSymbol, FromPrimitiveAttributeFullName);
                if (fromPrimitiveAttr != null && methodSymbol.IsStatic)
                {
                    fromPrimitiveMethodName = methodSymbol.Name;
                    isFromPrimitiveStatic = true;
                    break;
                }
            }
        }

        // staticメソッドがなければコンストラクタを探す
        if (fromPrimitiveMethodName == null)
        {
            foreach (var constructor in ((INamedTypeSymbol)typeSymbol).Constructors)
            {
                var fromPrimitiveAttr = GetAttribute(constructor, FromPrimitiveAttributeFullName);
                if (fromPrimitiveAttr != null)
                {
                    // コンストラクタ使用（メソッド名は空）
                    fromPrimitiveMethodName = string.Empty;
                    isFromPrimitiveStatic = false;
                    break;
                }
            }
        }

        return new ValueObjectInfo(true, toPrimitiveMethodName, fromPrimitiveMethodName ?? string.Empty, isFromPrimitiveStatic, tupleElements);
    }

    private static string GetPrimitiveTypeName(ITypeSymbol typeSymbol)
    {
        return typeSymbol.SpecialType switch
        {
            SpecialType.System_Boolean => "bool",
            SpecialType.System_Byte => "byte",
            SpecialType.System_SByte => "sbyte",
            SpecialType.System_Int16 => "short",
            SpecialType.System_UInt16 => "ushort",
            SpecialType.System_Int32 => "int",
            SpecialType.System_UInt32 => "uint",
            SpecialType.System_Int64 => "long",
            SpecialType.System_UInt64 => "ulong",
            SpecialType.System_Single => "float",
            SpecialType.System_Double => "double",
            SpecialType.System_String => "string",
            _ => typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
        };
    }

    private static bool HasAttribute(ISymbol symbol, string attributeFullName)
    {
        return GetAttribute(symbol, attributeFullName) != null;
    }

    private static AttributeData? GetAttribute(ISymbol symbol, string attributeFullName)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            var attrClass = attr.AttributeClass;
            if (attrClass == null) continue;

            var fullName = attrClass.ToDisplayString();
            if (fullName == attributeFullName)
            {
                return attr;
            }
        }
        return null;
    }
}
