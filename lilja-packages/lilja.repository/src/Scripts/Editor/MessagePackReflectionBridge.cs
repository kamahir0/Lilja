#nullable enable
#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Lilja.Repository.Editor
{
    /// <summary>
    /// Accesses MessagePack APIs through reflection so the editor tooling can work when the package is optional.
    /// </summary>
    internal static class MessagePackReflectionBridge
    {
        private static readonly Type? SerializerType = FindType("MessagePack.MessagePackSerializer");
        private static readonly Type? SerializerOptionsType = FindType("MessagePack.MessagePackSerializerOptions");
        private static readonly Type? CompositeResolverType = FindType("MessagePack.Resolvers.CompositeResolver");
        private static readonly Type? StandardResolverType = FindType("MessagePack.Resolvers.StandardResolver");
        private static readonly Type? FormatterType = FindType("MessagePack.Formatters.IMessagePackFormatter");
        private static readonly Type? ResolverType = FindType("MessagePack.IFormatterResolver");
        private static readonly MethodInfo? DeserializeMethod = FindDeserializeMethod();
        private static readonly MethodInfo? CreateResolverMethod = FindCreateResolverMethod();
        private static readonly MethodInfo? WithResolverMethod = FindWithResolverMethod();
        private static readonly PropertyInfo? StandardOptionsProperty = SerializerOptionsType?.GetProperty("Standard", BindingFlags.Public | BindingFlags.Static);
        private static readonly PropertyInfo? StandardResolverProperty = StandardResolverType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);

        /// <summary>
        /// Gets a value indicating whether the required MessagePack runtime types are available.
        /// </summary>
        public static bool IsAvailable =>
            SerializerType is not null &&
            SerializerOptionsType is not null &&
            CompositeResolverType is not null &&
            StandardResolverType is not null &&
            FormatterType is not null &&
            ResolverType is not null &&
            DeserializeMethod is not null &&
            CreateResolverMethod is not null &&
            WithResolverMethod is not null &&
            StandardOptionsProperty is not null &&
            StandardResolverProperty is not null;

        /// <summary>
        /// Creates serializer options that include the supplied formatter types when MessagePack is available.
        /// </summary>
        /// <param name="formatterTypes">Formatter types that should be registered ahead of the standard resolver.</param>
        /// <returns>The configured options object, or the standard options when custom registration is unavailable.</returns>
        public static object? CreateOptions(params Type[] formatterTypes)
        {
            try
            {
                var standardOptions = GetStandardOptions();
                if (!IsAvailable || standardOptions is null || FormatterType is null || ResolverType is null || CreateResolverMethod is null || WithResolverMethod is null)
                {
                    return standardOptions;
                }

                var formatterArray = Array.CreateInstance(FormatterType, formatterTypes.Length);
                for (var index = 0; index < formatterTypes.Length; index++)
                {
                    var formatterInstance = Activator.CreateInstance(formatterTypes[index], true);
                    if (formatterInstance is null)
                    {
                        return standardOptions;
                    }

                    formatterArray.SetValue(formatterInstance, index);
                }

                var standardResolver = GetStandardResolver();
                if (standardResolver is null)
                {
                    return standardOptions;
                }

                var resolverArray = Array.CreateInstance(ResolverType, 1);
                resolverArray.SetValue(standardResolver, 0);

                var compositeResolver = CreateResolverMethod.Invoke(null, new object[] { formatterArray, resolverArray });
                if (compositeResolver is null)
                {
                    return standardOptions;
                }

                return WithResolverMethod.Invoke(standardOptions, new[] { compositeResolver });
            }
            catch
            {
                return GetStandardOptions();
            }
        }

        /// <summary>
        /// Deserializes MessagePack bytes into the requested runtime type.
        /// </summary>
        /// <param name="bytes">The serialized payload.</param>
        /// <param name="targetType">The runtime type to deserialize.</param>
        /// <param name="options">The serializer options to use.</param>
        /// <returns>The deserialized value, or <see langword="null"/> when deserialization cannot be performed.</returns>
        public static object? Deserialize(byte[] bytes, Type targetType, object? options)
        {
            try
            {
                if (!IsAvailable || DeserializeMethod is null)
                {
                    return null;
                }

                var closedMethod = DeserializeMethod.MakeGenericMethod(targetType);
                var parameters = closedMethod.GetParameters();
                if (parameters.Length == 2)
                {
                    return closedMethod.Invoke(null, new object?[] { bytes, options });
                }

                if (parameters.Length == 3)
                {
                    return closedMethod.Invoke(null, new object?[] { bytes, options, CancellationToken.None });
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static object? GetStandardOptions()
        {
            return StandardOptionsProperty?.GetValue(null);
        }

        private static object? GetStandardResolver()
        {
            return StandardResolverProperty?.GetValue(null);
        }

        private static Type? FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);
                if (type is not null)
                {
                    return type;
                }
            }

            return null;
        }

        private static MethodInfo? FindDeserializeMethod()
        {
            if (SerializerType is null || SerializerOptionsType is null)
            {
                return null;
            }

            return SerializerType
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method =>
                {
                    if (!method.IsGenericMethodDefinition || method.Name != "Deserialize")
                    {
                        return false;
                    }

                    var parameters = method.GetParameters();
                    if (parameters.Length < 2 || parameters.Length > 3)
                    {
                        return false;
                    }

                    return parameters[0].ParameterType == typeof(byte[]) &&
                           parameters[1].ParameterType == SerializerOptionsType;
                });
        }

        private static MethodInfo? FindCreateResolverMethod()
        {
            if (CompositeResolverType is null || FormatterType is null || ResolverType is null)
            {
                return null;
            }

            return CompositeResolverType
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method =>
                {
                    if (method.Name != "Create")
                    {
                        return false;
                    }

                    var parameters = method.GetParameters();
                    if (parameters.Length != 2)
                    {
                        return false;
                    }

                    return parameters[0].ParameterType == FormatterType.MakeArrayType() &&
                           parameters[1].ParameterType == ResolverType.MakeArrayType();
                });
        }

        private static MethodInfo? FindWithResolverMethod()
        {
            if (SerializerOptionsType is null || ResolverType is null)
            {
                return null;
            }

            return SerializerOptionsType.GetMethod("WithResolver", BindingFlags.Public | BindingFlags.Instance, null, new[] { ResolverType }, null);
        }
    }
}
#endif
