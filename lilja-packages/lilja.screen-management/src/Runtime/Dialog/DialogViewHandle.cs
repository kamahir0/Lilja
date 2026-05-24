using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// Dialog用の IViewHandle 実装クラス。
    /// </summary>
    public sealed class DialogViewHandle : IViewHandle
    {
        private readonly string _frameKey;
        private readonly string _contentKey;
        private readonly bool _useBackdrop;
        private readonly Func<GameObject> _fallbackFrameFactory;
        private readonly Func<GameObject> _fallbackContentFactory;

        private GameObject _root;

        /// <summary>
        /// Frame の RectTransform を取得します。
        /// </summary>
        public RectTransform FrameRectTransform { get; private set; }

        /// <summary>
        /// Content の RectTransform を取得します。
        /// </summary>
        public RectTransform ContentRectTransform { get; private set; }

        #region IViewHandle

        /// <inheritdoc />
        public GameObject[] RootObjects => new[] { _root };

        /// <inheritdoc />
        public bool IsLoaded => _root != null;

        /// <inheritdoc />
        public bool IsUnloadedTemporarily { get; set; }

        /// <inheritdoc />
        public bool UnloadsAncestors => false;

        /// <summary>
        /// 新しい <see cref="DialogViewHandle"/> インスタンスを初期化します。
        /// </summary>
        /// <param name="frameKey">フレームアセットのキー</param>
        /// <param name="contentKey">コンテンツアセットのキー</param>
        /// <param name="useBackdrop">背景イメージを使用するかどうか</param>
        /// <param name="fallbackFrameFactory">フォールバック用フレームの生成ファクトリ</param>
        /// <param name="fallbackContentFactory">フォールバック用コンテンツの生成ファクトリ</param>
        public DialogViewHandle(
            string frameKey,
            string contentKey,
            bool useBackdrop,
            Func<GameObject> fallbackFrameFactory,
            Func<GameObject> fallbackContentFactory
        )
        {
            _frameKey = frameKey;
            _contentKey = contentKey;
            _useBackdrop = useBackdrop;
            _fallbackFrameFactory = fallbackFrameFactory;
            _fallbackContentFactory = fallbackContentFactory;
        }

        /// <inheritdoc />
        public void Initialize(Type ownerType)
        {
            // キーはコンストラクタで直接指定されるため追加の自動解決は不要です
        }

        /// <inheritdoc />
        public async UniTask PreloadAsync(
            GameScreenContext context,
            CancellationToken cancellationToken
        )
        {
            var provider = context.Options.PrefabProvider;
            await UniTask.WhenAll(
                provider.LoadAsync(_frameKey, cancellationToken).SuppressCancellationThrow(),
                provider.LoadAsync(_contentKey, cancellationToken).SuppressCancellationThrow()
            );
        }

        /// <inheritdoc />
        public async UniTask LoadAsync(
            GameScreenContext context,
            CancellationToken cancellationToken
        )
        {
            if (IsLoaded)
            {
                return;
            }

            // Root生成
            _root = CreateRoot();

            // Backdrop生成
            if (_useBackdrop)
            {
                BackdropUtility.Create(_root.transform);
            }

            // OutsideButton生成
            OutsideButtonUtility.Create(_root.transform, _useBackdrop);

            // Frame / Content ロードとインスタンス化
            var provider = context.Options.PrefabProvider;

            GameObject framePrefab = null;
            GameObject contentPrefab = null;
            try
            {
                var results = await UniTask.WhenAll(
                    provider.LoadAsync(_frameKey, cancellationToken).SuppressCancellationThrow(),
                    provider.LoadAsync(_contentKey, cancellationToken).SuppressCancellationThrow()
                );

                if (!results.Item1.IsCanceled)
                {
                    framePrefab = results.Item1.Result;
                }
                if (!results.Item2.IsCanceled)
                {
                    contentPrefab = results.Item2.Result;
                }
            }
            catch (Exception ex)
            {
                // フォールバックで対応するため警告ログのみ残して進めます
                Debug.LogWarning(
                    $"[Lilja.ScreenManagement.Dialog] ダイアログプレハブのロードに失敗しました。フォールバックUIを生成します。エラー: {ex.Message}"
                );
            }

            // Frame生成
            var frameGo =
                framePrefab == null
                    ? _fallbackFrameFactory?.Invoke()
                    : Object.Instantiate(framePrefab);
            if (frameGo == null)
            {
                Debug.LogError(
                    "[Lilja.ScreenManagement.Dialog] ダイアログフレームのインスタンス化に失敗しました。"
                );
                return;
            }

            FrameRectTransform = frameGo.GetComponent<RectTransform>();
            if (FrameRectTransform == null)
            {
                Debug.LogError(
                    "[Lilja.ScreenManagement.Dialog] 生成されたダイアログフレームに RectTransform が見つかりません。"
                );
                return;
            }

            // FrameをRootの子にする
            FrameRectTransform.SetParent(_root.transform, false);
            FrameRectTransform.SetAsLastSibling(); // 順序保障: Backdrop(0) -> Outside(1) -> Frame(2)

            // Content生成
            var contentGo =
                contentPrefab == null
                    ? _fallbackContentFactory?.Invoke()
                    : Object.Instantiate(contentPrefab);

            if (contentGo == null)
            {
                Debug.LogError(
                    "[Lilja.ScreenManagement.Dialog] ダイアログコンテンツのインスタンス化に失敗しました。"
                );
                return;
            }

            ContentRectTransform = contentGo.GetComponent<RectTransform>();
            if (ContentRectTransform == null)
            {
                Debug.LogError(
                    "[Lilja.ScreenManagement.Dialog] 生成されたダイアログコンテンツに RectTransform が見つかりません。"
                );
                return;
            }

            // ContentをFrameの子にする
            SetContentParent(ContentRectTransform, FrameRectTransform);
        }

        /// <inheritdoc />
        public void Unload()
        {
            if (_root != null)
            {
                Object.Destroy(_root);
                _root = null;
            }
            FrameRectTransform = null;
            ContentRectTransform = null;
        }

        #endregion

        /// <summary> Rootを生成する </summary>
        private static GameObject CreateRoot()
        {
            var root = DialogRootUtility.Create();

            // 現在ロードされている最後尾の有効なシーンにオブジェクトを移動してツリー階層を保護
            if (SceneManager.sceneCount > 0)
            {
                var activeScene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
                if (activeScene.IsValid() && activeScene.isLoaded)
                {
                    SceneManager.MoveGameObjectToScene(root, activeScene);
                }
            }
            return root;
        }

        /// <summary> ContentをFrameの子にする </summary>
        private static void SetContentParent(RectTransform content, RectTransform frame)
        {
            if (content == null || frame == null)
            {
                Debug.LogError(
                    "[Lilja.ScreenManagement.Dialog] SetContentParent: content または frame が null のためアタッチをスキップします。"
                );
                return;
            }

            var frameComponent = frame.GetComponent<IDialogFrame>();
            if (frameComponent != null && frameComponent.ContentContainer != null)
            {
                content.SetParent(frameComponent.ContentContainer, false);

                // ContentContainer に合わせて全画面に広げる
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
