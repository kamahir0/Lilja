using System.ComponentModel;

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// 古い .NET Standard バージョンや Unity 環境で C# 9.0 の init アクセサおよび record 型のコンパイルを可能にするためのダミー型定義。
    /// コンパイラはこの型が存在することを期待してコンパイルを行います。
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit { }
}
