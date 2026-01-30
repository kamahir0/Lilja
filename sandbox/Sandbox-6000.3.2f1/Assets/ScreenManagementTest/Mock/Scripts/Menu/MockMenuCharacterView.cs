using UnityEngine;
using UnityEngine.UI;

namespace Lilja.ScreenManagement.Mock
{
    public class MockMenuCharacterView : MonoBehaviour
    {
        [SerializeField] private Button _testButton;
        [SerializeField] private Button _closeButton;

        /// <summary> テストボタン </summary>
        public Button TestButton => _testButton;

        /// <summary> 閉じるボタン </summary>
        public Button CloseButton => _closeButton;
    }
}