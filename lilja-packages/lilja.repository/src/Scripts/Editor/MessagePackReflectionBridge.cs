using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Lilja.Repository.Editor
{
    internal static class MessagePackReflectionBridge
    {
        private static readonly Type SerializerType = RuntimeTypeCache.FindType("MessagePackSerializer");
        private static readonly Type SerializerOptionsType = RuntimeTypeCache.FindType("MessagePackSerializerOptions");
        private static readonly Type CompositeResolverType = RuntimeTypeCache.FindType("CompositeResolver");
        private static readonly Type StandardResolverType = RuntimeTypeCache.FindType("StandardResolver");
        private static readonly Type FormatterInterfaceType = RuntimeTypeCache.FindType("IMessagePackFormatter");
        private static readonly Type FormatterResolverType = RuntimeTypeCache.FindType("IFormatterResolver");

        public static bool IsAvailable =>
            SerializerType != null &&
            SerializerOptionsType != null &&
            CompositeResolverType != null &&
            StandardResolverType != null &&
            FormatterInterfaceType != null &&
            FormatterResolverType != null;

        public static object CreateOptions(params Type[] formatterTypes)
        {
            if (!IsAvailable)
            {
                return null;
            }

            var options = SerializerOptionsType.GetProperty("Standard", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null);
            if (options == null)
            {
                return null;
            }

            if (formatterTypes == null || formatterTypes.Length == 0)
            {
                return options;
            }

            try
            {
                var formatterInstances = new List<object>();
                foreach (var formatterType in formatterTypes)
                {
                    if (formatterType == null)
                    {
                        continue;
                    }

                    var formatterInstance = Activator.CreateInstance(formatterType);
                    if (formatterInstance != null && FormatterInterfaceType.IsInstanceOfType(formatterInstance))
                    {
                        formatterInstances.Add(formatterInstance);
                    }
                }

                if (formatterInstances.Count == 0)
                {
                    return options;
                }

                var formatters = Array.CreateInstance(FormatterInterfaceType, formatterInstances.Count);
                for (var index = 0; index < formatterInstances.Count; index++)
                {
                    formatters.SetValue(formatterInstances[index], index);
                }

                var standardResolver = StandardResolverType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                    ?.GetValue(null);
                var resolvers = Array.CreateInstance(FormatterResolverType, 1);
                resolvers.SetValue(standardResolver, 0);

                var createMethod = CompositeResolverType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(method => method.Name == "Create" && method.GetParameters().Length == 2);
                if (createMethod == null)
                {
                    return options;
                }

                var resolver = createMethod.Invoke(null, new object[] { formatters, resolvers });
                var withResolverMethod = SerializerOptionsType.GetMethod("WithResolver", new[] { FormatterResolverType });
                return withResolverMethod?.Invoke(options, new[] { resolver }) ?? options;
            }
            catch
            {
                return options;
            }
        }

        public static object Deserialize(byte[] bytes, Type targetType, object options)
        {
            if (!IsAvailable || targetType == null)
            {
                return null;
            }

            var deserializeMethod = SerializerType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method =>
                    method.Name == "Deserialize" &&
                    method.IsGenericMethodDefinition &&
                    method.GetParameters().Length >= 2 &&
                    method.GetParameters()[0].ParameterType == typeof(byte[]));
            if (deserializeMethod == null)
            {
                throw new MissingMethodException("MessagePackSerializer.Deserialize<byte[]> overload was not found.");
            }

            var genericMethod = deserializeMethod.MakeGenericMethod(targetType);
            var parameters = genericMethod.GetParameters();
            var arguments = new object[parameters.Length];
            arguments[0] = bytes;
            arguments[1] = options;

            for (var index = 2; index < parameters.Length; index++)
            {
                arguments[index] = parameters[index].HasDefaultValue ? parameters[index].DefaultValue : Type.Missing;
            }

            return genericMethod.Invoke(null, arguments);
        }
    }
}
