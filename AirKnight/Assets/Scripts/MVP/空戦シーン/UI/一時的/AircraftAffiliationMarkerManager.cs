using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class AircraftAffiliationMarkerManager : MonoBehaviour
{
    const float SynchronizeInterval = 0.25f;
    static AircraftAffiliationMarkerManager instance;

    [SerializeField] Camera targetCamera;
    [SerializeField] TargetLineUIManager observationManager;
    [SerializeField] AircraftAffiliation playerAffiliation = AircraftAffiliation.A;
    [SerializeField] Vector2 viewportOffset = new(0f, -0.05f);
    [SerializeField, Min(0.001f)] float markerViewportHeight = 0.025f;
    [SerializeField] Color friendlyColor = new(0.1f, 0.65f, 1f, 0.95f);
    [SerializeField] Color enemyColor = new(1f, 0.2f, 0.15f, 0.95f);
    [SerializeField] Color observedColor = new(1f, 0.85f, 0.1f, 1f);

    readonly Dictionary<AircraftFlightAI, AircraftAffiliationMarker> markers = new();
    readonly List<AircraftFlightAI> staleAircraft = new();
    Mesh sharedMarkerMesh;
    Material sharedMarkerMaterial;
    float nextSynchronization;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;
        AircraftAffiliationMarkerManager existing = FindAnyObjectByType<AircraftAffiliationMarkerManager>();
        if (existing != null)
        {
            instance = existing;
            return;
        }

        GameObject managerObject = new("Aircraft Affiliation Marker Manager");
        instance = managerObject.AddComponent<AircraftAffiliationMarkerManager>();
        DontDestroyOnLoad(managerObject);
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        CreateSharedResources();
    }

    void Update()
    {
        if (Time.unscaledTime < nextSynchronization) return;
        nextSynchronization = Time.unscaledTime + SynchronizeInterval;
        SynchronizeMarkers();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (sharedMarkerMaterial != null) Destroy(sharedMarkerMaterial);
        if (sharedMarkerMesh != null) Destroy(sharedMarkerMesh);
    }

    void OnValidate()
    {
        markerViewportHeight = Mathf.Max(0.001f, markerViewportHeight);
    }

    void SynchronizeMarkers()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (observationManager == null)
            observationManager = FindAnyObjectByType<TargetLineUIManager>();
        if (sharedMarkerMesh == null || sharedMarkerMaterial == null) CreateSharedResources();

        IReadOnlyList<AircraftFlightAI> aircraft = AircraftFlightAI.ActiveAircraft;
        for (int i = 0; i < aircraft.Count; i++)
        {
            AircraftFlightAI item = aircraft[i];
            if (item == null) continue;
            if (!markers.TryGetValue(item, out AircraftAffiliationMarker marker) || marker == null)
            {
                marker = CreateMarker(item);
                markers[item] = marker;
            }
            else
            {
                marker.UpdateDependencies(targetCamera, observationManager);
            }
        }

        staleAircraft.Clear();
        foreach (KeyValuePair<AircraftFlightAI, AircraftAffiliationMarker> pair in markers)
            if (pair.Key == null || !ContainsAircraft(aircraft, pair.Key)) staleAircraft.Add(pair.Key);

        for (int i = 0; i < staleAircraft.Count; i++)
        {
            AircraftFlightAI item = staleAircraft[i];
            if (markers.TryGetValue(item, out AircraftAffiliationMarker marker) && marker != null)
                Destroy(marker.gameObject);
            markers.Remove(item);
        }
    }

    AircraftAffiliationMarker CreateMarker(AircraftFlightAI aircraft)
    {
        GameObject markerObject = new("Affiliation Marker");
        markerObject.transform.SetParent(aircraft.transform, false);
        AircraftAffiliationMarker marker = markerObject.AddComponent<AircraftAffiliationMarker>();
        marker.Configure(
            aircraft,
            targetCamera,
            observationManager,
            sharedMarkerMesh,
            sharedMarkerMaterial,
            viewportOffset,
            markerViewportHeight,
            playerAffiliation,
            friendlyColor,
            enemyColor,
            observedColor);
        return marker;
    }

    void CreateSharedResources()
    {
        if (sharedMarkerMesh == null)
        {
            sharedMarkerMesh = new Mesh { name = "Aircraft Affiliation Triangle" };
            sharedMarkerMesh.SetVertices(new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0f, 0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f)
            });
            sharedMarkerMesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            sharedMarkerMesh.RecalculateNormals();
            sharedMarkerMesh.RecalculateBounds();
        }

        if (sharedMarkerMaterial != null) return;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) return;
        sharedMarkerMaterial = new Material(shader) { name = "Aircraft Affiliation Marker (Runtime)" };
        sharedMarkerMaterial.SetInt("_Cull", (int)CullMode.Off);
        sharedMarkerMaterial.renderQueue = (int)RenderQueue.Overlay;
    }

    static bool ContainsAircraft(IReadOnlyList<AircraftFlightAI> aircraft, AircraftFlightAI target)
    {
        for (int i = 0; i < aircraft.Count; i++)
            if (aircraft[i] == target) return true;
        return false;
    }
}
