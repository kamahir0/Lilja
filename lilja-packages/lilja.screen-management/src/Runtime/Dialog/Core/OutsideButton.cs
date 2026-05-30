using UnityEngine.UI;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// ダイアログの「枠外領域」がクリックされたことを検知するための、インジェクション判別用専用ボタンコンポーネント。
    /// 型による確実な [View] 依存注入を実現するために定義されています。
    /// </summary>
    internal sealed class OutsideButton : Button { }
}
