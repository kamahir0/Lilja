using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
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
        private readonly Func<GameObject> _fallbackFrameFactory;
        private readonly Func<GameObject> _fallbackContentFactory;

        private GameObject _root;

        /// <summary>
        /// 背景イメージを使用するかどうか。
        /// </summary>
        public bool UseBackdrop { get; set; } = true;

        /// <summary>
        /// Frame の RectTransform を取得します。
        /// </summary>
        public RectTransform FrameRectTransform { get; private set; }

        /// <summary>
        /// Content の RectTransform を取得します。
        /// </summary>
        public RectTransform ContentRectTransform { get; private set; }

        #region IViewHandle

        private GameObject[] _rootObjects = Array.Empty<GameObject>();

        /// <inheritdoc />
        public GameObject[] RootObjects => _rootObjects;

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
            Func<GameObject> fallbackFrameFactory,
            Func<GameObject> fallbackContentFactory
        )
        {
            _frameKey = frameKey;
            _contentKey = contentKey;
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
            var provider = context.PrefabProvider;
            try
            {
                await UniTask.WhenAll(
                    provider.LoadAsync(_frameKey, cancellationToken).SuppressCancellationThrow(),
                    provider.LoadAsync(_contentKey, cancellationToken).SuppressCancellationThrow()
                );
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // フォールバックで対応するため、警告ログのみ残して例外は投げない
                Debug.LogWarning(
                    $"[Lilja.ScreenManagement.Dialog] PreloadAsync: ダイアログプレハブのロードに失敗しました。フォールバック処理を行います。エラー: {ex.Message}"
                );
            }
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
            _rootObjects = new[] { _root };

            try
            {
                // GameScreens シーンへ移動
                var targetScene = await GameScreenSceneUtility.GetOrCreateSceneAsync(
                    cancellationToken
                );
                if (targetScene.IsValid() && targetScene.isLoaded && _root != null)
                {
                    SceneManager.MoveGameObjectToScene(_root, targetScene);
                }

                // Backdrop生成
                if (UseBackdrop)
                {
                    BackdropUtility.Create(_root.transform);
                }

                // OutsideButton生成
                OutsideButtonUtility.Create(_root.transform, UseBackdrop);

                // Frame / Content ロードとインスタンス化
                var provider = context.PrefabProvider;

                GameObject framePrefab = null;
                GameObject contentPrefab = null;
                try
                {
                    var results = await UniTask.WhenAll(
                        provider
                            .LoadAsync(_frameKey, cancellationToken)
                            .SuppressCancellationThrow(),
                        provider
                            .LoadAsync(_contentKey, cancellationToken)
                            .SuppressCancellationThrow()
                    );

                    // いずれかがキャンセルされていた場合はフォールバックを生成せずに中断する。
                    // キャンセルされた状態でフォールバックUIを生成すると、呼び出し元が既に
                    // キャンセル済みであるにもかかわらずダイアログが画面に残り続けるバグの原因となる。
                    if (results.Item1.IsCanceled || results.Item2.IsCanceled)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        // ThrowIfCancellationRequested が通過した場合（トークン自体は未キャンセルだが
                        // SuppressCancellationThrow が IsCanceled を返した異常ケース）は例外を投げて早期脱出
                        throw new OperationCanceledException(cancellationToken);
                    }

                    framePrefab = results.Item1.Result;
                    contentPrefab = results.Item2.Result;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // フォールバックで対応するが、アセットロードに失敗した原因究明のために必ずログを出力する
                    Debug.LogWarning(
                        $"[Lilja.ScreenManagement.Dialog] LoadAsync: ダイアログプレハブのロードに失敗しました。フォールバック処理を行います。エラー: {ex.Message}\n{ex.StackTrace}"
                    );
                }

                // Frame生成
                var frameGo =
                    framePrefab == null
                        ? _fallbackFrameFactory?.Invoke()
                        : Object.Instantiate(framePrefab);
                if (frameGo == null)
                {
                    throw new InvalidOperationException(
                        $"[Lilja.ScreenManagement.Dialog] ダイアログフレーム '{_frameKey}' のインスタンス化に失敗しました（プレハブ・フォールバック共に生成不可）。"
                    );
                }

                FrameRectTransform = frameGo.GetComponent<RectTransform>();
                if (FrameRectTransform == null)
                {
                    throw new InvalidOperationException(
                        $"[Lilja.ScreenManagement.Dialog] 生成されたダイアログフレーム '{_frameKey}' に RectTransform が見つかりません。"
                    );
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
                    throw new InvalidOperationException(
                        $"[Lilja.ScreenManagement.Dialog] ダイアログコンテンツ '{_contentKey}' のインスタンス化に失敗しました（プレハブ・フォールバック共に生成不可）。"
                    );
                }

                ContentRectTransform = contentGo.GetComponent<RectTransform>();
                if (ContentRectTransform == null)
                {
                    throw new InvalidOperationException(
                        $"[Lilja.ScreenManagement.Dialog] 生成されたダイアログコンテンツ '{_contentKey}' に RectTransform が見つかりません。"
                    );
                }

                // ContentをFrameの子にする
                SetContentParent(ContentRectTransform, FrameRectTransform);
            }
            catch (Exception)
            {
                if (_root != null)
                {
                    Object.Destroy(_root);
                    _root = null;
                }
                _rootObjects = Array.Empty<GameObject>();
                FrameRectTransform = null;
                ContentRectTransform = null;
                throw;
            }
        }

        /// <inheritdoc />
        public UniTask UnloadAsync(CancellationToken cancellationToken)
        {
            if (_root != null)
            {
                Object.Destroy(_root);
                _root = null;
            }
            _rootObjects = Array.Empty<GameObject>();
            FrameRectTransform = null;
            ContentRectTransform = null;
            return UniTask.CompletedTask;
        }

        #endregion

        /// <summary> Rootを生成する </summary>
        private static GameObject CreateRoot()
        {
            var root = new GameObject("Dialog");

            // Canvas の自動アタッチと設定
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // 基準解像度 1920x1080 に基づくマルチ解像度対応スケーラーの設定
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // UI入力を可能にするための GraphicRaycaster 設定
            root.AddComponent<GraphicRaycaster>();

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
