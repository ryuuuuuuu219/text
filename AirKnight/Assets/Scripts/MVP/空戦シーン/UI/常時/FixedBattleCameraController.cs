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
    Vector3 zoomAnchorPosition;
    Vector3 dragStartCameraPosition;
    Vector3 dragStartWorldPoint;
    GUIStyle zoomButtonStyle;
    AircraftFlightAI firstPersonTarget;
    Renderer[] hiddenTargetRenderers;
    bool[] hiddenTargetRendererStates;
    Vector3 overviewPosition;
    Quaternion overviewRotation;
    Vector3 overviewZoomAnchorPosition;
    float overviewZoomDistance;
    int overviewZoomMode;

    public int ZoomMode => zoomMode;
    public float CurrentZoomDistance => currentZoomDistance;
    public bool IsFirstPersonActive => firstPersonTarget != null;
    public AircraftFlightAI FirstPersonTarget => firstPersonTarget;

    void Awake()
    {
        targetCamera = GetComponent<Camera>();
        ApplyFixedCameraSettings();
        currentZoomDistance = Mathf.Clamp(
            transform.position.magnitude,
            GetMinimumZoomDistance(),
            GetMaximumZoomDistance());
        zoomMode = FindClosestZoomMode(currentZoomDistance);
        zoomAnchorPosition = transform.position;
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
        float distanceDelta = nextDistance - currentZoomDistance;
        if (Mathf.Abs(distanceDelta) <= Mathf.Epsilon) return;

        Vector3 positionDelta = -transform.forward * distanceDelta;
        transform.position += positionDelta;
        zoomAnchorPosition += positionDelta;
        currentZoomDistance = nextDistance;
        zoomMode = FindClosestZoomMode(currentZoomDistance);
        isDragging = false;
    }

    public void FocusWorldPoint(Vector3 worldPoint)
    {
        if (IsFirstPersonActive) ExitFirstPerson();
        Vector3 planarOffset = transform.right * Vector3.Dot(worldPoint, transform.right)
            + transform.up * Vector3.Dot(worldPoint, transform.up);
        transform.position = ClampPanPosition(zoomAnchorPosition + planarOffset);
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
            overviewZoomAnchorPosition = zoomAnchorPosition;
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
        zoomAnchorPosition = overviewZoomAnchorPosition;
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
        Plane movementPlane = new(transform.forward, Vector3.zero);
        if (!TryProjectToMovementPlane(mousePosition, dragStartCameraPosition, movementPlane, out dragStartWorldPoint))
            return;

        isDragging = true;
    }

    void ContinueDrag(Vector2 mousePosition)
    {
        Plane movementPlane = new(transform.forward, Vector3.zero);
        if (!TryProjectToMovementPlane(mousePosition, dragStartCameraPosition, movementPlane, out Vector3 worldPoint))
            return;

        Vector3 requestedPosition = dragStartCameraPosition + dragStartWorldPoint - worldPoint;
        transform.position = ClampPanPosition(requestedPosition);
    }

    Vector3 ClampPanPosition(Vector3 requestedPosition)
    {
        Vector3 right = transform.right;
        Vector3 up = transform.up;
        Vector3 offset = requestedPosition - zoomAnchorPosition;

        float maximumRightOffset = Mathf.Max(
            0f,
            terrainHalfSize - terrainEdgePadding);
        float rightOffset = Mathf.Clamp(
            Vector3.Dot(offset, right),
            -maximumRightOffset,
            maximumRightOffset);

        float upOffset = Vector3.Dot(offset, up);
        if (up.y > Mathf.Epsilon)
        {
            float minimumUpOffset = (minimumCameraHeight - zoomAnchorPosition.y) / up.y;
            upOffset = Mathf.Max(upOffset, minimumUpOffset);
        }

        return zoomAnchorPosition + right * rightOffset + up * upOffset;
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
