using System;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class AircraftStatus : MonoBehaviour
{
    [Header("Identity")]
    public string aircraftModel = "Generic Aircraft";

    [Header("Completed Aircraft Performance")]
    [Min(0.01f)] public float totalWeight = 1000f;
    [Min(0.01f)] public float rigidbodyMass = 1f;
    [Min(1f)] public float maxHitPoints = 100f;
    [FormerlySerializedAs("maximumSpeed")]
    [Tooltip("Full-throttle equilibrium speed in ideal level flight (m/s).")]
    [Min(1f)] public float levelFlightEquilibriumSpeed = 50f;
    [Tooltip("Full-throttle equilibrium speed in an ideal vertical dive including gravity (m/s).")]
    [Min(1f)] public float idealDiveEquilibriumSpeed = 60f;
    [Min(0f)] public float acceleration = 20f;
    [Tooltip("Structural breakup speed calculated from ideal dive speed and safety factor (m/s).")]
    [Min(1f)] public float breakupSpeed = 75f;

    [Header("Maneuverability (deg/s)")]
    [Tooltip("Pitch turn rate curve. X: aircraft speed (m/s), Y: pitch rate (deg/s).")]
    public AnimationCurve pitchPerformance = AnimationCurve.Linear(0f, 8f, 50f, 12f);
    [Tooltip("Maximum roll rate in degrees per second.")]
    [Min(0f)] public float rollPerformance = 10f;
    [Range(0f, 1f)] public float rollAccuracy = 1f;
    [Tooltip("Maximum yaw rate in degrees per second.")]
    [Min(0f)] public float yawPerformance = 8f;

    [Header("Operations")]
    [Min(0f)] public float operationDurationMinutes = 30f;
    [Min(0)] public int hardpointCount = 2;
    [Min(0f)] public float maximumHardpointWeight = 500f;

    [Header("Calculated Geometry")]
    [Min(0f)] public float fuselageBottomArea = 20f;
    [Min(0f)] public float fuselageProjectedArea = 4f;
    [Min(0f)] public float wingBottomArea = 30f;
    [Min(0f)] public float wingProjectedArea = 2f;
    [Min(0f)] public float internalVolume;

    [Header("Runtime")]
    [SerializeField] float currentHitPoints = 100f;

    public event Action<AircraftStatus> Changed;
    public float CurrentHitPoints => currentHitPoints;
    public float HealthRatio => maxHitPoints > 0f ? currentHitPoints / maxHitPoints : 0f;
    public bool IsDestroyed => currentHitPoints <= 0f;

    void Awake()
    {
        currentHitPoints = Mathf.Clamp(currentHitPoints, 0f, maxHitPoints);
        if (currentHitPoints <= 0f) currentHitPoints = maxHitPoints;
    }

    void OnValidate()
    {
        levelFlightEquilibriumSpeed = Mathf.Max(1f, levelFlightEquilibriumSpeed);
        idealDiveEquilibriumSpeed = Mathf.Max(levelFlightEquilibriumSpeed, idealDiveEquilibriumSpeed);
        breakupSpeed = Mathf.Max(1f, breakupSpeed);
        currentHitPoints = Mathf.Clamp(currentHitPoints, 0f, maxHitPoints);
    }

    public void ResetRuntimeState()
    {
        currentHitPoints = maxHitPoints;
        Changed?.Invoke(this);
    }

    public void ApplyDamage(float amount)
    {
        if (!float.IsFinite(amount) || amount <= 0f) return;
        currentHitPoints = Mathf.Max(0f, currentHitPoints - amount);
        Changed?.Invoke(this);
    }

    public void Repair(float amount)
    {
        if (!float.IsFinite(amount) || amount <= 0f) return;
        currentHitPoints = Mathf.Min(maxHitPoints, currentHitPoints + amount);
        Changed?.Invoke(this);
    }

    public float EvaluatePitchPerformance(float speed)
    {
        return pitchPerformance == null ? 0f : Mathf.Max(0f, pitchPerformance.Evaluate(speed));
    }

    public float GetTotalAoaProjectedArea(float absoluteAoaDegrees)
    {
        float radians = Mathf.Clamp(Mathf.Abs(absoluteAoaDegrees), 0f, 90f) * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        float fuselageArea = sin * fuselageBottomArea + cos * fuselageProjectedArea;
        float wingArea = sin * wingBottomArea + cos * wingProjectedArea;
        return Mathf.Max(0f, fuselageArea + wingArea);
    }

    public void NotifyCalculatedValuesChanged(bool resetCurrentHitPoints)
    {
        if (resetCurrentHitPoints) currentHitPoints = maxHitPoints;
        else currentHitPoints = Mathf.Clamp(currentHitPoints, 0f, maxHitPoints);
        Changed?.Invoke(this);
    }

    public void ApplyTo(AircraftController controller, Rigidbody body)
    {
        if (controller == null || body == null) return;
        controller.thrustPower = acceleration;
        controller.levelFlightEquilibriumSpeed = levelFlightEquilibriumSpeed;
        controller.idealDiveEquilibriumSpeed = idealDiveEquilibriumSpeed;
        controller.breakupSpeed = breakupSpeed;
        controller.forwardDragCoefficient = acceleration
            / Mathf.Max(1f, levelFlightEquilibriumSpeed * levelFlightEquilibriumSpeed);
        controller.turnRateDegrees = new Vector3(
            controller.turnRateDegrees.x,
            rollPerformance,
            yawPerformance);
        controller.fuselageBottomArea = fuselageBottomArea;
        controller.fuselageProjectedArea = fuselageProjectedArea;
        controller.wingBottomArea = wingBottomArea;
        controller.wingProjectedArea = wingProjectedArea;
        body.mass = rigidbodyMass;
    }
}
