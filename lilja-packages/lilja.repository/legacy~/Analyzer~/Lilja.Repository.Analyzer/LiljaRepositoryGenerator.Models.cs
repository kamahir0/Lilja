using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Lilja.Repository.Analyzer;

public sealed partial class LiljaRepositoryGenerator
{
    /// <summary>
    /// 生成されたエンティティモデルと、解析中に出力された診断をまとめて保持します。
    /// </summary>
    private sealed class EntityAnalysis
    {
        /// <summary>
        /// <see cref="EntityAnalysis"/> クラスの新しいインスタンスを初期化します。
        /// </summary>
        /// <param name="model">解析成功時に得られる生成エンティティモデル。</param>
        /// <param name="diagnostics">解析中に出力された診断。</param>
        public EntityAnalysis(EntityModel? model, ImmutableArray<Diagnostic> diagnostics)
        {
            Model = model;
            Diagnostics = diagnostics;
        }

        /// <summary>
        /// 解析済みのエンティティモデルを取得します。生成をスキップすべき場合は <see langword="null"/> です。
        /// </summary>
        public EntityModel? Model { get; }

        /// <summary>
        /// エンティティ解析中に出力された診断を取得します。
        /// </summary>
        public ImmutableArray<Diagnostic> Diagnostics { get; }
    }

    /// <summary>
    /// エンティティ用のリポジトリとストレージ補助コードを生成するために必要な、すべてのメタデータを表します。
    /// </summary>
    private sealed class EntityModel
    {
        /// <summary>
        /// <see cref="EntityModel"/> クラスの新しいインスタンスを初期化します。
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
        /// エンティティに対応する元の Roslyn シンボルを取得します。
        /// </summary>
        public INamedTypeSymbol Symbol { get; }

        /// <summary>
        /// エンティティの名前空間を取得します。グローバル名前空間にある場合は空文字列です。
        /// </summary>
        public string NamespaceName { get; }

        /// <summary>
        /// 永続化ファイル名と生成ヒント名に使う安定した識別子を取得します。
        /// </summary>
        public string StorageIdentifier { get; }

        /// <summary>
        /// 名前空間を含まないエンティティ型名を取得します。
        /// </summary>
        public string EntityName { get; }

        /// <summary>
        /// 完全修飾されたエンティティ型名を取得します。
        /// </summary>
        public string EntityTypeName { get; }

        /// <summary>
        /// 生成リポジトリ型で使う名前空間を取得します。
        /// </summary>
        public string RepositoryNamespace { get; }

        /// <summary>
        /// 生成 DTO 型で使う名前空間を取得します。
        /// </summary>
        public string DtoNamespace { get; }

        /// <summary>
        /// 生成ストレージエンベロープ型で使う名前空間を取得します。
        /// </summary>
        public string StorageNamespace { get; }

        /// <summary>
        /// 生成フォーマッター型で使う名前空間を取得します。
        /// </summary>
        public string FormatterNamespace { get; }

        /// <summary>
        /// 完全修飾された生成 DTO 型名を取得します。
        /// </summary>
        public string DtoTypeName { get; }

        /// <summary>
        /// 名前空間を含まない生成 DTO 型名を取得します。
        /// </summary>
        public string DtoTypeNameWithoutNamespace { get; }

        /// <summary>
        /// 完全修飾された生成ストレージエンベロープ型名を取得します。
        /// </summary>
        public string StorageEnvelopeTypeName { get; }

        /// <summary>
        /// 名前空間を含まない生成ストレージエンベロープ型名を取得します。
        /// </summary>
        public string StorageEnvelopeTypeNameWithoutNamespace { get; }

        /// <summary>
        /// 名前空間を含まない生成 DTO フォーマッター型名を取得します。
        /// </summary>
        public string DtoFormatterTypeNameWithoutNamespace { get; }

        /// <summary>
        /// 名前空間を含まない生成ストレージエンベロープフォーマッター型名を取得します。
        /// </summary>
        public string StorageEnvelopeFormatterTypeNameWithoutNamespace { get; }

        /// <summary>
        /// リポジトリシグネチャで使用される生成キー型式を取得します。
        /// </summary>
        public string KeyTypeName { get; }

        /// <summary>
        /// <c>[Key]</c> が付いたメンバーを取得します。
        /// </summary>
        public ImmutableArray<MemberModel> KeyMembers { get; }

        /// <summary>
        /// <c>[Persist]</c> が付いたメンバーを取得します。
        /// </summary>
        public ImmutableArray<MemberModel> PersistedMembers { get; }

        /// <summary>
        /// 各永続化対象メンバーに対して出力される、展開済み DTO フィールドを取得します。
        /// </summary>
        public ImmutableArray<DtoFieldModel> AllDtoFields { get; }

        /// <summary>
        /// DTO からの再構築のために private コンストラクタを生成する必要があるかどうかを示す値を取得します。
        /// </summary>
        public bool NeedsGeneratedConstructor { get; }

        /// <summary>
        /// 永続化対象のメンバーが存在するかどうかを示す値を取得します。
        /// </summary>
        public bool IsPersisted => PersistedMembers.Length > 0;

        /// <summary>
        /// エンティティがキー付きかどうかを示す値を取得します。
        /// </summary>
        public bool IsKeyed => KeyMembers.Length > 0;
    }

