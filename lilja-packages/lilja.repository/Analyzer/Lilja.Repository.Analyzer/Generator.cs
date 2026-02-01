using Microsoft.CodeAnalysis;

namespace SourceGeneratorSample;

[Generator(LanguageNames.CSharp)]
public partial class SampleGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
        {
            ctx.AddSource("SampleGeneratorAttribute.cs", """
using System;
namespace SourceGeneratorSample
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    internal sealed class GenerateToStringAttribute : Attribute
    {
    }
}
""");});

        var source = context.SyntaxProvider.ForAttributeWithMetadataName(
        "SourceGeneratorSample.GenerateToStringAttribute",
        static (node, token) => true,
        static (context, token) => context);

        context.RegisterSourceOutput(source, Emit);
    }

    private static void Emit(SourceProductionContext context, GeneratorAttributeSyntaxContext syntaxContext) 
    {
    }
}