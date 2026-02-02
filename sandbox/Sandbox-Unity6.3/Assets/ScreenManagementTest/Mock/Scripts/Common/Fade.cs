using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace Lilja.ScreenManagement.Mock
{
    public class Fade : MonoBehaviour, ITransition
    {
        #region Singleton

        /// <summary> シングルトン </summary>
        public static Fade Instance => _instance ??= Create();

        private static Fade _instance;

        private static Fade Create()
        {
            var instance = Instantiate(Resources.Load<Fade>("Common/Fade"));
            instance.name = nameof(Fade);
            DontDestroyOnLoad(instance.gameObject);
            return instance;
        }

        #endregion

        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float _fadeDuration;

        private const float CoverAlpha = 1f;
        private const float UncoverAlpha = 0f;

        private Tweener _currentTween;

        /// <summary>
        /// フェードイン
        /// </summary>
        public UniTask InAsync(CancellationToken cancellationToken)
        {
            _currentTween?.Kill();

            _currentTween = _canvasGroup.DOFade(UncoverAlpha, _fadeDuration);

            return _currentTween.ToUniTask(cancellationToken: cancellationToken);
        }

        /// <summary>
        /// フェードアウト
        /// </summary>
        public UniTask OutAsync(CancellationToken cancellationToken)
        {
            _currentTween?.Kill();

            _currentTween = _canvasGroup.DOFade(CoverAlpha, _fadeDuration);

            return _currentTween.ToUniTask(cancellationToken: cancellationToken);
        }

        private void OnDestroy()
        {
            _currentTween?.Kill();
        }
    }
}
