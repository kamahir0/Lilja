using System;
using UnityEngine;
using UnityEngine.UI;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// デフォルトデザインのダイアログ用コンテンツコンポーネント。
    /// </summary>
    public class DefaultDialogContent : MonoBehaviour
    {
        [SerializeField]
        private Text _bodyText;

        /// <summary>
        /// 本文テキストコンポーネントを取得します。
        /// </summary>
        public Text BodyText => _bodyText;

        /// <summary>
        /// テキストを追加します。
        /// </summary>
        /// <param name="text">追加するテキスト文字列。</param>
        /// <exception cref="InvalidOperationException">本文テキストコンポーネントのシリアライズド参照が不足している場合にスローされます。</exception>
        public void AddText(string text)
        {
            if (_bodyText == null)
            {
                Debug.LogError(
                    "[Lilja.ScreenManagement.Dialog] DefaultDialogContent の本文テキストコンポーネント（_bodyText）が設定されていません。プレハブのシリアライズ参照を確認してください。"
                );
                return;
            }

            // 既存テキストがあれば改行で連結、なければそのまま代入
            if (!string.IsNullOrEmpty(_bodyText.text))
            {
                _bodyText.text += "\n" + text;
            }
            else
            {
                _bodyText.text = text;
            }
        }
    }
}
