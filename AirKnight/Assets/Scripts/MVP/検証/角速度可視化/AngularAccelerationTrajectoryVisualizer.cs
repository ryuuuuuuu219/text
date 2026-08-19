using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(LineRenderer))]
public sealed class AngularAccelerationTrajectoryVisualizer : MonoBehaviour
{
    [Header("Motion")]
    [Min(0f)] public float speed = 120f;
    public float angularVelocityDegrees = 30f;
    [Min(0.1f)] public float simulationDuration = 12f;
    [Min(0.001f)] public float sampleInterval = 0.02f;

    [Header("Canvas Coordinates")]
    public Vector2 canvasSize = new(1024f, 768f);
    [Min(0f)] public float canvasEdgePadding = 4f;

    LineRenderer trajectoryLine;
    [SerializeField] InputField speedInput;
    [SerializeField] InputField angularVelocityInput;
    [SerializeField] Text statusText;
    readonly List<Vector3> trajectoryPoints = new();
    float trajectoryElapsedTime;
    float currentOmega;
    bool isDrawing;

    public void Configure(InputField speedField, InputField angularVelocityField, Text status)
    {
        speedInput = speedField;
        angularVelocityInput = angularVelocityField;
        statusText = status;
    }

    void Awake()
    {
        trajectoryLine = GetComponent<LineRenderer>();
        if (speedInput != null)
        {
            speedInput.text = speed.ToString("0.##", CultureInfo.InvariantCulture);
            speedInput.onEndEdit.AddListener(SetSpeedFromText);
        }
        if (angularVelocityInput != null)
        {
            angularVelocityInput.text = angularVelocityDegrees.ToString("0.##", CultureInfo.InvariantCulture);
            angularVelocityInput.onEndEdit.AddListener(SetAngularVelocityFromText);
        }
        RebuildTrajectory();
    }

    void Update()
    {
        if (!isDrawing || trajectoryPoints.Count <= 1) return;

        trajectoryElapsedTime += Time.deltaTime;
        int visiblePointCount = Mathf.Min(
            Mathf.FloorToInt(trajectoryElapsedTime / Mathf.Max(0.001f, sampleInterval)) + 1,
            trajectoryPoints.Count);
        int previousPointCount = trajectoryLine.positionCount;
        if (visiblePointCount <= previousPointCount) return;

        trajectoryLine.positionCount = visiblePointCount;
        for (int i = previousPointCount; i < visiblePointCount; i++)
            trajectoryLine.SetPosition(i, trajectoryPoints[i]);

        UpdateStatusText(currentOmega, visiblePointCount, trajectoryPoints.Count);
        if (visiblePointCount >= trajectoryPoints.Count) isDrawing = false;
    }

    public void SetSpeedFromText(string text)
    {
        if (TryParseFloat(text, out float value)) speed = Mathf.Max(0f, value);
        if (speedInput != null)
            speedInput.text = speed.ToString("0.##", CultureInfo.InvariantCulture);
        RebuildTrajectory();
    }

    public void SetAngularVelocityFromText(string text)
    {
        if (TryParseFloat(text, out float value)) angularVelocityDegrees = value;
        if (angularVelocityInput != null)
            angularVelocityInput.text = angularVelocityDegrees.ToString("0.##", CultureInfo.InvariantCulture);
        RebuildTrajectory();
    }

    [ContextMenu("Rebuild Trajectory")]
    public void RebuildTrajectory()
    {
        if (trajectoryLine == null) trajectoryLine = GetComponent<LineRenderer>();

        float safeDuration = Mathf.Max(0.1f, simulationDuration);
        float safeInterval = Mathf.Max(0.001f, sampleInterval);
        int maximumSamples = Mathf.CeilToInt(safeDuration / safeInterval) + 1;
        currentOmega = angularVelocityDegrees * Mathf.Deg2Rad;
        Vector2 halfCanvas = canvasSize * 0.5f - Vector2.one * Mathf.Max(0f, canvasEdgePadding);
        trajectoryPoints.Clear();

        for (int i = 0; i < maximumSamples; i++)
        {
            float time = Mathf.Min(i * safeInterval, safeDuration);
            Vector2 canvasPosition;
            if (Mathf.Abs(currentOmega) <= 0.000001f)
            {
                canvasPosition = Vector2.right * (speed * time);
            }
            else
            {
                float radius = speed / currentOmega;
                float angle = currentOmega * time;
                canvasPosition = new Vector2(
                    radius * Mathf.Sin(angle),
                    radius * (1f - Mathf.Cos(angle)));
            }

            if (Mathf.Abs(canvasPosition.x) > halfCanvas.x ||
                Mathf.Abs(canvasPosition.y) > halfCanvas.y)
                break;

            trajectoryPoints.Add(new Vector3(canvasPosition.x, canvasPosition.y, 0f));
            if (time >= safeDuration) break;
        }

        if (trajectoryPoints.Count == 0) trajectoryPoints.Add(Vector3.zero);
        trajectoryElapsedTime = 0f;
        if (Application.isPlaying)
        {
            trajectoryLine.positionCount = 1;
            trajectoryLine.SetPosition(0, trajectoryPoints[0]);
            isDrawing = trajectoryPoints.Count > 1;
            UpdateStatusText(currentOmega, 1, trajectoryPoints.Count);
        }
        else
        {
            trajectoryLine.positionCount = trajectoryPoints.Count;
            trajectoryLine.SetPositions(trajectoryPoints.ToArray());
            isDrawing = false;
            UpdateStatusText(currentOmega, trajectoryPoints.Count, trajectoryPoints.Count);
        }
    }

    void UpdateStatusText(float omega, int visiblePointCount, int totalPointCount)
    {
        if (statusText == null) return;
        string radiusText = Mathf.Abs(omega) > 0.000001f
            ? (speed / Mathf.Abs(omega)).ToString("0.##", CultureInfo.InvariantCulture) + " px"
            : "∞";
        float centripetalAcceleration = speed * Mathf.Abs(omega);
        statusText.text =
            $"速度: {speed:0.##} px/s    角速度: {angularVelocityDegrees:0.##} deg/s\n" +
            $"旋回半径: {radiusText}    向心加速度: {centripetalAcceleration:0.##} px/s²    点数: {visiblePointCount}/{totalPointCount}";
    }

    static bool TryParseFloat(string text, out float value)
    {
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
               float.TryParse(text, out value);
    }

    void OnValidate()
    {
        speed = Mathf.Max(0f, speed);
        simulationDuration = Mathf.Max(0.1f, simulationDuration);
        sampleInterval = Mathf.Max(0.001f, sampleInterval);
        canvasSize.x = Mathf.Max(1f, canvasSize.x);
        canvasSize.y = Mathf.Max(1f, canvasSize.y);
        if (isActiveAndEnabled) RebuildTrajectory();
    }
}
