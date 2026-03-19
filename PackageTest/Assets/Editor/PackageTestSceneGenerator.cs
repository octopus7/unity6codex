using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class PackageTestSceneGenerator
{
    private const string SceneDirectory = "Assets/Scenes/Generated";
    private const string ScenePath = SceneDirectory + "/RequestPipelineTestScene.unity";
    private const string AutoGenerateRequestPath = "Assets/Editor/PackageTestSceneGenerator.request";
    private const string DemoPanelTypeName = "CodexSix.RequestPipeline.Debug.RequestPipelineDemoPanel, CodexSix.RequestPipeline";
    private const string RootObjectName = "RequestPipelineTestRoot";
    private const string CanvasObjectName = "RequestPipelineCanvas";
    private const string EventSystemObjectName = "PackageTestEventSystem";

    [InitializeOnLoadMethod]
    private static void RegisterAutoGenerateHook()
    {
        EditorApplication.delayCall += RunPendingAutoGenerateRequest;
    }

    private static void RunPendingAutoGenerateRequest()
    {
        var requestPath = GetAutoGenerateRequestAbsolutePath();
        if (!File.Exists(requestPath))
        {
            return;
        }

        Debug.Log("PackageTestSceneGenerator detected an auto-generate request.");
        File.Delete(requestPath);
        CreateRequestPipelineTestScene();
    }

    [MenuItem("Tools/PackageTest/Create Request Pipeline Test Scene")]
    public static void CreateRequestPipelineTestScene()
    {
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Scenes", "Generated"));
        var scene = File.Exists(GetSceneAbsolutePath())
            ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
            : EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        RebuildGeneratedScene(scene);
    }

    private static void RebuildGeneratedScene(Scene scene)
    {
        DestroyImmediateIfPresent(RootObjectName);
        DestroyImmediateIfPresent(CanvasObjectName);

        var existingEventSystem = GameObject.Find(EventSystemObjectName);
        if (existingEventSystem != null)
        {
            UnityEngine.Object.DestroyImmediate(existingEventSystem);
        }

        var root = new GameObject(RootObjectName);
        var componentType = Type.GetType(DemoPanelTypeName);
        if (componentType == null)
        {
            throw new InvalidOperationException(
                $"Could not resolve package component '{DemoPanelTypeName}'. Check that the local package dependency imported correctly.");
        }

        var component = root.AddComponent(componentType);
        var canvas = CreateCanvas();
        var panel = CreatePanel(canvas.transform);
        var titleLabel = CreateText(panel.transform, "TitleLabel", "Request Pipeline Package", 30, FontStyle.Bold);
        titleLabel.alignment = TextAnchor.UpperLeft;
        SetRect(titleLabel.rectTransform, new Vector2(24f, -24f), new Vector2(-24f, -88f));

        var statusLabel = CreateText(
            panel.transform,
            "StatusLabel",
            "Press Play to auto-send a dummy request.\nUse the button to send another one.",
            18,
            FontStyle.Normal);
        statusLabel.alignment = TextAnchor.UpperLeft;
        statusLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        statusLabel.verticalOverflow = VerticalWrapMode.Overflow;
        SetRect(statusLabel.rectTransform, new Vector2(24f, -96f), new Vector2(-24f, -164f));

        var button = CreateButton(panel.transform, "SendButton", "Send Dummy Request");
        SetFixedRect(button.GetComponent<RectTransform>(), new Vector2(24f, -188f), new Vector2(256f, 56f));

        EnsureEventSystem();
        WireComponentReferences(component, titleLabel, statusLabel, button);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        AssetDatabase.SaveAssets();

        Debug.Log($"Rebuilt package test scene at {ScenePath}");
    }

    private static void WireComponentReferences(Component component, Text titleLabel, Text statusLabel, Button button)
    {
        var serializedObject = new SerializedObject(component);
        serializedObject.FindProperty("_titleLabel").objectReferenceValue = titleLabel;
        serializedObject.FindProperty("_statusLabel").objectReferenceValue = statusLabel;
        serializedObject.FindProperty("_sendButton").objectReferenceValue = button;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Canvas CreateCanvas()
    {
        var canvasObject = new GameObject(
            CanvasObjectName,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var rect = canvasObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return canvas;
    }

    private static RectTransform CreatePanel(Transform parent)
    {
        var panelObject = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        var rect = panelObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(760f, 300f);

        var image = panelObject.GetComponent<Image>();
        image.color = new Color(0.08f, 0.11f, 0.16f, 0.94f);
        return rect;
    }

    private static Text CreateText(Transform parent, string name, string content, int fontSize, FontStyle fontStyle)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        var rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        var text = textObject.GetComponent<Text>();
        text.font = LoadFont();
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = Color.white;
        text.text = content;
        return text;
    }

    private static Button CreateButton(Transform parent, string name, string label)
    {
        var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        var rect = buttonObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        var image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.18f, 0.45f, 0.78f, 1f);

        var button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(buttonObject.transform, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12f, 8f);
        labelRect.offsetMax = new Vector2(-12f, -8f);

        var labelText = labelObject.GetComponent<Text>();
        labelText.font = LoadFont();
        labelText.fontSize = 18;
        labelText.fontStyle = FontStyle.Bold;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = Color.white;
        labelText.text = label;

        return button;
    }

    private static void EnsureEventSystem()
    {
        var existingEventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
        if (existingEventSystem != null)
        {
            existingEventSystem.gameObject.name = EventSystemObjectName;
            return;
        }

        var eventSystemObject = new GameObject(EventSystemObjectName, typeof(EventSystem));
        var inputSystemModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputSystemModuleType != null)
        {
            eventSystemObject.AddComponent(inputSystemModuleType);
            return;
        }

        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    private static void DestroyImmediateIfPresent(string objectName)
    {
        var target = GameObject.Find(objectName);
        if (target != null)
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    private static void SetRect(RectTransform rect, Vector2 topLeft, Vector2 bottomRight)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(topLeft.x, bottomRight.y);
        rect.offsetMax = new Vector2(bottomRight.x, topLeft.y);
    }

    private static void SetFixedRect(RectTransform rect, Vector2 topLeft, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = topLeft;
        rect.sizeDelta = size;
    }

    private static Font LoadFont()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return font;
    }

    private static string GetSceneAbsolutePath()
    {
        return Path.Combine(Application.dataPath, "Scenes", "Generated", "RequestPipelineTestScene.unity");
    }

    private static string GetAutoGenerateRequestAbsolutePath()
    {
        return Path.Combine(Application.dataPath, "Editor", "PackageTestSceneGenerator.request");
    }
}
