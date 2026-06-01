#nullable enable
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Lilja.Repository.Editor
{
    internal sealed class MessagePackCompatibilityProbe
    {
        private readonly Type _serializerOptionsType;
        private readonly Type _formatterType;
        private readonly Type _resolverType;
        private readonly MethodInfo _deserializeMethod;
        private readonly MethodInfo _createResolverMethod;
        private readonly MethodInfo _withResolverMethod;
        private readonly MemberInfo _standardOptionsMember;
        private readonly MemberInfo _standardResolverMember;

        private MessagePackCompatibilityProbe(
            Type serializerOptionsType,
            Type formatterType,
            Type resolverType,
            MethodInfo deserializeMethod,
            MethodInfo createResolverMethod,
            MethodInfo withResolverMethod,
            MemberInfo standardOptionsMember,
            MemberInfo standardResolverMember)
        {
            _serializerOptionsType = serializerOptionsType;
            _formatterType = formatterType;
            _resolverType = resolverType;
            _deserializeMethod = deserializeMethod;
            _createResolverMethod = createResolverMethod;
            _withResolverMethod = withResolverMethod;
            _standardOptionsMember = standardOptionsMember;
            _standardResolverMember = standardResolverMember;
        }

        public static MessagePackCompatibilityProbe? Create(IEnumerable<Assembly>? assemblies = null)
        {
            assemblies ??= AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                var serializerType = assembly.GetType("MessagePack.MessagePackSerializer", false);
                if (serializerType is null)
                {
                    continue;
                }

                var serializerOptionsType = assembly.GetType("MessagePack.MessagePackSerializerOptions", false);
                var compositeResolverType = assembly.GetType("MessagePack.Resolvers.CompositeResolver", false);
                var standardResolverType = assembly.GetType("MessagePack.Resolvers.StandardResolver", false);
                var resolverType = assembly.GetType("MessagePack.IFormatterResolver", false);
                var formatterType = assembly.GetType("MessagePack.Formatters.IMessagePackFormatter", false);
                var genericFormatterType = assembly.GetType("MessagePack.Formatters.IMessagePackFormatter`1", false);
                var writerType = assembly.GetType("MessagePack.MessagePackWriter", false);
                var readerType = assembly.GetType("MessagePack.MessagePackReader", false);
                var serializationExceptionType = assembly.GetType("MessagePack.MessagePackSerializationException", false);
                if (serializerOptionsType is null ||
                    compositeResolverType is null ||
                    standardResolverType is null ||
                    resolverType is null ||
                    formatterType is null ||
                    genericFormatterType is null ||
                    writerType is null ||
                    readerType is null ||
                    serializationExceptionType is null)
                {
                    continue;
                }

                var deserializeMethod = FindDeserializeMethod(serializerType, serializerOptionsType);
                var createResolverMethod = FindCreateResolverMethod(compositeResolverType, formatterType, resolverType);
                var withResolverMethod = FindWithResolverMethod(serializerOptionsType, resolverType);
                var standardOptionsMember = FindStaticMember(serializerOptionsType, "Standard", serializerOptionsType);
                var standardResolverMember = FindStaticMember(standardResolverType, "Instance", standardResolverType);
                if (deserializeMethod is null ||
                    createResolverMethod is null ||
                    withResolverMethod is null ||
                    standardOptionsMember is null ||
                    standardResolverMember is null ||
                    !HasResolverProperty(serializerOptionsType, resolverType) ||
                    !HasResolverGetFormatter(resolverType) ||
                    !HasWriterContract(writerType) ||
                    !HasReaderContract(readerType) ||
                    !HasSerializationExceptionContract(serializationExceptionType))
                {
                    continue;
                }

                return new MessagePackCompatibilityProbe(
                    serializerOptionsType,
                    formatterType,
                    resolverType,
                    deserializeMethod,
                    createResolverMethod,
                    withResolverMethod,
                    standardOptionsMember,
                    standardResolverMember);
            }

            return null;
        }

        public object? CreateOptions(params Type[] formatterTypes)
        {
            try
            {
                var standardOptions = GetMemberValue(_standardOptionsMember);
                if (standardOptions is null)
                {
                    return null;
                }

                var standardResolver = GetMemberValue(_standardResolverMember);
                if (standardResolver is null)
                {
                    return standardOptions;
                }

                var formatterInstances = new object[formatterTypes.Length];
                for (var index = 0; index < formatterTypes.Length; index++)
                {
                    var formatterInstance = Activator.CreateInstance(formatterTypes[index], true);
                    if (formatterInstance is null)
                    {
                        return standardOptions;
                    }

                    formatterInstances[index] = formatterInstance;
                }

                var formatterCollection = CreateCollectionArgument(_createResolverMethod.GetParameters()[0].ParameterType, _formatterType, formatterInstances);
                var resolverCollection = CreateCollectionArgument(_createResolverMethod.GetParameters()[1].ParameterType, _resolverType, new[] { standardResolver });
                var compositeResolver = _createResolverMethod.Invoke(null, new[] { formatterCollection, resolverCollection });
                if (compositeResolver is null)
                {
                    return standardOptions;
                }

                return _withResolverMethod.Invoke(standardOptions, new[] { compositeResolver });
            }
            catch
            {
                return GetMemberValue(_standardOptionsMember);
            }
        }

        public object? Deserialize(byte[] bytes, Type targetType, object? options)
        {
            try
            {
                var closedMethod = _deserializeMethod.MakeGenericMethod(targetType);
                var parameters = closedMethod.GetParameters();
                var arguments = new object?[parameters.Length];
                arguments[0] = CreateSerializedPayloadArgument(parameters[0].ParameterType, bytes);
                arguments[1] = options;
                if (parameters.Length > 2)
                {
                    arguments[2] = CancellationToken.None;
                }

                return closedMethod.Invoke(null, arguments);
            }
            catch
            {
                return null;
            }
        }

        private static bool HasResolverProperty(Type serializerOptionsType, Type resolverType)
        {
            var resolverProperty = serializerOptionsType.GetProperty("Resolver", BindingFlags.Public | BindingFlags.Instance);
            return resolverProperty?.PropertyType == resolverType;
        }

        private static bool HasResolverGetFormatter(Type resolverType)
        {
            return resolverType
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Any(method =>
                    method.Name == "GetFormatter" &&
                    method.IsGenericMethodDefinition &&
                    method.GetParameters().Length == 0);
        }

        private static bool HasWriterContract(Type writerType)
        {
            return writerType.GetMethod("WriteNil", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null) is not null &&
                   writerType.GetMethod("WriteArrayHeader", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(int) }, null) is not null;
        }

        private static bool HasReaderContract(Type readerType)
        {
            var tryReadNilMethod = readerType.GetMethod("TryReadNil", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            var readArrayHeaderMethod = readerType.GetMethod("ReadArrayHeader", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            return tryReadNilMethod?.ReturnType == typeof(bool) &&
                   readArrayHeaderMethod?.ReturnType == typeof(int) &&
                   readerType.GetMethod("Skip", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null) is not null;
        }

        private static bool HasSerializationExceptionContract(Type serializationExceptionType)
        {
            return serializationExceptionType.GetConstructor(new[] { typeof(string) }) is not null;
        }

        private static MethodInfo? FindDeserializeMethod(Type serializerType, Type serializerOptionsType)
        {
            return serializerType
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(method =>
                    method.Name == "Deserialize" &&
                    method.IsGenericMethodDefinition &&
                    method.GetParameters().Length >= 2)
                .OrderBy(GetDeserializePriority)
                .FirstOrDefault(method =>
                {
                    var parameters = method.GetParameters();
                    return AcceptsSerializedPayload(parameters[0].ParameterType) &&
                           parameters[1].ParameterType == serializerOptionsType;
                });
        }

        private static int GetDeserializePriority(MethodInfo method)
        {
            var payloadType = method.GetParameters()[0].ParameterType;
            if (IsReadOnlyMemoryOfByte(payloadType))
            {
                return 0;
            }

            if (payloadType == typeof(byte[]))
            {
                return 1;
            }

            if (payloadType == typeof(Stream))
            {
                return 2;
            }

            return 3;
        }

        private static bool AcceptsSerializedPayload(Type payloadType)
        {
            return payloadType == typeof(byte[]) ||
                   IsReadOnlyMemoryOfByte(payloadType) ||
                   payloadType == typeof(Stream);
        }

        private static MethodInfo? FindCreateResolverMethod(Type compositeResolverType, Type formatterType, Type resolverType)
        {
            return compositeResolverType
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(method => method.Name == "Create" && method.GetParameters().Length == 2)
                .OrderBy(GetCreateResolverPriority)
                .FirstOrDefault(method =>
                {
                    var parameters = method.GetParameters();
                    return AcceptsCollection(parameters[0].ParameterType, formatterType) &&
                           AcceptsCollection(parameters[1].ParameterType, resolverType);
                });
        }

        private static int GetCreateResolverPriority(MethodInfo method)
        {
            var parameters = method.GetParameters();
            if (IsReadOnlyListOf(parameters[0].ParameterType) && IsReadOnlyListOf(parameters[1].ParameterType))
            {
                return 0;
            }

            if (parameters[0].ParameterType.IsArray && parameters[1].ParameterType.IsArray)
            {
                return 1;
            }

            return 2;
        }

        private static bool IsReadOnlyListOf(Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>);
        }

        private static bool AcceptsCollection(Type parameterType, Type elementType)
        {
            if (parameterType.IsArray)
            {
                return parameterType.GetElementType() == elementType;
            }

            if (!parameterType.IsGenericType)
            {
                return false;
            }

            var genericDefinition = parameterType.GetGenericTypeDefinition();
            if (genericDefinition != typeof(IReadOnlyList<>) &&
                genericDefinition != typeof(IEnumerable<>))
            {
                return false;
            }

            return parameterType.GetGenericArguments()[0] == elementType;
        }

        private static MethodInfo? FindWithResolverMethod(Type serializerOptionsType, Type resolverType)
        {
            return serializerOptionsType.GetMethod("WithResolver", BindingFlags.Public | BindingFlags.Instance, null, new[] { resolverType }, null);
        }

        private static MemberInfo? FindStaticMember(Type type, string name, Type expectedType)
        {
            var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Static);
            if (property?.PropertyType == expectedType)
            {
                return property;
            }

            var field = type.GetField(name, BindingFlags.Public | BindingFlags.Static);
            return field?.FieldType == expectedType ? field : null;
        }

        private static object? GetMemberValue(MemberInfo member)
        {
            return member switch
            {
                PropertyInfo property => property.GetValue(null),
                FieldInfo field => field.GetValue(null),
                _ => null,
            };
        }

        private static object CreateCollectionArgument(Type parameterType, Type elementType, IReadOnlyList<object> values)
        {
            if (parameterType.IsArray)
            {
                var array = Array.CreateInstance(elementType, values.Count);
                for (var index = 0; index < values.Count; index++)
                {
                    array.SetValue(values[index], index);
                }

                return array;
            }

            var listType = typeof(List<>).MakeGenericType(elementType);
            var list = (IList)Activator.CreateInstance(listType)!;
            foreach (var value in values)
            {
                list.Add(value);
            }

            return list;
        }

        private static object CreateSerializedPayloadArgument(Type parameterType, byte[] bytes)
        {
            if (parameterType == typeof(byte[]))
            {
                return bytes;
            }

            if (IsReadOnlyMemoryOfByte(parameterType))
            {
                return new ReadOnlyMemory<byte>(bytes);
            }

            if (parameterType == typeof(Stream))
            {
                return new MemoryStream(bytes, writable: false);
            }

            throw new InvalidOperationException($"Unsupported deserialize payload parameter type: {parameterType.FullName}");
        }

        private static bool IsReadOnlyMemoryOfByte(Type type)
        {
            return type.IsGenericType &&
                   type.GetGenericTypeDefinition() == typeof(ReadOnlyMemory<>) &&
                   type.GetGenericArguments()[0] == typeof(byte);
        }
    }
}
#endif
