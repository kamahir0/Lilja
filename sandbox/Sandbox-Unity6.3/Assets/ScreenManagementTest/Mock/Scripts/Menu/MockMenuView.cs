using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Lilja.ScreenManagement.Mock
{
    public class MockMenuView : MonoBehaviour
    {
        [SerializeField] private Button _characterButton;
        [SerializeField] private Button _battleButton;
        [SerializeField] private Button _titleButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private RectTransform _panel;
        [SerializeField] private Vector2 _panelPosDefault;
        [SerializeField] private Vector2 _panelPosHide;
        [SerializeField] private float _fadeDuration;

        private Tweener _currentTween;

        /// <summary> キャラ詳細ボタン </summary>
        public Button CharacterButton => _characterButton;

        /// <summary> バトルボタン </summary>
        public Button BattleButton => _battleButton;

        /// <summary> タイトルボタン </summary>
        public Button TitleButton => _titleButton;

        /// <summary> 閉じるボタン </summary>
        public Button CloseButton => _closeButton;

        /// <summary>
        /// 入場アニメーション
        /// </summary>
        public UniTask AnimateInAsync(CancellationToken cancellationToken)
        {
            _currentTween?.Kill();

            _currentTween = _panel.DOAnchorPos(_panelPosDefault, _fadeDuration)
                .From(_panelPosHide)
                .SetEase(Ease.OutExpo);

            return _currentTween.ToUniTask(cancellationToken: cancellationToken);
        }

        /// <summary>
        /// 退出アニメーション
        /// </summary>
        public UniTask AnimateOutAsync(CancellationToken cancellationToken)
        {
            _currentTween?.Kill();

            _currentTween = _panel.DOAnchorPos(_panelPosHide, _fadeDuration)
                .From(_panelPosDefault)
                .SetEase(Ease.InExpo);

            return _currentTween.ToUniTask(cancellationToken: cancellationToken);
        }
    }
}