    /// <summary>
    /// 生成されるリポジトリ動作に参加する 1 つのエンティティメンバーを表します。
    /// </summary>
    private sealed class MemberModel
    {
        /// <summary>
        /// <see cref="MemberModel"/> クラスの新しいインスタンスを初期化します。
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
        /// 宣言されたメンバー名を取得します。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 生成ソースで使うエスケープ済みメンバー名を取得します。
        /// </summary>
        public string AccessibleName { get; }

        /// <summary>
        /// メンバーに対応する Roslyn 型シンボルを取得します。
        /// </summary>
        public ITypeSymbol TypeSymbol { get; }

        /// <summary>
        /// 生成ソースで使う完全修飾型名を取得します。
        /// </summary>
        public string TypeName { get; }

        /// <summary>
        /// メンバーがプロパティかどうかを示す値を取得します。
        /// </summary>
        public bool IsProperty { get; }

        /// <summary>
        /// メンバーが生成されるキーに参加するかどうかを示す値を取得します。
        /// </summary>
        public bool HasKey { get; }

        /// <summary>
        /// メンバーが永続化対象かどうかを示す値を取得します。
        /// </summary>
        public bool HasPersist { get; }

        /// <summary>
        /// 宣言された永続化インデックスを取得します。存在しない場合もあります。
        /// </summary>
        public int? PersistIndex { get; }

        /// <summary>
        /// メンバーがプリミティブ DTO フィールドへ展開される場合の、値オブジェクト変換メタデータを取得します。
        /// </summary>
        public ValueObjectShape? ValueObjectShape { get; }

        /// <summary>
        /// このメンバーから生成された DTO フィールドを取得します。
        /// </summary>
        public ImmutableArray<DtoFieldModel> DtoFields { get; }

        /// <summary>
        /// メンバーに対する診断を報告するときに使う位置情報を取得します。
        /// </summary>
        public Location Location { get; }
    }

    /// <summary>
    /// 値オブジェクトがプリミティブ DTO フィールドへ変換され、そこから復元される方法を表します。
    /// </summary>
    private sealed class ValueObjectShape
    {
        /// <summary>
        /// <see cref="ValueObjectShape"/> クラスの新しいインスタンスを初期化します。
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
        /// プリミティブ値を公開するために使うメソッド名を取得します。
        /// </summary>
        public string ToPrimitiveMethodName { get; }

        /// <summary>
        /// 値オブジェクトを復元するために使う戦略を取得します。
        /// </summary>
        public ValueObjectCreationKind CreationKind { get; }

        /// <summary>
        /// 該当する場合に復元で使う静的ファクトリ名を取得します。
        /// </summary>
        public string CreationMemberName { get; }

        /// <summary>
        /// 値オブジェクトに対して出力されるプリミティブ DTO 要素を取得します。
        /// </summary>
        public ImmutableArray<PrimitivePartModel> PrimitiveParts { get; }
    }

    /// <summary>
    /// 値オブジェクト表現を構成する 1 つのプリミティブ要素を表します。
    /// </summary>
    private sealed class PrimitivePartModel
    {
        /// <summary>
        /// <see cref="PrimitivePartModel"/> クラスの新しいインスタンスを初期化します。
        /// </summary>
        public PrimitivePartModel(ITypeSymbol typeSymbol, string typeName, string accessName, string dtoSuffixName)
        {
            TypeSymbol = typeSymbol;
            TypeName = typeName;
            AccessName = accessName;
            DtoSuffixName = dtoSuffixName;
        }

        /// <summary>
        /// プリミティブ要素に対応する Roslyn 型シンボルを取得します。
        /// </summary>
        public ITypeSymbol TypeSymbol { get; }

        /// <summary>
        /// 生成ソースで使う完全修飾型名を取得します。
        /// </summary>
        public string TypeName { get; }

        /// <summary>
        /// 複数要素のプリミティブ値を読み取るときに使うタプルアクセサを取得します。
        /// </summary>
        public string AccessName { get; }

        /// <summary>
        /// 生成 DTO フィールド名へ付与される接尾辞を取得します。
        /// </summary>
        public string DtoSuffixName { get; }
    }

    /// <summary>
    /// DTO 型上に生成される 1 つのフィールドを表します。
    /// </summary>
    private sealed class DtoFieldModel
    {
        /// <summary>
        /// <see cref="DtoFieldModel"/> クラスの新しいインスタンスを初期化します。
        /// </summary>
        public DtoFieldModel(string name, string typeName, string tupleAccessName)
        {
            Name = name;
            TypeName = typeName;
            TupleAccessName = tupleAccessName;
        }

        /// <summary>
        /// 生成されるフィールド名を取得します。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 完全修飾されたフィールド型名を取得します。
        /// </summary>
        public string TypeName { get; }

        /// <summary>
        /// 複数要素のプリミティブをマッピングするときに使うタプルメンバーアクセサを取得します。
        /// </summary>
        public string TupleAccessName { get; }
    }

    /// <summary>
    /// 値オブジェクトがプリミティブ DTO フィールドからどのように再生成されるかを識別します。
    /// </summary>
    private enum ValueObjectCreationKind
    {
        Constructor,
        StaticFactory,
    }
}
