using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Lilja.ScreenManagement;
using UnityEngine;

namespace ScreenManagementSample.Presentation
{
    /// <summary>
    /// フェードトランジション
    /// </summary>
    public class SampleFade : MonoBehaviour, ITransition
    {
        #region Singleton

        public static SampleFade Instance => _instance ??= Create();
        private static SampleFade _instance;

        private static SampleFade Create()
        {
            var prefab = Resources.Load<SampleFade>("Common/SampleFade");
            if (prefab == null)
            {
                // フォールバック: プログラムで生成
                var go = new GameObject(nameof(SampleFade));
                var canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 9999;
                var canvasGroup = go.AddComponent<CanvasGroup>();

                var instance = go.AddComponent<SampleFade>();
                instance._canvasGroup = canvasGroup;
                instance._fadeDuration = 0.3f;
                instance._canvasGroup.alpha = 1f;
                DontDestroyOnLoad(go);
                return instance;
            }

            var fadeInstance = Instantiate(prefab);
            fadeInstance.name = nameof(SampleFade);
            DontDestroyOnLoad(fadeInstance.gameObject);
            return fadeInstance;
        }

        #endregion

        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float _fadeDuration = 0.3f;

        private Tweener _currentTween;

        /// <summary>
        /// フェードイン（画面を表示）
        /// </summary>
        public UniTask InAsync(CancellationToken cancellationToken)
        {
            _currentTween?.Kill();
            _currentTween = _canvasGroup.DOFade(0f, _fadeDuration);
            return _currentTween.ToUniTask(cancellationToken: cancellationToken);
        }

        /// <summary>
        /// フェードアウト（画面を隠す）
        /// </summary>
        public UniTask OutAsync(CancellationToken cancellationToken)
        {
            _currentTween?.Kill();
            _currentTween = _canvasGroup.DOFade(1f, _fadeDuration);
            return _currentTween.ToUniTask(cancellationToken: cancellationToken);
        }

        private void OnDestroy()
        {
            _currentTween?.Kill();
        }
    }
}
