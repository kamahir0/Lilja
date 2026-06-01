using System.Linq;
using Microsoft.CodeAnalysis;

namespace Lilja.Repository.Analyzer;

internal static class MessagePackContract
{
    private const string SerializerMetadataName = "MessagePack.MessagePackSerializer";
    private const string SerializerOptionsMetadataName = "MessagePack.MessagePackSerializerOptions";
    private const string CompositeResolverMetadataName = "MessagePack.Resolvers.CompositeResolver";
    private const string StandardResolverMetadataName = "MessagePack.Resolvers.StandardResolver";
    private const string ResolverMetadataName = "MessagePack.IFormatterResolver";
    private const string FormatterMetadataName = "MessagePack.Formatters.IMessagePackFormatter";
    private const string GenericFormatterMetadataName = "MessagePack.Formatters.IMessagePackFormatter`1";
    private const string WriterMetadataName = "MessagePack.MessagePackWriter";
    private const string ReaderMetadataName = "MessagePack.MessagePackReader";
    private const string SerializationExceptionMetadataName = "MessagePack.MessagePackSerializationException";

    public static bool HasCompatibleContract(Compilation compilation)
    {
        var serializerType = compilation.GetTypeByMetadataName(SerializerMetadataName);
        var serializerOptionsType = compilation.GetTypeByMetadataName(SerializerOptionsMetadataName);
        var compositeResolverType = compilation.GetTypeByMetadataName(CompositeResolverMetadataName);
        var standardResolverType = compilation.GetTypeByMetadataName(StandardResolverMetadataName);
        var resolverType = compilation.GetTypeByMetadataName(ResolverMetadataName);
        var formatterType = compilation.GetTypeByMetadataName(FormatterMetadataName);
        var genericFormatterType = compilation.GetTypeByMetadataName(GenericFormatterMetadataName);
        var writerType = compilation.GetTypeByMetadataName(WriterMetadataName);
        var readerType = compilation.GetTypeByMetadataName(ReaderMetadataName);
        var serializationExceptionType = compilation.GetTypeByMetadataName(SerializationExceptionMetadataName);

        return serializerType is not null &&
               serializerOptionsType is not null &&
               compositeResolverType is not null &&
               standardResolverType is not null &&
               resolverType is not null &&
               formatterType is not null &&
               genericFormatterType is not null &&
               writerType is not null &&
               readerType is not null &&
               serializationExceptionType is not null &&
               HasStandardOptions(serializerOptionsType) &&
               HasResolverProperty(serializerOptionsType, resolverType) &&
               HasWithResolver(serializerOptionsType, resolverType) &&
               HasStandardResolverInstance(standardResolverType) &&
               HasResolverGetFormatter(resolverType) &&
               HasCompositeResolverCreate(compositeResolverType, formatterType, resolverType, compilation) &&
               HasSerialize(serializerType, serializerOptionsType) &&
               HasDeserialize(serializerType, serializerOptionsType, compilation) &&
               HasWriterContract(writerType, compilation) &&
               HasReaderContract(readerType, compilation) &&
               HasSerializationExceptionContract(serializationExceptionType, compilation);
    }

    private static bool HasStandardOptions(INamedTypeSymbol serializerOptionsType)
    {
        return serializerOptionsType.GetMembers("Standard")
            .OfType<IPropertySymbol>()
            .Any(member => member.IsStatic && SymbolEqualityComparer.Default.Equals(member.Type, serializerOptionsType));
    }

    private static bool HasResolverProperty(INamedTypeSymbol serializerOptionsType, INamedTypeSymbol resolverType)
    {
        return serializerOptionsType.GetMembers("Resolver")
            .OfType<IPropertySymbol>()
            .Any(member => !member.IsStatic && SymbolEqualityComparer.Default.Equals(member.Type, resolverType));
    }

