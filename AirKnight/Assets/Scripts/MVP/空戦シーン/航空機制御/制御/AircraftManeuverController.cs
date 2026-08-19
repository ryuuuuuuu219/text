using UnityEngine;
using UnityEngine.Serialization;

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
    [SerializeField, Min(0f)] float pitchInputAdjustmentRate = 2f;
    [SerializeField, Min(0f)] float aoaErrorDeadZone = 0.1f;

    [Header("Tactical Judgment")]
    [SerializeField, Min(0f)] float speedThreshold = 25f;
    [SerializeField, Min(0f)] float decelerationThreshold = 18f;
    [SerializeField, Min(0f)] float prioritySwitchBaseCooldown = 0.5f;
    [SerializeField, Min(0f)] float staminaCooldownPenalty = 2f;
    [FormerlySerializedAs("accelerationPriorityPitchDecay")]
    [SerializeField, Min(0f)] float targetAoaAdjustmentRate = 0.25f;

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
    float targetAoaScale = 1f;
    bool hasPreviousSpeed;

    public AircraftFlightAI TrackingTarget => trackingTarget;
    public Vector3 CurrentAimPoint { get; private set; }
    public AircraftManeuverPriority ManeuverPriority { get; private set; }
    public float CurrentSpeed { get; private set; }
    public float CurrentDeceleration { get; private set; }
    public float TargetAngleOfAttack { get; private set; }
    public float SignedTargetAngleOfAttack { get; private set; }
    public float AngleOfAttackError { get; private set; }
    public float PitchInput { get; private set; }
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
        targetAoaScale = 1f;
        TargetAngleOfAttack = GetTrackingTargetAngleOfAttack();
        previousSpeed = ownerBody != null ? ownerBody.linearVelocity.magnitude : 0f;
        hasPreviousSpeed = ownerBody != null;
    }

    public void SetTrackingTarget(AircraftFlightAI target)
    {
        trackingTarget = target;
        targetBody = trackingTarget != null ? trackingTarget.GetComponent<Rigidbody>() : null;
    }

    public Vector3 GetControlInput()
    {
        ObserveSpeed();
        if (owner == null || trackingTarget == null)
        {
            SignedTargetAngleOfAttack = 0f;
            AngleOfAttackError = 0f;
            PitchInput = 0f;
            return Vector3.zero;
        }

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
        UpdateTargetAngleOfAttack();
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
    }

    float CalculatePitchInput(float requestedPitch)
    {
        SignedTargetAngleOfAttack = Mathf.Abs(requestedPitch) > 0.01f
            ? Mathf.Sign(requestedPitch) * TargetAngleOfAttack
            : 0f;
        AngleOfAttackError = SignedTargetAngleOfAttack - owner.AngleOfAttack;
        float inputDelta = pitchInputAdjustmentRate * Time.fixedDeltaTime;
        if (Mathf.Abs(AngleOfAttackError) <= aoaErrorDeadZone)
        {
            PitchInput = Mathf.MoveTowards(PitchInput, 0f, inputDelta);
        }
        else
        {
            PitchInput = Mathf.Clamp(
                PitchInput + Mathf.Sign(AngleOfAttackError) * inputDelta,
                -1f,
                1f);
        }
        return PitchInput;
    }

    void UpdateTargetAngleOfAttack()
    {
        float desiredScale = ManeuverPriority == AircraftManeuverPriority.Acceleration
            ? 0f
            : 1f;
        targetAoaScale = Mathf.MoveTowards(
            targetAoaScale,
            desiredScale,
            targetAoaAdjustmentRate * Time.fixedDeltaTime);
        TargetAngleOfAttack = Mathf.Lerp(
            GetAccelerationTargetAngleOfAttack(),
            GetTrackingTargetAngleOfAttack(),
            targetAoaScale);
    }

    float GetTrackingTargetAngleOfAttack()
    {
        return pilotStatus != null
            ? Mathf.Max(0f, pilotStatus.targetAngleOfAttack)
            : 30f;
    }

    float GetAccelerationTargetAngleOfAttack()
    {
        return pilotStatus != null
            ? Mathf.Max(0f, pilotStatus.accelerationTargetAngleOfAttack)
            : 8f;
    }

    void OnValidate()
    {
        speedThreshold = Mathf.Max(0f, speedThreshold);
        decelerationThreshold = Mathf.Max(0f, decelerationThreshold);
        prioritySwitchBaseCooldown = Mathf.Max(0f, prioritySwitchBaseCooldown);
        staminaCooldownPenalty = Mathf.Max(0f, staminaCooldownPenalty);
        targetAoaAdjustmentRate = Mathf.Max(0f, targetAoaAdjustmentRate);
        pitchInputAdjustmentRate = Mathf.Max(0f, pitchInputAdjustmentRate);
        aoaErrorDeadZone = Mathf.Max(0f, aoaErrorDeadZone);
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
