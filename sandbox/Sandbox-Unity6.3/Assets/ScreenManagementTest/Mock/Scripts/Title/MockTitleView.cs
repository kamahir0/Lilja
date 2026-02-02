using UnityEngine;
using UnityEngine.UI;

namespace Lilja.ScreenManagement.Mock
{
    /// <summary>
    /// タイトル画面のView
    /// </summary>
    public class MockTitleView : MonoBehaviour
    {
        [SerializeField] private Button _startButton;

        /// <summary> スタートボタン </summary>
        public Button StartButton => _startButton;
    }
}