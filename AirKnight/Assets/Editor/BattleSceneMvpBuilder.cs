using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class BattleSceneMvpBuilder
{
    const string ScenePath = "Assets/Scenes/BattleScene.unity";
    const string PrefabPath = "Assets/Prefabs/GenericAircraft.prefab";
    const string MaterialFolder = "Assets/Materials";
    const int SelectableLayer = 8;

    [MenuItem("Tools/MVP/Build BattleScene")]
    public static void BuildBattleScene()
    {
        EnsureFolder("Assets", "Prefabs");
        EnsureFolder("Assets", "Materials");
        EnsureSelectableLayer();

        Material aircraftMaterial = GetOrCreateMaterial(MaterialFolder + "/Aircraft.mat", new Color(0.65f, 0.68f, 0.72f));
        Material terrainMaterial = GetOrCreateMaterial(MaterialFolder + "/Terrain.mat", new Color(0.22f, 0.38f, 0.18f));
        GameObject aircraftPrefab = BuildAircraftPrefab(aircraftMaterial);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "BattleScene";

        CreateCamera();
        CreateLight();
        CreateTerrain(terrainMaterial);
        TargetLineUIManager targetManager = CreateTargetManager();
        CreateSelector(targetManager);

        CreateAircraft(aircraftPrefab, "A-1", new Vector3(-300f, 100f, 10f), Vector3.right, AircraftAffiliation.A, 101, 201);
        CreateAircraft(aircraftPrefab, "A-2", new Vector3(-300f, 100f, 0f), Vector3.right, AircraftAffiliation.A, 102, 202);
        CreateAircraft(aircraftPrefab, "A-3", new Vector3(-300f, 100f, -10f), Vector3.right, AircraftAffiliation.A, 103, 203);
        CreateAircraft(aircraftPrefab, "E-1", new Vector3(300f, 100f, 10f), Vector3.left, AircraftAffiliation.E, 201, 101);
        CreateAircraft(aircraftPrefab, "E-2", new Vector3(300f, 100f, 0f), Vector3.left, AircraftAffiliation.E, 202, 102);
        CreateAircraft(aircraftPrefab, "E-3", new Vector3(300f, 100f, -10f), Vector3.left, AircraftAffiliation.E, 203, 103);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("BattleScene MVP generated: " + ScenePath);
    }

    public static void BuildFromCommandLine()
    {
        BuildBattleScene();
    }

    static GameObject BuildAircraftPrefab(Material material)
    {
        GameObject root = new("GenericAircraft");
        Rigidbody body = root.AddComponent<Rigidbody>();
        body.mass = 1f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        BoxCollider physicalCollider = root.AddComponent<BoxCollider>();
        physicalCollider.size = new Vector3(3f, 1.5f, 8f);

        AircraftFlightAI ai = root.AddComponent<AircraftFlightAI>();
        ai.maxSpeed = 50f;
        ai.maxTurnRateDegrees = 30f;

        CreateVisual(root.transform, "Fuselage", PrimitiveType.Cube, Vector3.zero, new Vector3(2.5f, 1.5f, 10f), material);
        CreateVisual(root.transform, "Wings", PrimitiveType.Cube, new Vector3(0f, 0f, -0.5f), new Vector3(10f, 0.35f, 3f), material);
        CreateVisual(root.transform, "Tail", PrimitiveType.Cube, new Vector3(0f, 1.25f, -4f), new Vector3(0.3f, 2.5f, 2f), material);
        CreateVisual(root.transform, "Nose", PrimitiveType.Sphere, new Vector3(0f, 0f, 5f), new Vector3(2.5f, 1.5f, 2f), material);

        GameObject selection = new("SelectionCollider");
        selection.layer = SelectableLayer;
        selection.transform.SetParent(root.transform, false);
        BoxCollider selectionCollider = selection.AddComponent<BoxCollider>();
        selectionCollider.isTrigger = true;
        selectionCollider.size = new Vector3(8f, 4f, 22f);
        selectionCollider.center = Vector3.zero;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void CreateVisual(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
    {
        GameObject visual = GameObject.CreatePrimitive(type);
        visual.name = name;
        visual.transform.SetParent(parent, false);
        visual.transform.localPosition = position;
        visual.transform.localScale = scale;
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        visual.GetComponent<Renderer>().sharedMaterial = material;
    }

    static void CreateAircraft(GameObject prefab, string name, Vector3 position, Vector3 forward,
        AircraftAffiliation affiliation, int id, int targetId)
    {
        GameObject aircraft = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        aircraft.name = name;
        aircraft.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));
        AircraftFlightAI ai = aircraft.GetComponent<AircraftFlightAI>();
        ai.affiliation = affiliation;
        ai.aircraftId = id;
        ai.trackingTargetId = targetId;
    }

    static Camera CreateCamera()
    {
        GameObject cameraObject = new("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        cameraObject.AddComponent<AudioListener>();
        cameraObject.transform.position = new Vector3(0f, 300f, -1000f);
        cameraObject.transform.rotation = Quaternion.LookRotation(-cameraObject.transform.position.normalized, Vector3.up);
        camera.farClipPlane = 5000f;
        return camera;
    }

    static void CreateLight()
    {
        GameObject lightObject = new("Directional Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
    }

    static void CreateTerrain(Material material)
    {
        GameObject tool = new("Terrain Generator Tool");
        FlatTerrainGenerator generator = tool.AddComponent<FlatTerrainGenerator>();
        generator.Generate();
        Transform terrain = tool.transform.Find("FlatTerrain");
        if (terrain != null) terrain.GetComponent<Renderer>().sharedMaterial = material;
    }

    static TargetLineUIManager CreateTargetManager()
    {
        GameObject managerObject = new("Target UI Manager");
        LineRenderer renderer = managerObject.AddComponent<LineRenderer>();
        renderer.useWorldSpace = true;
        renderer.positionCount = 2;
        renderer.startWidth = 1f;
        renderer.endWidth = 1f;
        renderer.enabled = false;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return managerObject.AddComponent<TargetLineUIManager>();
    }

    static void CreateSelector(TargetLineUIManager targetManager)
    {
        GameObject selectorObject = new("Object Selector");
        AircraftObjectSelector selector = selectorObject.AddComponent<AircraftObjectSelector>();
        selector.Configure(Camera.main, targetManager, 1 << SelectableLayer);
    }

    static Material GetOrCreateMaterial(string path, Color color)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        material = new Material(shader) { color = color };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static void EnsureSelectableLayer()
    {
        SerializedObject tagManager = new(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        SerializedProperty layer = layers.GetArrayElementAtIndex(SelectableLayer);
        if (string.IsNullOrEmpty(layer.stringValue)) layer.stringValue = "SelectableAircraft";
        else if (layer.stringValue != "SelectableAircraft")
            throw new System.InvalidOperationException("Layer 8 is already in use: " + layer.stringValue);
        tagManager.ApplyModifiedProperties();
    }

    static void EnsureFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
    }

    static void AddSceneToBuildSettings()
    {
        EditorBuildSettingsScene[] oldScenes = EditorBuildSettings.scenes;
        for (int i = 0; i < oldScenes.Length; i++)
        {
            if (oldScenes[i].path == ScenePath)
            {
                oldScenes[i].enabled = true;
                EditorBuildSettings.scenes = oldScenes;
                return;
            }
        }

        EditorBuildSettingsScene[] scenes = new EditorBuildSettingsScene[oldScenes.Length + 1];
        oldScenes.CopyTo(scenes, 0);
        scenes[^1] = new EditorBuildSettingsScene(ScenePath, true);
        EditorBuildSettings.scenes = scenes;
    }
}
