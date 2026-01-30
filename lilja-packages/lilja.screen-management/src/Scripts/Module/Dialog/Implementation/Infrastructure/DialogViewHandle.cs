using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// Dialog用の IViewHandle 実装
    /// </summary>
    public class DialogViewHandle : IViewHandle
    {
        private readonly IPrefabHandle _framePrefabHandle;
        private readonly IPrefabHandle _contentPrefabHandle;
        private readonly bool _useBackdrop;
        private readonly Func<GameObject> _fallbackFrameFactory;
        private readonly Func<GameObject> _fallbackContentFactory;

        private GameObject _root;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public DialogViewHandle(
            IPrefabHandle framePrefabHandle,
            IPrefabHandle contentPrefabHandle,
            bool useBackdrop,
            Func<GameObject> fallbackFrameFactory,
            Func<GameObject> fallbackContentFactory)
        {
            _framePrefabHandle = framePrefabHandle;
            _contentPrefabHandle = contentPrefabHandle;
            _useBackdrop = useBackdrop;
            _fallbackFrameFactory = fallbackFrameFactory;
            _fallbackContentFactory = fallbackContentFactory;
        }

        /// <summary> FrameのRectTransform </summary>
        public RectTransform FrameRectTransform { get; private set; }

        /// <summary> ContentのRectTransform </summary>
        public RectTransform ContentRectTransform { get; private set; }

        #region IViewHandle

        /// <inheritdoc/>
        public GameObject[] RootObjects => new[] { _root };

        /// <inheritdoc/>
        public async UniTask LoadAsync(CancellationToken cancellationToken)
        {
            // Root生成
            _root = CreateRoot();

            // Backdrop生成
            if (_useBackdrop) BackdropUtility.Create(_root.transform);

            // OutsideButton生成
            OutsideButtonUtility.Create(_root.transform, _useBackdrop);

            // Frameプレハブロード
            var framePrefab = await _framePrefabHandle.LoadAsync(cancellationToken);

            // Frame生成
            FrameRectTransform = (RectTransform)(framePrefab == null
                ? _fallbackFrameFactory()
                : Object.Instantiate(framePrefab)).transform;

            // FrameをRootの子にする
            FrameRectTransform.SetParent(_root.transform, false);
            FrameRectTransform.SetAsLastSibling(); // 順序保障: Backdrop(0) -> Outside(1) -> Frame(2)

            // Contentプレハブロード
            var contentPrefab = await _contentPrefabHandle.LoadAsync(cancellationToken);

            // Content生成
            ContentRectTransform = (RectTransform)(contentPrefab == null
                ? _fallbackContentFactory()
                : Object.Instantiate(contentPrefab)).transform;

            // ContentをFrameの子にする
            SetContentParent(ContentRectTransform, FrameRectTransform);
        }

        /// <inheritdoc/>
        public void Unload()
        {
            Object.Destroy(_root);
            _root = null;
            _framePrefabHandle.Release();
            _contentPrefabHandle.Release();
        }

        #endregion

        /// <summary> Rootを生成する </summary>
        private static GameObject CreateRoot()
        {
            // ルート作成
            var root = DialogRootUtility.Create();
            // PrefabOverlayシーンに移動
            var prefabOverlayScene = PrefabOverlaySceneUtility.GetOrCreate();
            SceneManager.MoveGameObjectToScene(root, prefabOverlayScene);
            return root;
        }

        /// <summary> ContentをFrameの子にする </summary>
        private static void SetContentParent(RectTransform content, RectTransform frame)
        {
            var frameComponent = frame.GetComponent<IDialogFrame>();
            if (frameComponent != null && frameComponent.ContentContainer != null)
            {
                content.SetParent(frameComponent.ContentContainer, false);

                // ContentContainer に合わせて全画面に広げる
                if (content != null)
                {
                    content.anchorMin = Vector2.zero;
                    content.anchorMax = Vector2.one;
                    content.offsetMin = Vector2.zero;
                    content.offsetMax = Vector2.zero;
                    content.pivot = new Vector2(0.5f, 0.5f);
                }
                else
                {
                    // Fallback: Frame直下に配置
                    content.SetParent(frame, false);
                }
            }
        }
    }
}