    private static bool HasWithResolver(INamedTypeSymbol serializerOptionsType, INamedTypeSymbol resolverType)
    {
        return serializerOptionsType.GetMembers("WithResolver")
            .OfType<IMethodSymbol>()
            .Any(method =>
                !method.IsStatic &&
                method.Parameters.Length == 1 &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, resolverType) &&
                SymbolEqualityComparer.Default.Equals(method.ReturnType, serializerOptionsType));
    }

    private static bool HasStandardResolverInstance(INamedTypeSymbol standardResolverType)
    {
        return standardResolverType.GetMembers("Instance").Any(member => member switch
        {
            IFieldSymbol field => field.IsStatic && SymbolEqualityComparer.Default.Equals(field.Type, standardResolverType),
            IPropertySymbol property => property.IsStatic && SymbolEqualityComparer.Default.Equals(property.Type, standardResolverType),
            _ => false,
        });
    }

    private static bool HasResolverGetFormatter(INamedTypeSymbol resolverType)
    {
        return resolverType.GetMembers("GetFormatter")
            .OfType<IMethodSymbol>()
            .Any(method =>
                method.TypeParameters.Length == 1 &&
                method.Parameters.Length == 0);
    }

    private static bool HasCompositeResolverCreate(
        INamedTypeSymbol compositeResolverType,
        INamedTypeSymbol formatterType,
        INamedTypeSymbol resolverType,
        Compilation compilation)
    {
        return compositeResolverType.GetMembers("Create")
            .OfType<IMethodSymbol>()
            .Any(method =>
            {
                if (!method.IsStatic || method.Parameters.Length != 2 || !SymbolEqualityComparer.Default.Equals(method.ReturnType, resolverType))
                {
                    return false;
                }

                return AcceptsCollectionOf(method.Parameters[0].Type, formatterType, compilation) &&
                       AcceptsCollectionOf(method.Parameters[1].Type, resolverType, compilation);
            });
    }

    private static bool HasSerialize(INamedTypeSymbol serializerType, INamedTypeSymbol serializerOptionsType)
    {
        return serializerType.GetMembers("Serialize")
            .OfType<IMethodSymbol>()
            .Any(method =>
            {
                if (!method.IsStatic || method.TypeParameters.Length != 1 || method.Parameters.Length < 2)
                {
                    return false;
                }

                return method.Parameters[0].Type is ITypeParameterSymbol typeParameter &&
                       SymbolEqualityComparer.Default.Equals(typeParameter, method.TypeParameters[0]) &&
                       SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, serializerOptionsType);
            });
    }

    private static bool HasDeserialize(INamedTypeSymbol serializerType, INamedTypeSymbol serializerOptionsType, Compilation compilation)
    {
        return serializerType.GetMembers("Deserialize")
            .OfType<IMethodSymbol>()
            .Any(method =>
            {
                if (!method.IsStatic || method.TypeParameters.Length != 1 || method.Parameters.Length < 2)
                {
                    return false;
                }

                return AcceptsSerializedBytes(method.Parameters[0].Type, compilation) &&
                       SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, serializerOptionsType);
            });
    }

    private static bool HasWriterContract(INamedTypeSymbol writerType, Compilation compilation)
    {
        var intType = compilation.GetSpecialType(SpecialType.System_Int32);
        return writerType.GetMembers("WriteNil")
                   .OfType<IMethodSymbol>()
                   .Any(method => method.Parameters.Length == 0) &&
               writerType.GetMembers("WriteArrayHeader")
                   .OfType<IMethodSymbol>()
                   .Any(method => method.Parameters.Length == 1 &&
                                  SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, intType));
    }

    private static bool HasReaderContract(INamedTypeSymbol readerType, Compilation compilation)
    {
        var intType = compilation.GetSpecialType(SpecialType.System_Int32);
        var boolType = compilation.GetSpecialType(SpecialType.System_Boolean);
        return readerType.GetMembers("TryReadNil")
                   .OfType<IMethodSymbol>()
                   .Any(method => method.Parameters.Length == 0 &&
                                  SymbolEqualityComparer.Default.Equals(method.ReturnType, boolType)) &&
               readerType.GetMembers("ReadArrayHeader")
                   .OfType<IMethodSymbol>()
                   .Any(method => method.Parameters.Length == 0 &&
                                  SymbolEqualityComparer.Default.Equals(method.ReturnType, intType)) &&
               readerType.GetMembers("Skip")
                   .OfType<IMethodSymbol>()
                   .Any(method => method.Parameters.Length == 0);
    }

    private static bool HasSerializationExceptionContract(INamedTypeSymbol serializationExceptionType, Compilation compilation)
    {
        var stringType = compilation.GetSpecialType(SpecialType.System_String);
        return serializationExceptionType.InstanceConstructors.Any(constructor =>
            constructor.Parameters.Length == 1 &&
            SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type, stringType));
    }

    private static bool AcceptsCollectionOf(ITypeSymbol candidateType, ITypeSymbol elementType, Compilation compilation)
    {
        if (candidateType is IArrayTypeSymbol arrayType)
        {
            return SymbolEqualityComparer.Default.Equals(arrayType.ElementType, elementType);
        }

        if (candidateType is not INamedTypeSymbol namedType || !namedType.IsGenericType)
        {
            return false;
        }

        var constructed = compilation.GetTypeByMetadataName("System.Collections.Generic.IReadOnlyList`1")?.Construct(elementType);
        if (constructed is not null && SymbolEqualityComparer.Default.Equals(candidateType, constructed))
        {
            return true;
        }

        var enumerable = compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1")?.Construct(elementType);
        return enumerable is not null && SymbolEqualityComparer.Default.Equals(candidateType, enumerable);
    }

    private static bool AcceptsSerializedBytes(ITypeSymbol candidateType, Compilation compilation)
    {
        if (candidateType is IArrayTypeSymbol arrayType &&
            arrayType.ElementType.SpecialType == SpecialType.System_Byte)
        {
            return true;
        }

        if (candidateType is not INamedTypeSymbol namedType || !namedType.IsGenericType)
        {
            return false;
        }

        var byteType = compilation.GetSpecialType(SpecialType.System_Byte);
        var readOnlyMemory = compilation.GetTypeByMetadataName("System.ReadOnlyMemory`1")?.Construct(byteType);
        return readOnlyMemory is not null && SymbolEqualityComparer.Default.Equals(namedType, readOnlyMemory);
    }
}
