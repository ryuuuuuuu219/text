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
    const int AircraftLayer = 9;

    [MenuItem("Tools/MVP/Build BattleScene")]
    public static void BuildBattleScene()
    {
        EnsureFolder("Assets", "Prefabs");
        EnsureFolder("Assets", "Materials");
        EnsureLayer(SelectableLayer, "SelectableAircraft");
        EnsureLayer(AircraftLayer, "Aircraft");

        Material aircraftMaterial = GetOrCreateMaterial(MaterialFolder + "/Aircraft.mat", new Color(0.65f, 0.68f, 0.72f));
        Material terrainMaterial = GetOrCreateMaterial(MaterialFolder + "/Terrain.mat", new Color(0.22f, 0.38f, 0.18f));
        GameObject aircraftPrefab = BuildAircraftPrefab(aircraftMaterial);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "BattleScene";

        CreateCamera();
        CreateLight();
        CreateEnvironmentManager();
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
        root.layer = AircraftLayer;
        Rigidbody body = root.AddComponent<Rigidbody>();
        body.mass = 1f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        BoxCollider physicalCollider = root.AddComponent<BoxCollider>();
        physicalCollider.size = new Vector3(3f, 1.5f, 8f);

        root.AddComponent<AircraftStatus>();
        root.AddComponent<PilotStatus>();

        FuselagePartStatus fuselage = root.AddComponent<FuselagePartStatus>();
        fuselage.partName = "Generic Fuselage";
        fuselage.weight = 400f;
        fuselage.hitPoints = 400f;
        fuselage.width = 2.5f;
        fuselage.height = 1.5f;
        fuselage.length = 10f;

        MainWingPartStatus mainWing = root.AddComponent<MainWingPartStatus>();
        mainWing.partName = "Generic Main Wing";
        mainWing.quantity = 2;
        mainWing.weight = 150f;
        mainWing.hitPoints = 150f;
        mainWing.width = 5f;
        mainWing.height = 0.2f;
        mainWing.length = 2f;
        mainWing.hardpointCount = 0;
        mainWing.maximumHardpointWeight = 0f;

        ControlSurfacePartStatus controlSurface = root.AddComponent<ControlSurfacePartStatus>();
        controlSurface.partName = "Generic Control Surface";
        controlSurface.weight = 40f;
        controlSurface.hitPoints = 50f;

        AuxiliaryWingPartStatus auxiliaryWing = root.AddComponent<AuxiliaryWingPartStatus>();
        auxiliaryWing.partName = "Generic Tail";
        auxiliaryWing.quantity = 2;
        auxiliaryWing.weight = 25f;
        auxiliaryWing.hitPoints = 25f;

        EnginePartStatus engine = root.AddComponent<EnginePartStatus>();
        engine.partName = "Generic Engine";
        engine.weight = 200f;
        engine.hitPoints = 150f;
        engine.thrust = 20000f;

        FuelTankPartStatus fuelTank = root.AddComponent<FuelTankPartStatus>();
        fuelTank.partName = "Generic Fuel Tank";
        fuelTank.weight = 100f;
        fuelTank.hitPoints = 50f;
        fuelTank.volume = 30f;

        ArmorPartStatus armor = root.AddComponent<ArmorPartStatus>();
        armor.partName = "Generic Armor";
        armor.weight = 50f;
        armor.hitPoints = 100f;
        armor.defenseMultiplier = 1.2f;

        AuxiliaryEquipmentPartStatus equipment = root.AddComponent<AuxiliaryEquipmentPartStatus>();
        equipment.partName = "Generic Sensor";
        equipment.weight = 20f;
        equipment.hitPoints = 30f;

        HardpointPartStatus hardpoint = root.AddComponent<HardpointPartStatus>();
        hardpoint.partName = "Generic Hardpoint";
        hardpoint.quantity = 2;
        hardpoint.weight = 10f;
        hardpoint.hitPoints = 20f;
        hardpoint.maximumWeaponWeight = 250f;

        AircraftPartStatusConverter partConverter = root.AddComponent<AircraftPartStatusConverter>();
        AircraftFlightAI ai = root.AddComponent<AircraftFlightAI>();
        ai.maxSpeed = 50f;
        ai.maxTurnRateDegrees = 30f;
        partConverter.Calc();

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
        PilotStatus pilot = aircraft.GetComponent<PilotStatus>();
        pilot.pilotId = id;
        pilot.pilotName = "Pilot " + name;
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

    static void CreateEnvironmentManager()
    {
        GameObject environmentObject = new("Environment Manager");
        environmentObject.AddComponent<EnvironmentManager>();
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

    static void EnsureLayer(int layerIndex, string layerName)
    {
        SerializedObject tagManager = new(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        SerializedProperty layer = layers.GetArrayElementAtIndex(layerIndex);
        if (string.IsNullOrEmpty(layer.stringValue)) layer.stringValue = layerName;
        else if (layer.stringValue != layerName)
            throw new System.InvalidOperationException($"Layer {layerIndex} is already in use: {layer.stringValue}");
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
