using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Lilja.Repository.Analyzer;

public sealed partial class LiljaRepositoryGenerator
{
    /// <summary>
    /// Wraps the generated entity model together with diagnostics emitted during analysis.
    /// </summary>
    private sealed class EntityAnalysis
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EntityAnalysis"/> class.
        /// </summary>
        /// <param name="model">The generated entity model, when analysis succeeds.</param>
        /// <param name="diagnostics">Diagnostics emitted during analysis.</param>
        public EntityAnalysis(EntityModel? model, ImmutableArray<Diagnostic> diagnostics)
        {
            Model = model;
            Diagnostics = diagnostics;
        }

        /// <summary>
        /// Gets the analyzed entity model, or <see langword="null"/> when generation should be skipped.
        /// </summary>
        public EntityModel? Model { get; }

        /// <summary>
        /// Gets the diagnostics emitted while analyzing the entity.
        /// </summary>
        public ImmutableArray<Diagnostic> Diagnostics { get; }
    }

    /// <summary>
    /// Describes all metadata required to generate repositories and storage helpers for an entity.
    /// </summary>
    private sealed class EntityModel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EntityModel"/> class.
        /// </summary>
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

        /// <summary>
        /// Gets the original Roslyn symbol for the entity.
        /// </summary>
        public INamedTypeSymbol Symbol { get; }

        /// <summary>
        /// Gets the entity namespace, or an empty string when the entity is in the global namespace.
        /// </summary>
        public string NamespaceName { get; }

        /// <summary>
        /// Gets the stable identifier used for persisted file names and generated hint names.
        /// </summary>
        public string StorageIdentifier { get; }

        /// <summary>
        /// Gets the entity type name without namespace qualification.
        /// </summary>
        public string EntityName { get; }

        /// <summary>
        /// Gets the fully qualified entity type name.
        /// </summary>
        public string EntityTypeName { get; }

        /// <summary>
        /// Gets the namespace used for generated repository types.
        /// </summary>
        public string RepositoryNamespace { get; }

        /// <summary>
        /// Gets the namespace used for generated DTO types.
        /// </summary>
        public string DtoNamespace { get; }

        /// <summary>
        /// Gets the namespace used for generated storage envelope types.
        /// </summary>
        public string StorageNamespace { get; }

        /// <summary>
        /// Gets the namespace used for generated formatter types.
        /// </summary>
        public string FormatterNamespace { get; }

        /// <summary>
        /// Gets the fully qualified generated DTO type name.
        /// </summary>
        public string DtoTypeName { get; }

        /// <summary>
        /// Gets the generated DTO type name without namespace qualification.
        /// </summary>
        public string DtoTypeNameWithoutNamespace { get; }

        /// <summary>
        /// Gets the fully qualified generated storage envelope type name.
        /// </summary>
        public string StorageEnvelopeTypeName { get; }

        /// <summary>
        /// Gets the generated storage envelope type name without namespace qualification.
        /// </summary>
        public string StorageEnvelopeTypeNameWithoutNamespace { get; }

        /// <summary>
        /// Gets the generated DTO formatter type name without namespace qualification.
        /// </summary>
        public string DtoFormatterTypeNameWithoutNamespace { get; }

        /// <summary>
        /// Gets the generated storage envelope formatter type name without namespace qualification.
        /// </summary>
        public string StorageEnvelopeFormatterTypeNameWithoutNamespace { get; }

        /// <summary>
        /// Gets the generated key type expression used by repository signatures.
        /// </summary>
        public string KeyTypeName { get; }

        /// <summary>
        /// Gets the members annotated with <c>[Key]</c>.
        /// </summary>
        public ImmutableArray<MemberModel> KeyMembers { get; }

        /// <summary>
        /// Gets the members annotated with <c>[Persist]</c>.
        /// </summary>
        public ImmutableArray<MemberModel> PersistedMembers { get; }

        /// <summary>
        /// Gets the flattened DTO fields emitted for every persisted member.
        /// </summary>
        public ImmutableArray<DtoFieldModel> AllDtoFields { get; }

        /// <summary>
        /// Gets a value indicating whether a private constructor must be generated for DTO rehydration.
        /// </summary>
        public bool NeedsGeneratedConstructor { get; }

        /// <summary>
        /// Gets a value indicating whether any members are persisted.
        /// </summary>
        public bool IsPersisted => PersistedMembers.Length > 0;

        /// <summary>
        /// Gets a value indicating whether the entity is keyed.
        /// </summary>
        public bool IsKeyed => KeyMembers.Length > 0;
    }

    /// <summary>
    /// Describes one entity member that participates in generated repository behavior.
    /// </summary>
    private sealed class MemberModel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MemberModel"/> class.
        /// </summary>
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

        /// <summary>
        /// Gets the declared member name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the escaped member name used in generated source.
        /// </summary>
        public string AccessibleName { get; }

        /// <summary>
        /// Gets the Roslyn type symbol for the member.
        /// </summary>
        public ITypeSymbol TypeSymbol { get; }

        /// <summary>
        /// Gets the fully qualified type name used in generated source.
        /// </summary>
        public string TypeName { get; }

        /// <summary>
        /// Gets a value indicating whether the member is a property.
        /// </summary>
        public bool IsProperty { get; }

        /// <summary>
        /// Gets a value indicating whether the member participates in the generated key.
        /// </summary>
        public bool HasKey { get; }

        /// <summary>
        /// Gets a value indicating whether the member is persisted.
        /// </summary>
        public bool HasPersist { get; }

        /// <summary>
        /// Gets the declared persistence index, when present.
        /// </summary>
        public int? PersistIndex { get; }

        /// <summary>
        /// Gets the value-object conversion metadata, when the member is flattened to primitive DTO fields.
        /// </summary>
        public ValueObjectShape? ValueObjectShape { get; }

        /// <summary>
        /// Gets the DTO fields generated from this member.
        /// </summary>
        public ImmutableArray<DtoFieldModel> DtoFields { get; }

        /// <summary>
        /// Gets the location used when reporting diagnostics for the member.
        /// </summary>
        public Location Location { get; }
    }

    /// <summary>
    /// Describes how a value object converts to and from primitive DTO fields.
    /// </summary>
    private sealed class ValueObjectShape
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ValueObjectShape"/> class.
        /// </summary>
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

        /// <summary>
        /// Gets the method name used to expose primitive values.
        /// </summary>
        public string ToPrimitiveMethodName { get; }

        /// <summary>
        /// Gets the strategy used to reconstruct the value object.
        /// </summary>
        public ValueObjectCreationKind CreationKind { get; }

        /// <summary>
        /// Gets the static factory name used for reconstruction when applicable.
        /// </summary>
        public string CreationMemberName { get; }

        /// <summary>
        /// Gets the primitive DTO parts emitted for the value object.
        /// </summary>
        public ImmutableArray<PrimitivePartModel> PrimitiveParts { get; }
    }

    /// <summary>
    /// Describes one primitive piece of a value-object representation.
    /// </summary>
    private sealed class PrimitivePartModel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PrimitivePartModel"/> class.
        /// </summary>
        public PrimitivePartModel(ITypeSymbol typeSymbol, string typeName, string accessName, string dtoSuffixName)
        {
            TypeSymbol = typeSymbol;
            TypeName = typeName;
            AccessName = accessName;
            DtoSuffixName = dtoSuffixName;
        }

        /// <summary>
        /// Gets the Roslyn type symbol for the primitive part.
        /// </summary>
        public ITypeSymbol TypeSymbol { get; }

        /// <summary>
        /// Gets the fully qualified type name used in generated source.
        /// </summary>
        public string TypeName { get; }

        /// <summary>
        /// Gets the tuple accessor used when reading a multi-part primitive value.
        /// </summary>
        public string AccessName { get; }

        /// <summary>
        /// Gets the suffix appended to generated DTO field names.
        /// </summary>
        public string DtoSuffixName { get; }
    }

    /// <summary>
    /// Describes one generated field on a DTO type.
    /// </summary>
    private sealed class DtoFieldModel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DtoFieldModel"/> class.
        /// </summary>
        public DtoFieldModel(string name, string typeName, string tupleAccessName)
        {
            Name = name;
            TypeName = typeName;
            TupleAccessName = tupleAccessName;
        }

        /// <summary>
        /// Gets the generated field name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the fully qualified field type name.
        /// </summary>
        public string TypeName { get; }

        /// <summary>
        /// Gets the tuple member accessor used when mapping multi-part primitives.
        /// </summary>
        public string TupleAccessName { get; }
    }

    /// <summary>
    /// Identifies how a value object is recreated from primitive DTO fields.
    /// </summary>
    private enum ValueObjectCreationKind
    {
        Constructor,
        StaticFactory,
    }
}
