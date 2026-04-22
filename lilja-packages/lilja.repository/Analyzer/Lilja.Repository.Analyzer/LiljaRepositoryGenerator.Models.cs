using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Lilja.Repository.Analyzer;

public sealed partial class LiljaRepositoryGenerator
{
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
        public ValueObjectShape(
            string toPrimitiveMethodName,
            ValueObjectCreationKind creationKind,
            string creationMemberName,
            ImmutableArray<PrimitivePartModel> primitiveParts)
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
