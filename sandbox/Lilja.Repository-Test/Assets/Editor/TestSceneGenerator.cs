using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Lilja.Repository.Test;

public static class TestSceneGenerator
{
    [MenuItem("Lilja/Tests/Generate Repository Test Scene")]
    public static void GenerateScene()
    {
        // Create new scene
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "RepositoryTest";

        // Create Main Camera
        var cameraGo = new GameObject("Main Camera");
        var camera = cameraGo.AddComponent<Camera>();
        cameraGo.tag = "MainCamera";
        cameraGo.transform.position = new Vector3(0, 0, -10);
        camera.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
        camera.clearFlags = CameraClearFlags.SolidColor;

        // Create EventSystem
        var eventSystemGo = new GameObject("EventSystem");
        eventSystemGo.AddComponent<EventSystem>();
        eventSystemGo.AddComponent<StandaloneInputModule>();

        // Create Canvas
        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        // Panel
        var panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        var img = panelGo.AddComponent<Image>();
        img.color = new Color(0, 0, 0, 0.5f);
        var rect = panelGo.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // UI Elements
        var idInput = CreateInputField(panelGo.transform, "IDInput", "ID", 0);
        var xInput = CreateInputField(panelGo.transform, "XInput", "Value X", 1);
        var yInput = CreateInputField(panelGo.transform, "YInput", "Value Y", 2);
        var descInput = CreateInputField(panelGo.transform, "DescInput", "Description", 3);

        var saveBtn = CreateButton(panelGo.transform, "SaveButton", "Save", 4);
        var loadBtn = CreateButton(panelGo.transform, "LoadButton", "Load", 5);
        var deleteBtn = CreateButton(panelGo.transform, "DeleteButton", "Delete", 6);

        var logTextGo = DefaultControls.CreateText(new DefaultControls.Resources());
        logTextGo.name = "LogText";
        logTextGo.transform.SetParent(panelGo.transform, false);
        var logText = logTextGo.GetComponent<Text>();
        logText.text = "Log...";
        logText.color = Color.white;
        var logRect = logTextGo.GetComponent<RectTransform>();
        logRect.sizeDelta = new Vector2(400, 200);
        logRect.anchoredPosition = new Vector2(0, -150);

        // Controller
        var controllerGo = new GameObject("RepositoryTestController");
        var controller = controllerGo.AddComponent<RepositoryTestController>();

        var so = new SerializedObject(controller);
        so.FindProperty("_idInput").objectReferenceValue = idInput;
        so.FindProperty("_valueXInput").objectReferenceValue = xInput;
        so.FindProperty("_valueYInput").objectReferenceValue = yInput;
        so.FindProperty("_descInput").objectReferenceValue = descInput;
        so.FindProperty("_saveButton").objectReferenceValue = saveBtn;
        so.FindProperty("_loadButton").objectReferenceValue = loadBtn;
        so.FindProperty("_deleteButton").objectReferenceValue = deleteBtn;
        so.FindProperty("_logText").objectReferenceValue = logText;
        so.ApplyModifiedProperties();

        // Save Scene
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
        {
            AssetDatabase.CreateFolder("Assets", "Scenes");
        }
        var scenePath = "Assets/Scenes/RepositoryTest.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log($"Scene generated at {scenePath}");
    }

    private static InputField CreateInputField(Transform parent, string name, string placeholder, int index)
    {
        var go = DefaultControls.CreateInputField(new DefaultControls.Resources());
        go.name = name;
        go.transform.SetParent(parent, false);
        var input = go.GetComponent<InputField>();
        input.placeholder.GetComponent<Text>().text = placeholder;

        var rect = go.GetComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(0, 200 - index * 40);

        return input;
    }

    private static Button CreateButton(Transform parent, string name, string label, int index)
    {
        var go = DefaultControls.CreateButton(new DefaultControls.Resources());
        go.name = name;
        go.transform.SetParent(parent, false);
        var btn = go.GetComponent<Button>();
        go.GetComponentInChildren<Text>().text = label;

        var rect = go.GetComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(0, 200 - index * 40);

        return btn;
    }
}
