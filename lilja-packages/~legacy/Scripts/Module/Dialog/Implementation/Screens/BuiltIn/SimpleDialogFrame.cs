using UnityEngine;
using UnityEngine.UI;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// SimpleDialog 用のフレーム
    /// </summary>
    public class SimpleDialogFrame : MonoBehaviour, IDialogFrame
    {
        [SerializeField] private RectTransform _contentContainer;
        [SerializeField] private Text _titleText;
        [SerializeField] private Transform _buttonContainer;
        [SerializeField] private GameObject _buttonPrefab;

        /// <inheritdoc />
        public RectTransform ContentContainer => _contentContainer;

        /// <summary> タイトル Text </summary>
        public Text TitleText => _titleText;

        /// <summary> ボタン配置用の Transform </summary>
        public Transform ButtonContainer => _buttonContainer;

        /// <summary> ボタンの Prefab </summary>
        public GameObject ButtonPrefab => _buttonPrefab;

        /// <summary>
        /// タイトルを設定します
        /// </summary>
        /// <param name="title">タイトル文字列</param>
        public void SetTitle(string title)
        {
            if (_titleText != null)
            {
                _titleText.text = title;
            }
        }

        /// <summary>
        /// ボタンを追加します
        /// </summary>
        /// <param name="label">ボタンのラベル</param>
        /// <param name="onClick">ボタン押下時のアクション</param>
        public void AddButton(string label, System.Action onClick)
        {
            if (_buttonContainer == null || _buttonPrefab == null) return;

            var buttonObj = Instantiate(_buttonPrefab, _buttonContainer);
            buttonObj.SetActive(true);

            var button = buttonObj.GetComponent<Button>();

            // ボタンのラベルを設定
            var text = buttonObj.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = label;
            }

            if (button != null)
            {
                button.onClick.AddListener(() => onClick?.Invoke());
            }
        }
    }
}
