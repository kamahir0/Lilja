using Microsoft.CodeAnalysis;

namespace MyUnityGenerator
{
    [Generator]
    public class HelloGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // ビルド時に必ず実行される登録
            context.RegisterPostInitializationOutput(ctx =>
            {
                ctx.AddSource("Test.g.cs", "// Auto Generated Code \n public class GeneratedClass { }");
                ctx.AddSource("Test2.g.cs", "// Auto Generated Code \n public class GeneratedClass2 { }");
            });
        }
    }
}