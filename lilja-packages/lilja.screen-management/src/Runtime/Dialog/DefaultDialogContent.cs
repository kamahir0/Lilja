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
        /// <summary>
        /// 本文テキストコンポーネント。Inspector またはコードから直接設定します。
        /// </summary>
        public Text BodyText;

        /// <summary>
        /// テキストを追加します。
        /// </summary>
        /// <param name="text">追加するテキスト文字列。</param>
        /// <exception cref="InvalidOperationException">本文テキストコンポーネントのシリアライズド参照が不足している場合にスローされます。</exception>
        public void AddText(string text)
        {
            if (BodyText == null)
            {
                Debug.LogError(
                    "[Lilja.ScreenManagement.Dialog] DefaultDialogContent の本文テキストコンポーネント（BodyText）が設定されていません。プレハブのシリアライズ参照を確認してください。"
                );
                return;
            }

            // 既存テキストがあれば改行で連結、なければそのまま代入
            if (!string.IsNullOrEmpty(BodyText.text))
            {
                BodyText.text += "\n" + text;
            }
            else
            {
                BodyText.text = text;
            }
        }
    }
}
