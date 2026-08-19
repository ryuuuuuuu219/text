using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public sealed class FixedBattleCameraController : MonoBehaviour
{
    static readonly float[] ZoomDistances =
    {
        100f, 200f, 500f, 1000f, 2000f, 5000f, 7500f, 10000f, 12000f
    };
    const float ReferenceWidth = 1024f;
    const float ReferenceHeight = 768f;
    const float VerticalFieldOfView = 60f;
    const float FarClipDistance = 17000f;
    const float MouseWheelZoomStep = 50f;

    [Header("Zoom")]
    [SerializeField] int zoomMode;
    [SerializeField] Vector3 initialFocusPoint = new(0f, 2000f, 0f);

    [Header("First Person")]
    [SerializeField] Vector3 firstPersonLocalOffset = new(0f, 0.75f, 2f);

    [Header("UI")]
    [SerializeField] Rect controlsRect = new(0f, 80f, 100f, 30f);

    [Header("Pan Limits")]
    [SerializeField, Min(1f)] float terrainHalfSize = 14000f;
    [SerializeField, Min(0f)] float terrainEdgePadding = 100f;
    [SerializeField, Min(0.01f)] float minimumCameraHeight = 1f;

    Camera targetCamera;
    bool isDragging;
    float currentZoomDistance;
    float referenceZoomDistance;
    Vector3 focusPoint;
    Vector3 dragStartCameraPosition;
    Vector3 dragStartFocusPoint;
    Vector3 dragStartWorldPoint;
    GUIStyle zoomButtonStyle;
    AircraftFlightAI firstPersonTarget;
    Renderer[] hiddenTargetRenderers;
    bool[] hiddenTargetRendererStates;
    Vector3 overviewPosition;
    Quaternion overviewRotation;
    Vector3 overviewFocusPoint;
    float overviewZoomDistance;
    int overviewZoomMode;

    public int ZoomMode => zoomMode;
    public float CurrentZoomDistance => currentZoomDistance;
    public float CurrentZoomMultiplier => referenceZoomDistance /
        Mathf.Max(GetMinimumZoomDistance(), currentZoomDistance);
    public Vector3 FocusPoint => focusPoint;
    public bool IsFirstPersonActive => firstPersonTarget != null;
    public AircraftFlightAI FirstPersonTarget => firstPersonTarget;

    void Awake()
    {
        targetCamera = GetComponent<Camera>();
        ApplyFixedCameraSettings();
        focusPoint = initialFocusPoint;
        currentZoomDistance = Mathf.Clamp(
            Vector3.Distance(transform.position, focusPoint),
            GetMinimumZoomDistance(),
            GetMaximumZoomDistance());
        referenceZoomDistance = currentZoomDistance;
        zoomMode = FindClosestZoomMode(currentZoomDistance);
        ApplyOverviewCameraPosition();
    }

    void OnValidate()
    {
        terrainHalfSize = Mathf.Max(1f, terrainHalfSize);
        terrainEdgePadding = Mathf.Max(0f, terrainEdgePadding);
        minimumCameraHeight = Mathf.Max(1f, minimumCameraHeight);
        zoomMode = Mathf.Clamp(zoomMode, 0, ZoomDistances.Length - 1);

        if (TryGetComponent(out Camera camera))
        {
            targetCamera = camera;
            ApplyFixedCameraSettings();
        }
    }

    void ApplyFixedCameraSettings()
    {
        targetCamera.fieldOfView = VerticalFieldOfView;
        targetCamera.farClipPlane = FarClipDistance;
    }

    void Update()
    {
        if (IsFirstPersonActive)
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                ExitFirstPerson();
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 mousePosition = mouse.position.ReadValue();
        if (mouse.rightButton.wasPressedThisFrame
            && !IsPointerOverControls(mousePosition)
            && !BattleCommonScreenUI.IsPointerOverInterface(mousePosition))
            BeginDrag(mousePosition);

        if (isDragging && mouse.rightButton.isPressed)
            ContinueDrag(mousePosition);

        if (mouse.rightButton.wasReleasedThisFrame)
            isDragging = false;

        Vector2 scroll = mouse.scroll.ReadValue();
        if (Mathf.Abs(scroll.y) > Mathf.Epsilon
            && !IsPointerOverControls(mousePosition)
            && !BattleCommonScreenUI.IsPointerOverInterface(mousePosition))
        {
            ZoomBy(scroll.y > 0f ? -MouseWheelZoomStep : MouseWheelZoomStep);
        }
    }

    public void ZoomIn()
    {
        if (IsFirstPersonActive) return;
        float targetDistance = GetMinimumZoomDistance();
        for (int i = 0; i < ZoomDistances.Length; i++)
        {
            float candidate = ZoomDistances[i];
            if (candidate >= currentZoomDistance || candidate <= targetDistance) continue;
            targetDistance = candidate;
        }
        SetZoomDistance(targetDistance);
    }

    public void ZoomOut()
    {
        if (IsFirstPersonActive) return;
        float targetDistance = GetMaximumZoomDistance();
        for (int i = 0; i < ZoomDistances.Length; i++)
        {
            float candidate = ZoomDistances[i];
            if (candidate <= currentZoomDistance || candidate >= targetDistance) continue;
            targetDistance = candidate;
        }
        SetZoomDistance(targetDistance);
    }

    public void ResetToZoomMode()
    {
        zoomMode = Mathf.Clamp(zoomMode, 0, ZoomDistances.Length - 1);
        SetZoomDistance(ZoomDistances[zoomMode]);
    }

    void ZoomBy(float distanceDelta)
    {
        SetZoomDistance(currentZoomDistance + distanceDelta);
    }

    void SetZoomDistance(float requestedDistance)
    {
        float nextDistance = Mathf.Clamp(
            requestedDistance,
            GetMinimumZoomDistance(),
            GetMaximumZoomDistance());
        if (Mathf.Abs(nextDistance - currentZoomDistance) <= Mathf.Epsilon) return;
        currentZoomDistance = nextDistance;
        zoomMode = FindClosestZoomMode(currentZoomDistance);
        ApplyOverviewCameraPosition();
        isDragging = false;
    }

    public void FocusWorldPoint(Vector3 worldPoint)
    {
        if (IsFirstPersonActive) ExitFirstPerson();
        focusPoint = ClampFocusPoint(worldPoint);
        ApplyOverviewCameraPosition();
        isDragging = false;
    }

    public void ToggleFirstPerson(AircraftFlightAI aircraft)
    {
        if (aircraft == null)
        {
            ExitFirstPerson();
            return;
        }

        if (firstPersonTarget == aircraft)
            ExitFirstPerson();
        else
            EnterFirstPerson(aircraft);
    }

    public void EnterFirstPerson(AircraftFlightAI aircraft)
    {
        if (aircraft == null || firstPersonTarget == aircraft) return;

        if (!IsFirstPersonActive)
        {
            overviewPosition = transform.position;
            overviewRotation = transform.rotation;
            overviewFocusPoint = focusPoint;
            overviewZoomDistance = currentZoomDistance;
            overviewZoomMode = zoomMode;
        }
        else
        {
            RestoreTargetRenderers();
        }

        firstPersonTarget = aircraft;
        HideTargetRenderers(aircraft);
        isDragging = false;
        FollowFirstPersonTarget();
    }

    public void ExitFirstPerson()
    {
        if (!IsFirstPersonActive && hiddenTargetRenderers == null) return;

        RestoreTargetRenderers();
        firstPersonTarget = null;
        transform.SetPositionAndRotation(overviewPosition, overviewRotation);
        focusPoint = overviewFocusPoint;
        currentZoomDistance = overviewZoomDistance;
        zoomMode = overviewZoomMode;
        isDragging = false;
    }

    void LateUpdate()
    {
        if (!IsFirstPersonActive)
        {
            if (hiddenTargetRenderers != null) ExitFirstPerson();
            return;
        }

        FollowFirstPersonTarget();
    }

    void FollowFirstPersonTarget()
    {
        if (firstPersonTarget == null)
        {
            ExitFirstPerson();
            return;
        }

        Transform targetTransform = firstPersonTarget.transform;
        transform.SetPositionAndRotation(
            targetTransform.TransformPoint(firstPersonLocalOffset),
            targetTransform.rotation);
    }

    void HideTargetRenderers(AircraftFlightAI aircraft)
    {
        hiddenTargetRenderers = aircraft.GetComponentsInChildren<Renderer>(true);
        hiddenTargetRendererStates = new bool[hiddenTargetRenderers.Length];
        for (int i = 0; i < hiddenTargetRenderers.Length; i++)
        {
            Renderer targetRenderer = hiddenTargetRenderers[i];
            if (targetRenderer == null) continue;
            hiddenTargetRendererStates[i] = targetRenderer.enabled;
            targetRenderer.enabled = false;
        }
    }

    void RestoreTargetRenderers()
    {
        if (hiddenTargetRenderers != null && hiddenTargetRendererStates != null)
        {
            int count = Mathf.Min(hiddenTargetRenderers.Length, hiddenTargetRendererStates.Length);
            for (int i = 0; i < count; i++)
                if (hiddenTargetRenderers[i] != null)
                    hiddenTargetRenderers[i].enabled = hiddenTargetRendererStates[i];
        }

        hiddenTargetRenderers = null;
        hiddenTargetRendererStates = null;
    }

    void OnDisable()
    {
        ExitFirstPerson();
    }

    void BeginDrag(Vector2 mousePosition)
    {
        dragStartCameraPosition = transform.position;
        dragStartFocusPoint = focusPoint;
        Plane movementPlane = new(transform.forward, dragStartFocusPoint);
        if (!TryProjectToMovementPlane(mousePosition, dragStartCameraPosition, movementPlane, out dragStartWorldPoint))
            return;

        isDragging = true;
    }

    void ContinueDrag(Vector2 mousePosition)
    {
        Plane movementPlane = new(transform.forward, dragStartFocusPoint);
        if (!TryProjectToMovementPlane(mousePosition, dragStartCameraPosition, movementPlane, out Vector3 worldPoint))
            return;

        focusPoint = ClampFocusPoint(
            dragStartFocusPoint + dragStartWorldPoint - worldPoint);
        ApplyOverviewCameraPosition();
    }

    Vector3 ClampFocusPoint(Vector3 requestedFocusPoint)
    {
        float limit = Mathf.Max(0f, terrainHalfSize - terrainEdgePadding);
        requestedFocusPoint.x = Mathf.Clamp(requestedFocusPoint.x, -limit, limit);
        requestedFocusPoint.z = Mathf.Clamp(requestedFocusPoint.z, -limit, limit);

        float cameraHeight = requestedFocusPoint.y -
                             transform.forward.y * currentZoomDistance;
        if (cameraHeight < minimumCameraHeight)
            requestedFocusPoint.y += minimumCameraHeight - cameraHeight;
        return requestedFocusPoint;
    }

    void ApplyOverviewCameraPosition()
    {
        transform.position = focusPoint - transform.forward * currentZoomDistance;
    }

    bool TryProjectToMovementPlane(
        Vector2 mousePosition,
        Vector3 rayOrigin,
        Plane movementPlane,
        out Vector3 worldPoint)
    {
        Vector3 viewport = targetCamera.ScreenToViewportPoint(mousePosition);
        float tanHalfVerticalFov = Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        Vector3 localDirection = new(
            (viewport.x * 2f - 1f) * targetCamera.aspect * tanHalfVerticalFov,
            (viewport.y * 2f - 1f) * tanHalfVerticalFov,
            1f);
        Ray ray = new(rayOrigin, transform.rotation * localDirection.normalized);

        if (movementPlane.Raycast(ray, out float distance))
        {
            worldPoint = ray.GetPoint(distance);
            return true;
        }

        worldPoint = default;
        return false;
    }

    int FindClosestZoomMode(float distance)
    {
        int closest = 0;
        float smallestDifference = Mathf.Abs(ZoomDistances[0] - distance);
        for (int i = 1; i < ZoomDistances.Length; i++)
        {
            float difference = Mathf.Abs(ZoomDistances[i] - distance);
            if (difference >= smallestDifference) continue;
            smallestDifference = difference;
            closest = i;
        }

        return closest;
    }

    static float GetMinimumZoomDistance()
    {
        return Mathf.Min(ZoomDistances);
    }

    static float GetMaximumZoomDistance()
    {
        return Mathf.Max(ZoomDistances);
    }

    bool IsPointerOverControls(Vector2 screenPosition)
    {
        Vector2 guiPosition = new(
            screenPosition.x * ReferenceWidth / Mathf.Max(1f, Screen.width),
            (Screen.height - screenPosition.y) * ReferenceHeight / Mathf.Max(1f, Screen.height));
        return controlsRect.Contains(guiPosition);
    }

    void OnGUI()
    {
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(
            Screen.width / ReferenceWidth,
            Screen.height / ReferenceHeight,
            1f));

        zoomButtonStyle ??= new GUIStyle(GUIStyle.none)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 20,
            fontStyle = FontStyle.Bold
        };
        zoomButtonStyle.normal.textColor = Color.white;
        zoomButtonStyle.hover.textColor = Color.white;
        zoomButtonStyle.active.textColor = Color.white;

        const float buttonWidth = 50f;
        const float buttonHeight = 30f;
        Rect zoomInRect = new(controlsRect.x, controlsRect.y, buttonWidth, buttonHeight);
        Rect zoomOutRect = new(controlsRect.x + buttonWidth, controlsRect.y, buttonWidth, buttonHeight);
        if (DrawZoomButton(zoomInRect, "+")) ZoomIn();
        if (DrawZoomButton(zoomOutRect, "-")) ZoomOut();
        GUI.Label(
            new Rect(100f, 80f, 110f, 30f),
            "倍率：" + currentZoomDistance.ToString());
        GUI.matrix = previousMatrix;
    }

    bool DrawZoomButton(Rect rect, string label)
    {
        Color previousColor = GUI.color;
        if (rect.Contains(Event.current.mousePosition))
        {
            GUI.color = new Color(1f, 1f, 1f, 0.15f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
        }

        GUI.color = Color.white;
        const float borderWidth = 2f;
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, borderWidth), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - borderWidth, rect.width, borderWidth), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y, borderWidth, rect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax - borderWidth, rect.y, borderWidth, rect.height), Texture2D.whiteTexture);

        bool clicked = GUI.Button(rect, label, zoomButtonStyle);
        GUI.color = previousColor;
        return clicked;
    }
}
