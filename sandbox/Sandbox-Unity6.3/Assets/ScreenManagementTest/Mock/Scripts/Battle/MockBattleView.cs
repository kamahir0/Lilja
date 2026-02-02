using UnityEngine;
using UnityEngine.UI;

namespace Lilja.ScreenManagement.Mock
{
    /// <summary>
    /// バトルパートの View
    /// </summary>
    public class MockBattleView : MonoBehaviour
    {
        [SerializeField] private Button _finishButton;

        /// <summary> 終了ボタン </summary>
        public Button FinishButton => _finishButton;
    }
}
