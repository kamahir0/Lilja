using System;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// 画面オブジェクト（GameScreen）のプライベートフィールドに対して、ロードされたビュー内の特定のコンポーネントを自動依存注入（インジェクション）することを指示するカスタム属性。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class ViewAttribute : Attribute { }
}
