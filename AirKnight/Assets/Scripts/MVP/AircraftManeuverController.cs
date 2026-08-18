using UnityEngine;

[DisallowMultipleComponent]
public sealed class AircraftManeuverController : MonoBehaviour
{
    [Header("Pursuit")]
    [SerializeField, Min(0f)] float leadTime = 0.5f;
    [SerializeField, Min(0f)] float commandDirectionSmoothing = 6f;

    [Header("Steering")]
    [SerializeField, Min(0f)] float steeringGain = 2f;
    [SerializeField, Min(0f)] float rollGain = 1.5f;
    [SerializeField, Min(0f)] float yawGain = 2f;

    [Header("Throttle")]
    [SerializeField, Min(0f)] float throttleDeadZone = 0.5f;

    AircraftFlightAI owner;
    AircraftFlightAI trackingTarget;
    Rigidbody ownerBody;
    Rigidbody targetBody;
    PilotStatus pilotStatus;
    Vector3 commandedFlightDirection;

    public AircraftFlightAI TrackingTarget => trackingTarget;
    public Vector3 CurrentAimPoint { get; private set; }

    public void Initialize(AircraftFlightAI aircraft)
    {
        owner = aircraft;
        ownerBody = GetComponent<Rigidbody>();
        pilotStatus = GetComponent<PilotStatus>();
        commandedFlightDirection = owner != null ? owner.transform.forward : transform.forward;
        CurrentAimPoint = transform.position + commandedFlightDirection;
    }

    public void SetTrackingTarget(AircraftFlightAI target)
    {
        trackingTarget = target;
        targetBody = trackingTarget != null ? trackingTarget.GetComponent<Rigidbody>() : null;
    }

    public Vector3 GetControlInput()
    {
        if (owner == null || trackingTarget == null)
            return Vector3.zero;

        if (targetBody == null || targetBody.transform != trackingTarget.transform)
            targetBody = trackingTarget.GetComponent<Rigidbody>();

        Vector3 leadOffset = targetBody != null
            ? targetBody.linearVelocity * leadTime
            : Vector3.zero;
        CurrentAimPoint = trackingTarget.transform.position + leadOffset;

        Vector3 desiredDirection = SafeNormalize(
            CurrentAimPoint - owner.transform.position,
            owner.transform.forward);
        float blend = commandDirectionSmoothing <= 0f
            ? 1f
            : 1f - Mathf.Exp(-commandDirectionSmoothing * Time.fixedDeltaTime);
        commandedFlightDirection = SafeNormalize(
            Vector3.Slerp(commandedFlightDirection, desiredDirection, blend),
            desiredDirection);

        Vector3 localDirection = owner.transform.InverseTransformDirection(commandedFlightDirection);
        float pitch = Mathf.Clamp(localDirection.y * steeringGain, -1f, 1f);
        float roll = Mathf.Clamp(localDirection.x * rollGain, -1f, 1f);
        float yaw = Mathf.Clamp(localDirection.x * yawGain, -1f, 1f);
        float effectiveness = pilotStatus != null ? pilotStatus.ControlEffectiveness : 1f;
        return new Vector3(pitch, roll, yaw) * effectiveness;
    }

    public float GetThrottleInput()
    {
        if (owner == null || ownerBody == null) return 1f;
        return ownerBody.linearVelocity.magnitude
            < owner.levelFlightEquilibriumSpeed - throttleDeadZone ? 1f : 0f;
    }

    static Vector3 SafeNormalize(Vector3 value, Vector3 fallback)
    {
        if (IsValidVector(value) && value.sqrMagnitude > 0.0001f)
            return value.normalized;
        if (IsValidVector(fallback) && fallback.sqrMagnitude > 0.0001f)
            return fallback.normalized;
        return Vector3.forward;
    }

    static bool IsValidVector(Vector3 value)
    {
        return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }

    void OnDrawGizmosSelected()
    {
        if (trackingTarget == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(CurrentAimPoint, 2f);
        Gizmos.DrawLine(transform.position, CurrentAimPoint);
    }
}
