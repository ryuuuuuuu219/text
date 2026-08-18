using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class AngularAccelerationVisualizationSceneBuilder
{
    const string ScenePath = "Assets/Scenes/AngularAccelerationVisualization.unity";
    static readonly Vector2 CanvasSize = new(1024f, 768f);

    static AngularAccelerationVisualizationSceneBuilder()
    {
        EditorApplication.delayCall += BuildMissingScene;
    }

    static void BuildMissingScene()
    {
        if (Application.isBatchMode ||
            AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            return;
        BuildScene();
    }

    [MenuItem("Tools/MVP/Build Angular Acceleration Visualization")]
    public static void BuildScene()
    {
        Scene previousScene = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        scene.name = "AngularAccelerationVisualization";

        Camera camera = CreateCamera();
        Canvas canvas = CreateCanvas(camera);
        CreateEventSystem();
        CreateTrajectory(canvas.transform);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.CloseScene(scene, true);
        if (previousScene.IsValid() && previousScene.isLoaded)
            SceneManager.SetActiveScene(previousScene);
        Debug.Log("Angular acceleration visualization generated: " + ScenePath);
    }

    public static void BuildFromCommandLine()
    {
        BuildScene();
    }

    static Camera CreateCamera()
    {
        GameObject cameraObject = new("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = CanvasSize.y * 0.5f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 2000f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.025f, 0.035f, 0.055f, 1f);
        cameraObject.transform.position = new Vector3(0f, 0f, -1000f);
        return camera;
    }

    static Canvas CreateCanvas(Camera camera)
    {
        GameObject canvasObject = new("Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = camera;
        canvas.sortingOrder = 10;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 1f;
        canvasObject.AddComponent<GraphicRaycaster>();

        RectTransform rect = canvas.GetComponent<RectTransform>();
        rect.sizeDelta = CanvasSize;
        rect.position = Vector3.zero;
        return canvas;
    }

    static void CreateEventSystem()
    {
        GameObject eventSystemObject = new("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
    }

    static void CreateTrajectory(Transform canvasTransform)
    {
        GameObject lineObject = new("Trajectory");
        lineObject.transform.SetParent(canvasTransform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.widthMultiplier = 4f;
        line.numCapVertices = 4;
        line.numCornerVertices = 2;
        line.alignment = LineAlignment.TransformZ;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sortingOrder = 0;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        line.sharedMaterial = new Material(shader) { color = new Color(0.1f, 0.9f, 1f, 1f) };
        line.startColor = new Color(0.1f, 0.9f, 1f, 1f);
        line.endColor = new Color(1f, 0.85f, 0.15f, 1f);

        AngularAccelerationTrajectoryVisualizer visualizer =
            lineObject.AddComponent<AngularAccelerationTrajectoryVisualizer>();
        visualizer.canvasSize = CanvasSize;

        RectTransform panel = CreatePanel(canvasTransform);
        InputField speedInput = CreateInputField(panel, "Speed Input", "速度 px/s", new Vector2(-270f, 0f));
        InputField angularInput = CreateInputField(panel, "Angular Velocity Input", "角速度 deg/s", new Vector2(30f, 0f));
        Text status = CreateText(panel, "Status", "", new Vector2(250f, 0f), new Vector2(450f, 58f), 18, TextAnchor.MiddleLeft);
        visualizer.Configure(speedInput, angularInput, status);
        visualizer.RebuildTrajectory();
    }

    static RectTransform CreatePanel(Transform parent)
    {
        GameObject panelObject = new("Control Panel");
        panelObject.transform.SetParent(parent, false);
        Image image = panelObject.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.72f);
        RectTransform rect = image.rectTransform;
        rect.sizeDelta = new Vector2(1000f, 72f);
        rect.anchoredPosition = new Vector2(0f, 338f);
        return rect;
    }

    static InputField CreateInputField(RectTransform parent, string name, string label,
        Vector2 position)
    {
        CreateText(parent, name + " Label", label, position + new Vector2(-70f, 0f),
            new Vector2(135f, 50f), 18, TextAnchor.MiddleRight);

        GameObject inputObject = new(name);
        inputObject.transform.SetParent(parent, false);
        Image background = inputObject.AddComponent<Image>();
        background.color = new Color(1f, 1f, 1f, 0.16f);
        InputField input = inputObject.AddComponent<InputField>();
        input.contentType = InputField.ContentType.DecimalNumber;
        RectTransform rect = background.rectTransform;
        rect.sizeDelta = new Vector2(115f, 42f);
        rect.anchoredPosition = position + new Vector2(65f, 0f);

        Text text = CreateText(rect, "Text", "0", Vector2.zero,
            new Vector2(100f, 38f), 20, TextAnchor.MiddleCenter);
        Text placeholder = CreateText(rect, "Placeholder", "0", Vector2.zero,
            new Vector2(100f, 38f), 20, TextAnchor.MiddleCenter);
        placeholder.color = new Color(1f, 1f, 1f, 0.3f);
        input.textComponent = text;
        input.placeholder = placeholder;
        return input;
    }

    static Text CreateText(Transform parent, string name, string value, Vector2 position,
        Vector2 size, int fontSize, TextAnchor alignment)
    {
        GameObject textObject = new(name);
        textObject.transform.SetParent(parent, false);
        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = alignment;
        RectTransform rect = text.rectTransform;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return text;
    }

    static void AddSceneToBuildSettings()
    {
        EditorBuildSettingsScene[] oldScenes = EditorBuildSettings.scenes;
        for (int i = 0; i < oldScenes.Length; i++)
        {
            if (oldScenes[i].path != ScenePath) continue;
            oldScenes[i].enabled = true;
            EditorBuildSettings.scenes = oldScenes;
            return;
        }

        EditorBuildSettingsScene[] scenes = new EditorBuildSettingsScene[oldScenes.Length + 1];
        oldScenes.CopyTo(scenes, 0);
        scenes[^1] = new EditorBuildSettingsScene(ScenePath, true);
        EditorBuildSettings.scenes = scenes;
    }
}
