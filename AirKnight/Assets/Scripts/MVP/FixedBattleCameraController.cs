using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public sealed class FixedBattleCameraController : MonoBehaviour
{
    static readonly Vector3 DefaultBasePosition = new(0f, 300f, -1000f);

    [Header("Zoom")]
    [SerializeField] Vector3 basePosition = DefaultBasePosition;
    [SerializeField] float[] zoomDistances = { 100f, 200f, 500f, 1000f };
    [SerializeField] int zoomMode;

    [Header("UI")]
    [SerializeField] Rect controlsRect = new(0f, 80f, 100f, 30f);

    [Header("Pan Limits")]
    [SerializeField, Min(1f)] float terrainHalfSize = 6000f;
    [SerializeField, Min(0f)] float terrainEdgePadding = 100f;
    [SerializeField, Min(0.01f)] float minimumCameraHeight = 1f;

    Camera targetCamera;
    bool isDragging;
    Vector3 zoomAnchorPosition;
    Vector3 dragStartCameraPosition;
    Vector3 dragStartWorldPoint;
    GUIStyle zoomButtonStyle;

    public int ZoomMode => zoomMode;
    public float CurrentZoomDistance => HasZoomModes ? zoomDistances[zoomMode] : 0f;
    bool HasZoomModes => zoomDistances != null && zoomDistances.Length > 0;

    void Awake()
    {
        targetCamera = GetComponent<Camera>();
        zoomMode = FindClosestZoomMode(transform.position.magnitude);
        zoomAnchorPosition = transform.position;
    }

    void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 mousePosition = mouse.position.ReadValue();
        if (mouse.rightButton.wasPressedThisFrame && !IsPointerOverControls(mousePosition))
            BeginDrag(mousePosition);

        if (isDragging && mouse.rightButton.isPressed)
            ContinueDrag(mousePosition);

        if (mouse.rightButton.wasReleasedThisFrame)
            isDragging = false;
    }

    public void ZoomIn()
    {
        if (!HasZoomModes) return;
        zoomMode = (zoomMode - 1 + zoomDistances.Length) % zoomDistances.Length;
        ResetToZoomMode();
    }

    public void ZoomOut()
    {
        if (!HasZoomModes) return;
        zoomMode = (zoomMode + 1) % zoomDistances.Length;
        ResetToZoomMode();
    }

    public void ResetToZoomMode()
    {
        if (!HasZoomModes) return;

        zoomMode = Mathf.Clamp(zoomMode, 0, zoomDistances.Length - 1);
        Vector3 direction = basePosition.sqrMagnitude > Mathf.Epsilon
            ? basePosition.normalized
            : DefaultBasePosition.normalized;
        transform.position = direction * Mathf.Max(0.01f, zoomDistances[zoomMode]);
        zoomAnchorPosition = transform.position;
        isDragging = false;
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

        float halfWidthAtFarClip = targetCamera.farClipPlane
            * targetCamera.aspect
            * Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float maximumRightOffset = Mathf.Max(
            0f,
            terrainHalfSize - terrainEdgePadding - halfWidthAtFarClip);
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
        if (!HasZoomModes) return 0;

        int closest = 0;
        float smallestDifference = Mathf.Abs(zoomDistances[0] - distance);
        for (int i = 1; i < zoomDistances.Length; i++)
        {
            float difference = Mathf.Abs(zoomDistances[i] - distance);
            if (difference >= smallestDifference) continue;
            smallestDifference = difference;
            closest = i;
        }

        return closest;
    }

    bool IsPointerOverControls(Vector2 screenPosition)
    {
        Vector2 guiPosition = new(screenPosition.x, Screen.height - screenPosition.y);
        return controlsRect.Contains(guiPosition);
    }

    void OnGUI()
    {
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
