/*
 * FancyScrollView (https://github.com/setchi/FancyScrollView)
 * Copyright (c) 2020 setchi
 * Licensed under MIT (https://github.com/setchi/FancyScrollView/blob/master/LICENSE)
 */

using System;
using UnityEditor;
using Lilja.FancyScrollView;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lilja.FancyScrollView.Editor
{
    [CustomEditor(typeof(FancyScrollViewBase), true)]
    public sealed class FancyScrollViewPreviewEditor : UnityEditor.Editor
    {
        SerializedProperty previewItemCount;

        bool previewEnabled;
        bool previewPlaying;
        bool previewReverse;
        double previousTime;
        float previewPosition;
        float previewSpeed = 1f;
        string previewException;

        FancyScrollViewBase View => target as FancyScrollViewBase;

        void OnEnable()
        {
            previewItemCount = serializedObject.FindProperty("editorPreviewItemCount");
            previousTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            PrefabStage.prefabStageClosing += OnPrefabStageClosing;
        }

        void OnDisable()
        {
            StopPreview(true);
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            PrefabStage.prefabStageClosing -= OnPrefabStageClosing;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var script = serializedObject.FindProperty("m_Script");
            if (script != null)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(script);
                }
            }

            EditorGUI.BeginChangeCheck();
            DrawPropertiesExcluding(serializedObject, "m_Script", "editorPreviewItemCount");
            var inspectorChanged = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            DrawPreviewPanel(inspectorChanged);
        }

        void DrawPreviewPanel(bool inspectorChanged)
        {
            var view = View;
            if (view == null)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

                if (Application.isPlaying)
                {
                    StopPreview(true);
                    EditorGUILayout.HelpBox("Preview is only available in Edit Mode.", MessageType.Info);
                    return;
                }

                EditorGUI.BeginChangeCheck();
                var enabled = EditorGUILayout.Toggle("Enable", previewEnabled);
                if (EditorGUI.EndChangeCheck())
                {
                    if (enabled)
                    {
                        StartPreview(true);
                        previewPlaying = false;
                    }
                    else
                    {
                        StopPreview(true);
                    }
                }

                serializedObject.Update();
                EditorGUI.BeginChangeCheck();
                using (new EditorGUI.DisabledScope(!previewEnabled))
                {
                    if (previewItemCount != null)
                    {
                        EditorGUILayout.PropertyField(previewItemCount, new GUIContent("Preview Item Count"));
                    }
                }
                var previewSettingsChanged = EditorGUI.EndChangeCheck();
                serializedObject.ApplyModifiedProperties();

                if (targets.Length > 1)
                {
                    EditorGUILayout.HelpBox("Preview supports one selected scroll view at a time.", MessageType.Info);
                    return;
                }

                var error = view.GetEditorPreviewError();
                if (!string.IsNullOrEmpty(error))
                {
                    if (previewEnabled || view.EditorPreviewing)
                    {
                        StopPreview(false);
                    }

                    EditorGUILayout.HelpBox(error, MessageType.Info);
                    return;
                }

                if (!string.IsNullOrEmpty(previewException))
                {
                    EditorGUILayout.HelpBox(previewException, MessageType.Error);
                }

                using (new EditorGUI.DisabledScope(!previewEnabled))
                {
                    var maxPosition = view.GetEditorPreviewMaxPosition();
                    previewPosition = Mathf.Clamp(previewPosition, 0f, maxPosition);

                    EditorGUI.BeginChangeCheck();
                    previewPosition = EditorGUILayout.Slider("Position", previewPosition, 0f, maxPosition);
                    if (EditorGUI.EndChangeCheck())
                    {
                        previewPlaying = false;
                        TryUpdatePreview(false);
                    }
                }

                using (new EditorGUI.DisabledScope(!previewEnabled))
                {
                    previewSpeed = EditorGUILayout.Slider("Speed", previewSpeed, 0.1f, 10f);
                }

                using (new EditorGUI.DisabledScope(!previewEnabled))
                {
                    previewReverse = EditorGUILayout.Toggle("Reverse", previewReverse);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(previewPlaying ? "Pause" : "Play"))
                    {
                        if (previewPlaying)
                        {
                            previewPlaying = false;
                        }
                        else
                        {
                            if (!previewEnabled)
                            {
                                StartPreview(true);
                            }

                            if (previewEnabled)
                            {
                                previewPlaying = true;
                            }
                        }

                        previousTime = EditorApplication.timeSinceStartup;
                    }

                    using (new EditorGUI.DisabledScope(!previewEnabled))
                    {
                        if (GUILayout.Button("Restart"))
                        {
                            previewPosition = 0f;
                            TryUpdatePreview(true);
                            previewPlaying = true;
                            previousTime = EditorApplication.timeSinceStartup;
                        }

                        if (GUILayout.Button("Stop"))
                        {
                            StopPreview(true);
                        }
                    }
                }

                if ((inspectorChanged || previewSettingsChanged) && previewEnabled)
                {
                    TryUpdatePreview(true);
                }
            }
        }

        void OnEditorUpdate()
        {
            if (!previewEnabled || !previewPlaying)
            {
                previousTime = EditorApplication.timeSinceStartup;
                return;
            }

            var view = View;
            if (view == null)
            {
                StopPreview(false);
                return;
            }

            var time = EditorApplication.timeSinceStartup;
            var deltaTime = Mathf.Max(0f, (float)(time - previousTime));
            previousTime = time;

            var maxPosition = view.GetEditorPreviewMaxPosition();
            if (maxPosition > 0f)
            {
                previewPosition += deltaTime * previewSpeed * (previewReverse ? -1f : 1f);
                while (previewPosition > maxPosition)
                {
                    previewPosition -= maxPosition;
                }

                while (previewPosition < 0f)
                {
                    previewPosition += maxPosition;
                }
            }
            else
            {
                previewPosition = 0f;
            }

            TryUpdatePreview(false);
            Repaint();
        }

        void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                StopPreview(true);
            }
            else if (state == PlayModeStateChange.EnteredPlayMode)
            {
                StopPreview(true);
                Repaint();
            }
        }

        void OnPrefabStageClosing(PrefabStage prefabStage)
        {
            StopPreview(true);
        }

        void StartPreview(bool forceRefresh)
        {
            var view = View;
            if (view == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                StopPreview(true);
                previewException = "Preview is only available in Edit Mode.";
                return;
            }

            previewEnabled = true;
            previewException = null;
            previousTime = EditorApplication.timeSinceStartup;

            try
            {
                view.BeginEditorPreview();
                TryUpdatePreview(forceRefresh);
            }
            catch (Exception exception)
            {
                previewException = FormatException(exception);
                previewPlaying = false;
                previewEnabled = false;
                view.EndEditorPreview();
            }
        }

        bool TryUpdatePreview(bool forceRefresh)
        {
            var view = View;
            if (!previewEnabled || view == null)
            {
                return false;
            }

            if (Application.isPlaying)
            {
                previewException = "Preview is only available in Edit Mode.";
                previewPlaying = false;
                previewEnabled = false;
                if (view.EditorPreviewing)
                {
                    view.EndEditorPreview();
                }

                return false;
            }

            var error = view.GetEditorPreviewError();
            if (!string.IsNullOrEmpty(error))
            {
                previewException = error;
                previewPlaying = false;
                previewEnabled = false;
                view.EndEditorPreview();
                return false;
            }

            try
            {
                view.UpdateEditorPreview(previewPosition, forceRefresh);
                previewException = null;
                return true;
            }
            catch (Exception exception)
            {
                previewException = FormatException(exception);
                previewPlaying = false;
                previewEnabled = false;
                view.EndEditorPreview();
                return false;
            }
        }

        void StopPreview(bool resetPosition)
        {
            var view = View;
            if (view != null && view.EditorPreviewing)
            {
                view.EndEditorPreview();
            }

            previewEnabled = false;
            previewPlaying = false;
            previewException = null;

            if (resetPosition)
            {
                previewPosition = 0f;
            }
        }

        static string FormatException(Exception exception)
        {
            return string.Format("{0}: {1}", exception.GetType().Name, exception.Message);
        }
    }
}
