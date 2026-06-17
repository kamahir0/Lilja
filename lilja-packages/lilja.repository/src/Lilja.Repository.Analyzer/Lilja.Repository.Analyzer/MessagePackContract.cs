using Microsoft.CodeAnalysis;

namespace Lilja.Repository.Analyzer;

internal static class MessagePackContract
{
    public static bool HasCompatibleContract(Compilation compilation)
    {
        return compilation.GetTypeByMetadataName("MessagePack.MessagePackSerializer") is not null &&
               compilation.GetTypeByMetadataName("MessagePack.MessagePackSerializerOptions") is not null &&
               compilation.GetTypeByMetadataName("MessagePack.Formatters.IMessagePackFormatter`1") is not null &&
               compilation.GetTypeByMetadataName("MessagePack.Resolvers.CompositeResolver") is not null &&
               compilation.GetTypeByMetadataName("MessagePack.Resolvers.StandardResolver") is not null;
    }
}
