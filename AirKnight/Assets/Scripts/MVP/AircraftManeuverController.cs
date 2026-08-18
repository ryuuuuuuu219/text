using UnityEngine;

public enum AircraftManeuverPriority
{
    Tracking,
    Acceleration
}

[DisallowMultipleComponent]
public sealed class AircraftManeuverController : MonoBehaviour
{
    const float RearTargetLateralDeadZone = 0.0001f;
    const float RearTargetFallbackRollInput = 0.01f;

    [Header("Pursuit")]
    [SerializeField, Min(0f)] float leadTime = 0.5f;
    [SerializeField, Min(0f)] float commandDirectionSmoothing = 6f;

    [Header("Steering")]
    [SerializeField, Min(0f)] float steeringGain = 2f;
    [SerializeField, Min(0f)] float rollGain = 1.5f;
    [SerializeField, Min(0f)] float yawGain = 2f;

    [Header("Tactical Judgment")]
    [SerializeField, Min(0f)] float speedThreshold = 25f;
    [SerializeField, Min(0f)] float decelerationThreshold = 18f;
    [SerializeField, Min(0f)] float prioritySwitchBaseCooldown = 0.5f;
    [SerializeField, Min(0f)] float staminaCooldownPenalty = 2f;
    [SerializeField, Min(0f)] float accelerationPriorityPitchDecay = 0.25f;

    [Header("Throttle")]
    [SerializeField, Min(0f)] float throttleDeadZone = 0.5f;

    AircraftFlightAI owner;
    AircraftFlightAI trackingTarget;
    Rigidbody ownerBody;
    Rigidbody targetBody;
    PilotStatus pilotStatus;
    Vector3 commandedFlightDirection;
    float previousSpeed;
    float nextPrioritySwitchTime;
    float fallbackPitchInputMemory;
    bool hasPreviousSpeed;
    bool wasPitching;

    public AircraftFlightAI TrackingTarget => trackingTarget;
    public Vector3 CurrentAimPoint { get; private set; }
    public AircraftManeuverPriority ManeuverPriority { get; private set; }
    public float CurrentSpeed { get; private set; }
    public float CurrentDeceleration { get; private set; }
    public float SpeedThreshold => Mathf.Max(0f, pilotStatus != null
        ? pilotStatus.accelerationPrioritySpeedThreshold
        : speedThreshold);
    public float DecelerationThreshold => Mathf.Max(0f, pilotStatus != null
        ? pilotStatus.accelerationPriorityDecelerationThreshold
        : decelerationThreshold);

    public void Initialize(AircraftFlightAI aircraft)
    {
        owner = aircraft;
        ownerBody = GetComponent<Rigidbody>();
        pilotStatus = GetComponent<PilotStatus>();
        commandedFlightDirection = owner != null ? owner.transform.forward : transform.forward;
        CurrentAimPoint = transform.position + commandedFlightDirection;
        ManeuverPriority = AircraftManeuverPriority.Tracking;
        SetPitchInputMemory(0f);
        previousSpeed = ownerBody != null ? ownerBody.linearVelocity.magnitude : 0f;
        hasPreviousSpeed = ownerBody != null;
    }

    public void SetTrackingTarget(AircraftFlightAI target)
    {
        bool targetChanged = trackingTarget != target;
        trackingTarget = target;
        targetBody = trackingTarget != null ? trackingTarget.GetComponent<Rigidbody>() : null;
        if (!targetChanged) return;

        SetPitchInputMemory(0f);
        wasPitching = false;
    }

    public Vector3 GetControlInput()
    {
        ObserveSpeed();
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
        Vector3 localDesiredDirection = owner.transform.InverseTransformDirection(desiredDirection);
        float requestedPitch = Mathf.Clamp(localDirection.y * steeringGain, -1f, 1f);
        float roll = Mathf.Clamp(localDirection.x * rollGain, -1f, 1f);
        float yaw = Mathf.Clamp(localDirection.x * yawGain, -1f, 1f);
        if (localDesiredDirection.z < 0f &&
            Mathf.Abs(localDesiredDirection.x) < RearTargetLateralDeadZone)
        {
            roll = RearTargetFallbackRollInput;
        }

        UpdateManeuverPriority();
        float pitch = CalculatePitchInput(requestedPitch);
        float effectiveness = pilotStatus != null ? pilotStatus.ControlEffectiveness : 1f;
        return new Vector3(pitch, roll, yaw) * effectiveness;
    }

