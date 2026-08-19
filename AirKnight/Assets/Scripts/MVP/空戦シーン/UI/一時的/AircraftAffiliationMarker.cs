using UnityEngine;

[DisallowMultipleComponent]
public sealed class AircraftAffiliationMarker : MonoBehaviour
{
    AircraftFlightAI aircraft;
    Camera targetCamera;
    TargetLineUIManager observationManager;
    MeshRenderer markerRenderer;
    MaterialPropertyBlock materialProperties;
    Vector2 viewportOffset;
    float viewportHeight;
    AircraftAffiliation playerAffiliation;
    Color friendlyColor;
    Color enemyColor;
    Color observedColor;
    Color appliedColor;

    public AircraftFlightAI Aircraft => aircraft;

    public void Configure(
        AircraftFlightAI targetAircraft,
        Camera cameraToUse,
        TargetLineUIManager targetManager,
        Mesh markerMesh,
        Material markerMaterial,
        Vector2 offset,
        float height,
        AircraftAffiliation playerSide,
        Color friendly,
        Color enemy,
        Color observed)
    {
        aircraft = targetAircraft;
        targetCamera = cameraToUse;
        observationManager = targetManager;
        viewportOffset = offset;
        viewportHeight = Mathf.Max(0.001f, height);
        playerAffiliation = playerSide;
        friendlyColor = friendly;
        enemyColor = enemy;
        observedColor = observed;

        MeshFilter filter = GetComponent<MeshFilter>();
        if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
        markerRenderer = GetComponent<MeshRenderer>();
        if (markerRenderer == null) markerRenderer = gameObject.AddComponent<MeshRenderer>();
        filter.sharedMesh = markerMesh;
        markerRenderer.sharedMaterial = markerMaterial;
        markerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        markerRenderer.receiveShadows = false;
        markerRenderer.sortingOrder = 100;
        materialProperties ??= new MaterialPropertyBlock();
        ApplyColor(force: true);
    }

    public void UpdateDependencies(Camera cameraToUse, TargetLineUIManager targetManager)
    {
        targetCamera = cameraToUse;
        observationManager = targetManager;
    }

    void LateUpdate()
    {
        if (aircraft == null || targetCamera == null || markerRenderer == null)
        {
            if (markerRenderer != null) markerRenderer.enabled = false;
            return;
        }

        Vector3 viewportPosition = targetCamera.WorldToViewportPoint(aircraft.transform.position);
        if (viewportPosition.z <= 0f)
        {
            markerRenderer.enabled = false;
            return;
        }

        // Deliberately do not clamp: markers may leave the screen at its edges.
        viewportPosition.x += viewportOffset.x;
        viewportPosition.y += viewportOffset.y;
        transform.position = targetCamera.ViewportToWorldPoint(viewportPosition);
        transform.rotation = Quaternion.LookRotation(
            targetCamera.transform.forward,
            targetCamera.transform.up);

        float worldHeight = targetCamera.orthographic
            ? targetCamera.orthographicSize * 2f * viewportHeight
            : 2f * viewportPosition.z
              * Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad)
              * viewportHeight;
        Vector3 parentScale = transform.parent != null
            ? transform.parent.lossyScale
            : Vector3.one;
        transform.localScale = new Vector3(
            worldHeight / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
            worldHeight / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
            worldHeight / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z)));
        markerRenderer.enabled = true;
        ApplyColor(force: false);
    }

    void ApplyColor(bool force)
    {
        if (markerRenderer == null) return;
        Color color = observationManager != null && observationManager.ObservationTarget == aircraft
            ? observedColor
            : aircraft != null && aircraft.affiliation == playerAffiliation
                ? friendlyColor
                : enemyColor;
        if (!force && color == appliedColor) return;

        appliedColor = color;
        materialProperties.SetColor("_Color", color);
        materialProperties.SetColor("_BaseColor", color);
        markerRenderer.SetPropertyBlock(materialProperties);
    }
}
