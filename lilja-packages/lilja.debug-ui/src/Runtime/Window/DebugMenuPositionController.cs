using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Lilja.DebugUI
{
    /// <summary>
    /// デバッグメニューウィンドウの位置を制御する。
    /// ドラッグによる移動・ダブルクリックによる初期位置リセット・位置の保存と復元を担う。
    /// </summary>
    internal sealed class DebugMenuPositionController
    {
        private readonly VisualElement _window;
        private readonly VisualElement _header;

        // ドラッグ状態
        private bool _isDragging;
        private bool _dragStarted;
        private Vector2 _pointerStartPos;
        private Vector2 _windowStartPos;
        private bool _hasFixedHeight;


        // EditorPrefs / PlayerPrefs キー
        private const string PrefKeyLeft = "LiljaDebugMenu.WindowLeft";
        private const string PrefKeyTop = "LiljaDebugMenu.WindowTop";
        private const string PrefKeyHasPosition = "LiljaDebugMenu.WindowHasPosition";

        internal DebugMenuPositionController(VisualElement window, VisualElement header)
        {
            _window = window;
            _header = header;

            header.RegisterCallback<PointerDownEvent>(OnPointerDown);
            header.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            header.RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        /// <summary>
        /// 保存済み位置があれば復元し、なければデフォルト位置を適用する。
        /// </summary>
        internal void RestoreOrDefault()
        {
            _window.style.position = Position.Absolute;

            if (HasSavedPosition())
            {
                var left = LoadFloat(PrefKeyLeft);
                var top = LoadFloat(PrefKeyTop);
                ApplyRestoredPosition(left, top);
            }
            else
            {
                ApplyDefaultPosition();
            }
        }

        // ── 位置適用 ─────────────────────────────────────────────────

        /// <summary>
        /// 初期位置（左端・上下ストレッチ）を適用する。
        /// </summary>
        private void ApplyDefaultPosition()
        {
            _hasFixedHeight = false;
            _window.style.left = 0f;
            _window.style.top = 0f;
            _window.style.bottom = 0f;
            _window.style.height = StyleKeyword.Auto;
        }

        /// <summary>
        /// 保存済み位置を復元する。height は auto のまま bottom: 0 で下端まで伸ばす。
        /// </summary>
        private void ApplyRestoredPosition(float left, float top)
        {
            _hasFixedHeight = false;
            _window.style.left = left;
            _window.style.top = top;
            _window.style.bottom = 0f;
            _window.style.height = StyleKeyword.Auto;
        }

        /// <summary>
        /// 初期位置へリセットし、保存済み位置を削除する。
        /// </summary>
        internal void ResetToDefault()
        {
            ClearSavedPosition();
            ApplyDefaultPosition();
        }

        // ── イベント処理 ─────────────────────────────────────────────

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0) return;

            // ダブルクリック: 初期位置へリセット
            if (evt.clickCount >= 2)
            {
                ResetToDefault();
                evt.StopPropagation();
                return;
            }

            // ドラッグ準備（実際の移動は DebugMenuSettings.DragThreshold を超えてから開始）
            _isDragging = true;
            _dragStarted = false;
            _pointerStartPos = evt.position;
            _windowStartPos = new Vector2(_window.resolvedStyle.left, _window.resolvedStyle.top);
            _header.CapturePointer(evt.pointerId);
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_isDragging) return;

            var delta = (Vector2)evt.position - _pointerStartPos;

            if (!_dragStarted)
            {
                if (delta.magnitude < DebugMenuSettings.DragThreshold) return;

                // 閾値を超えたらドラッグ開始。bottom: auto + height 固定に切り替える。
                _dragStarted = true;
                if (!_hasFixedHeight)
                {
                    _hasFixedHeight = true;
                    var h = _window.resolvedStyle.height;
                    _window.style.bottom = StyleKeyword.Auto;
                    _window.style.height = h;
                }
            }

            _window.style.left = _windowStartPos.x + delta.x;
            _window.style.top = _windowStartPos.y + delta.y;
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!_isDragging) return;

            _isDragging = false;
            _header.ReleasePointer(evt.pointerId);

            if (!_dragStarted) return;

            ClampPosition();
            SavePosition(_window.resolvedStyle.left, _window.resolvedStyle.top);
            evt.StopPropagation();
        }

        // ── 境界クランプ ──────────────────────────────────────────────

        /// <summary>
        /// ヘッダーが画面外に半分以上出た場合に内側へ押し戻す。
        /// 縦: ヘッダー高さの半分 / 横: ウィンドウ幅の半分 を限界とする。
        /// </summary>
        private void ClampPosition()
        {
            if (_window.panel == null) return;

            var panelRect = _window.panel.visualTree.layout;
            var left = _window.resolvedStyle.left;
            var top = _window.resolvedStyle.top;
            var windowWidth = _window.resolvedStyle.width;
            var headerHeight = _header.resolvedStyle.height;

            // 横方向: ウィンドウ幅の半分以上が画面外に出ないようにする
            left = Mathf.Clamp(left, -(windowWidth * 0.5f), panelRect.width - windowWidth * 0.5f);

            // 縦方向: ヘッダー高さの半分以上が画面外に出ないようにする
            top = Mathf.Clamp(top, -(headerHeight * 0.5f), panelRect.height - headerHeight * 0.5f);

            _window.style.left = left;
            _window.style.top = top;
        }

        // ── 位置の保存・復元（EditorPrefs / PlayerPrefs） ────────────

        private static bool HasSavedPosition()
        {
#if UNITY_EDITOR
            return EditorPrefs.GetBool(PrefKeyHasPosition, false);
#else
            return PlayerPrefs.GetInt(PrefKeyHasPosition, 0) == 1;
#endif
        }

        private static void SavePosition(float left, float top)
        {
#if UNITY_EDITOR
            EditorPrefs.SetBool(PrefKeyHasPosition, true);
            EditorPrefs.SetFloat(PrefKeyLeft, left);
            EditorPrefs.SetFloat(PrefKeyTop, top);
#else
            PlayerPrefs.SetInt(PrefKeyHasPosition, 1);
            PlayerPrefs.SetFloat(PrefKeyLeft, left);
            PlayerPrefs.SetFloat(PrefKeyTop, top);
            PlayerPrefs.Save();
#endif
        }

        internal static void ClearSavedPosition()
        {
#if UNITY_EDITOR
            EditorPrefs.SetBool(PrefKeyHasPosition, false);
#else
            PlayerPrefs.SetInt(PrefKeyHasPosition, 0);
            PlayerPrefs.Save();
#endif
        }

        private static float LoadFloat(string key)
        {
#if UNITY_EDITOR
            return EditorPrefs.GetFloat(key, 0f);
#else
            return PlayerPrefs.GetFloat(key, 0f);
#endif
        }
    }
}
