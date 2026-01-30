using UnityEngine;
using UnityEngine.UI;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// SimpleDialog 用の Content
    /// </summary>
    public class SimpleDialogContent : MonoBehaviour
    {
        [SerializeField] private Text _bodyText;
        [SerializeField] private Image _imageContainer;

        /// <summary> 本文 Text </summary>
        public Text BodyText => _bodyText;

        /// <summary> 画像コンテナ Image </summary>
        public Image ImageContainer => _imageContainer;

        /// <summary>
        /// テキストを追加します
        /// </summary>
        /// <param name="text">追加するテキスト</param>
        public void AddText(string text)
        {
            if (_bodyText == null) return;

            // 既存テキストがあれば改行で連結
            if (!string.IsNullOrEmpty(_bodyText.text))
            {
                _bodyText.text += "\n" + text;
            }
            else
            {
                _bodyText.text = text;
            }
        }

        /// <summary>
        /// 画像を追加します
        /// </summary>
        /// <param name="sprite">表示する Sprite</param>
        public void AddImage(Sprite sprite)
        {
            if (_imageContainer == null || sprite == null) return;

            _imageContainer.gameObject.SetActive(true);
            _imageContainer.sprite = sprite;
        }
    }
}
