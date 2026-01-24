using Microsoft.CodeAnalysis;

namespace #DISPLAY_NAME#.Analyzer
{
    [Generator]
    public class HelloGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput(ctx =>
            {
                ctx.AddSource("Test.g.cs", "// Auto Generated Code \n public class GeneratedClass { }");
            });
        }
    }
}
