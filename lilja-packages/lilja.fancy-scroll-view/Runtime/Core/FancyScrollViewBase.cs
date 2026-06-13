/*
 * FancyScrollView (https://github.com/setchi/FancyScrollView)
 * Copyright (c) 2020 setchi
 * Licensed under MIT (https://github.com/setchi/FancyScrollView/blob/master/LICENSE)
 */

using UnityEngine;

namespace Lilja.FancyScrollView
{
    /// <summary>
    /// Non-generic base class for editor integrations.
    /// </summary>
    public abstract class FancyScrollViewBase : MonoBehaviour
    {
        [SerializeField, Min(1)] int editorPreviewItemCount = 30;

        /// <summary>
        /// Item count used by the built-in editor preview.
        /// </summary>
        protected int EditorPreviewItemCount => Mathf.Max(1, editorPreviewItemCount);

#if UNITY_EDITOR
        internal static readonly HideFlags EditorPreviewHideFlags =
            HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

        internal abstract bool EditorPreviewing { get; }

        internal abstract string GetEditorPreviewError();

        internal abstract float GetEditorPreviewMaxPosition();

        internal abstract void BeginEditorPreview();

        internal abstract void UpdateEditorPreview(float position, bool forceRefresh);

        internal abstract void EndEditorPreview();
#endif
    }
}
