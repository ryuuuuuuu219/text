using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AircraftStatus : MonoBehaviour
{
    [Header("Identity")]
    public string aircraftModel = "Generic Aircraft";

    [Header("Completed Aircraft Performance")]
    [Min(0.01f)] public float totalWeight = 1000f;
    [Min(0.01f)] public float rigidbodyMass = 1f;
    [Min(1f)] public float maxHitPoints = 100f;
    [Min(0f)] public float wingLoading = 150f;
    [Min(1f)] public float maximumSpeed = 50f;
    [Min(0f)] public float acceleration = 20f;
    [Min(0.1f)] public float stallSpeed = 20f;
    [Min(1f)] public float breakupSpeed = 75f;

    [Header("Maneuverability")]
    public AnimationCurve pitchPerformance = AnimationCurve.Linear(0f, 8f, 50f, 12f);
    [Min(0f)] public float pitchPerformanceMvp = 12f;
    [Min(0f)] public float rollPerformance = 10f;
    [Range(0f, 1f)] public float rollAccuracy = 1f;
    [Min(0f)] public float yawPerformance = 8f;

    [Header("Operations")]
    [Min(0f)] public float operationDurationMinutes = 30f;
    [Min(0)] public int hardpointCount = 2;
    [Min(0f)] public float maximumHardpointWeight = 500f;

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
        maximumSpeed = Mathf.Max(1f, maximumSpeed);
        breakupSpeed = Mathf.Max(maximumSpeed, breakupSpeed);
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
        return pitchPerformance == null ? pitchPerformanceMvp : Mathf.Max(0f, pitchPerformance.Evaluate(speed));
    }

    public void ApplyTo(AircraftController controller, Rigidbody body)
    {
        if (controller == null || body == null) return;
        controller.thrustPower = acceleration;
        controller.maxSpeed = maximumSpeed;
        controller.stallSpeed = stallSpeed;
        controller.torquePower = new Vector3(pitchPerformanceMvp, rollPerformance, yawPerformance);
        body.mass = rigidbodyMass;
    }
}