    void ObserveSpeed()
    {
        if (ownerBody == null)
        {
            CurrentSpeed = 0f;
            CurrentDeceleration = 0f;
            hasPreviousSpeed = false;
            return;
        }

        CurrentSpeed = ownerBody.linearVelocity.magnitude;
        float deltaTime = Time.fixedDeltaTime;
        CurrentDeceleration = hasPreviousSpeed && deltaTime > 0f
            ? Mathf.Max(0f, (previousSpeed - CurrentSpeed) / deltaTime)
            : 0f;
        previousSpeed = CurrentSpeed;
        hasPreviousSpeed = true;
    }

    void UpdateManeuverPriority()
    {
        float staminaRatio = pilotStatus != null ? pilotStatus.ShortTermStaminaRatio : 1f;
        bool staminaDepleted = pilotStatus != null &&
                               staminaRatio < pilotStatus.shortTermPenaltyThreshold;
        bool accelerationRequired = staminaDepleted ||
                                    CurrentSpeed <= SpeedThreshold ||
                                    CurrentDeceleration >= DecelerationThreshold;
        AircraftManeuverPriority desiredPriority = accelerationRequired
            ? AircraftManeuverPriority.Acceleration
            : AircraftManeuverPriority.Tracking;

        if (desiredPriority == ManeuverPriority) return;
        if (!staminaDepleted && Time.time < nextPrioritySwitchTime) return;

        ManeuverPriority = desiredPriority;
        float cooldown = prioritySwitchBaseCooldown +
                         (1f - Mathf.Clamp01(staminaRatio)) * staminaCooldownPenalty;
        nextPrioritySwitchTime = Time.time + cooldown;
        wasPitching = false;
    }

    float CalculatePitchInput(float requestedPitch)
    {
        float pitchMemory = GetPitchInputMemory();
        if (ManeuverPriority == AircraftManeuverPriority.Acceleration)
        {
            pitchMemory = Mathf.MoveTowards(
                pitchMemory,
                0f,
                accelerationPriorityPitchDecay * Time.fixedDeltaTime);
            SetPitchInputMemory(pitchMemory);
            wasPitching = false;
            return pitchMemory;
        }

        bool isPitching = Mathf.Abs(requestedPitch) > 0.01f;
        bool pitchDirectionChanged = isPitching && pitchMemory != 0f &&
                                     Mathf.Sign(requestedPitch) != Mathf.Sign(pitchMemory);
        if (isPitching && (!wasPitching || pitchDirectionChanged))
        {
            pitchMemory = requestedPitch;
            SetPitchInputMemory(pitchMemory);
        }
        else if (!isPitching)
        {
            pitchMemory = 0f;
            SetPitchInputMemory(0f);
        }

        wasPitching = isPitching;
        return isPitching ? pitchMemory : 0f;
    }

    float GetPitchInputMemory()
    {
        return pilotStatus != null ? pilotStatus.pitchInputMemory : fallbackPitchInputMemory;
    }

    void SetPitchInputMemory(float value)
    {
        value = Mathf.Clamp(value, -1f, 1f);
        if (pilotStatus != null) pilotStatus.pitchInputMemory = value;
        else fallbackPitchInputMemory = value;
    }

    void OnValidate()
    {
        speedThreshold = Mathf.Max(0f, speedThreshold);
        decelerationThreshold = Mathf.Max(0f, decelerationThreshold);
        prioritySwitchBaseCooldown = Mathf.Max(0f, prioritySwitchBaseCooldown);
        staminaCooldownPenalty = Mathf.Max(0f, staminaCooldownPenalty);
        accelerationPriorityPitchDecay = Mathf.Max(0f, accelerationPriorityPitchDecay);
    }

    public float GetThrottleInput()
    {
        if (owner == null || ownerBody == null) return 1f;
        if (ManeuverPriority == AircraftManeuverPriority.Acceleration) return 1f;
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
