using System;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// 画面クラス（GameScreenBaseの派生）内のフィールドに付与することで、
    /// ビューアセット生成時に対応するコンポーネントを自動的に探し出して注入するよう指定するアトリビュート（属性）。
    /// </summary>
    /// <remarks>
    /// この属性はプロパティには付与せず、原則 private/protected フィールドに対して付与します。
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class UnityViewAttribute : Attribute
    {
        /// <summary>
        /// 新しい <see cref="UnityViewAttribute"/> インスタンスを初期化します。
        /// </summary>
        public UnityViewAttribute() { }
    }
}
