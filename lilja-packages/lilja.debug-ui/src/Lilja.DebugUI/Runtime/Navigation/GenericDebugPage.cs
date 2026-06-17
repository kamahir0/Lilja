using System;

namespace Lilja.DebugUI
{
    /// <summary>
    /// ラムダ式でUIを構成できる汎用DebugPage。
    /// 型として同じ GenericDebugPage でも、name が異なれば別ページとして履歴管理される。
    /// </summary>
    public sealed class GenericDebugPage : DebugPage
    {
        private readonly Action<IDebugUIBuilder> _configure;

        public GenericDebugPage(string name, Action<IDebugUIBuilder> configure)
        {
            this.name = name;
            _configure = configure;
        }

        public override void Configure(IDebugUIBuilder builder) => _configure?.Invoke(builder);
    }
}
