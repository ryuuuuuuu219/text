using UnityEngine;
using UnityEngine.Serialization;

public enum AircraftManeuverPriority
{
    Tracking,
    Acceleration,
    AltitudeRecovery
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
    [SerializeField, Min(0f)] float losPitchDeadZoneDegrees = 5f;
    [SerializeField, Min(0f)] float losPitchRateGain = 3f;
    [SerializeField, Min(0f)] float minimumPitchSpeedScale = 0.5f;
    [SerializeField, Min(0f)] float maximumPitchSpeedScale = 2f;
    [SerializeField, Min(0f)] float pitchInputAdjustmentRate = 2f;
    [SerializeField, Min(0f)] float aoaErrorDeadZone = 3f;

    [Header("Tactical Judgment")]
    [SerializeField, Min(0f)] float speedThreshold = 25f;
    [SerializeField, Min(0f)] float decelerationThreshold = 18f;
    [SerializeField, Min(0f)] float prioritySwitchBaseCooldown = 0.5f;
    [SerializeField, Min(0f)] float staminaCooldownPenalty = 2f;
    [FormerlySerializedAs("accelerationPriorityPitchDecay")]
    [SerializeField, Min(0f)] float targetAoaAdjustmentRate = 0.25f;

    [Header("Altitude Recovery")]
    [SerializeField, Min(0f)] float altitudeRecoveryThreshold = 1500f;
    [SerializeField, Range(0f, 90f)] float altitudeRecoveryPitchAllowedRollAngle = 25f;
    [SerializeField, Range(-89f, 89f)] float altitudeRecoveryTargetPitch = 30f;
    [SerializeField, Range(0f, 45f)] float altitudeRecoveryTargetAngleOfAttack = 30f;

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
    public float CurrentAltitude => transform.position.y;
    public float AltitudeRecoveryThreshold => Mathf.Max(0f, pilotStatus != null
        ? pilotStatus.altitudeRecoveryThreshold
        : altitudeRecoveryThreshold);
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
        UpdateManeuverPriority();
        if (owner == null)
        {
            SignedTargetAngleOfAttack = 0f;
            AngleOfAttackError = 0f;
            PitchInput = 0f;
            return Vector3.zero;
        }

        Vector3 desiredDirection;
        if (ManeuverPriority == AircraftManeuverPriority.AltitudeRecovery)
        {
            UpdateTargetAngleOfAttack();
            return CalculateAltitudeRecoveryControlInput();
        }
        else
        {
            if (trackingTarget == null)
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
            desiredDirection = SafeNormalize(
                CurrentAimPoint - owner.transform.position,
                owner.transform.forward);
        }

        float blend = commandDirectionSmoothing <= 0f
            ? 1f
            : 1f - Mathf.Exp(-commandDirectionSmoothing * Time.fixedDeltaTime);
        commandedFlightDirection = SafeNormalize(
            Vector3.Slerp(commandedFlightDirection, desiredDirection, blend),
            desiredDirection);

        Vector3 localDirection = owner.transform.InverseTransformDirection(commandedFlightDirection);
        Vector3 localDesiredDirection = owner.transform.InverseTransformDirection(desiredDirection);
        float losPitchErrorDegrees = Mathf.Asin(
            Mathf.Clamp(localDirection.y, -1f, 1f)) * Mathf.Rad2Deg;
        float roll = Mathf.Clamp(localDirection.x * rollGain, -1f, 1f);
        float yaw = Mathf.Clamp(localDirection.x * yawGain, -1f, 1f);
        if (localDesiredDirection.z < 0f &&
            Mathf.Abs(localDesiredDirection.x) < RearTargetLateralDeadZone)
        {
            roll = RearTargetFallbackRollInput;
        }

        UpdateTargetAngleOfAttack();
        float pitch = CalculateTrackingPitchInput(losPitchErrorDegrees);
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
        bool altitudeRecoveryRequired = CurrentAltitude < AltitudeRecoveryThreshold;
        if (altitudeRecoveryRequired)
        {
            ManeuverPriority = AircraftManeuverPriority.AltitudeRecovery;
            return;
        }

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
        if (ManeuverPriority == AircraftManeuverPriority.AltitudeRecovery)
        {
            ManeuverPriority = desiredPriority;
            nextPrioritySwitchTime = Time.time + prioritySwitchBaseCooldown;
            return;
        }
        if (!staminaDepleted && Time.time < nextPrioritySwitchTime) return;

        ManeuverPriority = desiredPriority;
        float cooldown = prioritySwitchBaseCooldown +
                         (1f - Mathf.Clamp01(staminaRatio)) * staminaCooldownPenalty;
        nextPrioritySwitchTime = Time.time + cooldown;
    }

    Vector3 CalculateAltitudeRecoveryControlInput()
    {
        float signedRollAngle = GetSignedRollAngle();
        float rollDivisor = Mathf.Max(1f, altitudeRecoveryPitchAllowedRollAngle);
        float roll = Mathf.Clamp(signedRollAngle / rollDivisor, -1f, 1f);

        float currentPitch = Mathf.Asin(Mathf.Clamp(
            owner.transform.forward.y,
            -1f,
            1f)) * Mathf.Rad2Deg;
        float pitchError = altitudeRecoveryTargetPitch - currentPitch;
        bool pitchAllowed = Mathf.Abs(signedRollAngle) <= altitudeRecoveryPitchAllowedRollAngle;
        float pitch;
        if (!pitchAllowed || Mathf.Abs(pitchError) <= 0.1f)
        {
            PitchInput = 0f;
            SignedTargetAngleOfAttack = 0f;
            AngleOfAttackError = 0f;
            pitch = 0f;
        }
        else
        {
            float requestedPitch = Mathf.Clamp(
                Mathf.Sin(pitchError * Mathf.Deg2Rad) * steeringGain,
                -1f,
                1f);
            pitch = CalculatePitchInput(requestedPitch);
        }

        Vector3 horizontalDirection = SafeNormalize(
            Vector3.ProjectOnPlane(owner.transform.forward, Vector3.up),
            Vector3.forward);
        float targetPitchRadians = altitudeRecoveryTargetPitch * Mathf.Deg2Rad;
        Vector3 targetDirection = SafeNormalize(
            horizontalDirection * Mathf.Cos(targetPitchRadians) +
            Vector3.up * Mathf.Sin(targetPitchRadians),
            Vector3.up);
        CurrentAimPoint = owner.transform.position + targetDirection * 100f;

        float effectiveness = pilotStatus != null ? pilotStatus.ControlEffectiveness : 1f;
        return new Vector3(pitch, roll, 0f) * effectiveness;
    }

    float GetSignedRollAngle()
    {
        Vector3 forward = owner.transform.forward;
        Vector3 levelUp = Vector3.ProjectOnPlane(Vector3.up, forward);
        if (levelUp.sqrMagnitude <= 0.0001f)
            return 0f;
        return Vector3.SignedAngle(levelUp.normalized, owner.transform.up, forward);
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
        if (ManeuverPriority == AircraftManeuverPriority.AltitudeRecovery)
        {
            TargetAngleOfAttack = altitudeRecoveryTargetAngleOfAttack;
            return;
        }

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

    float CalculateTrackingPitchInput(float losPitchErrorDegrees)
    {
        float effectiveLosError = Mathf.Sign(losPitchErrorDegrees) * Mathf.Max(
            0f,
            Mathf.Abs(losPitchErrorDegrees) - losPitchDeadZoneDegrees);
        if (Mathf.Approximately(effectiveLosError, 0f))
        {
            SignedTargetAngleOfAttack = 0f;
            AngleOfAttackError = 0f;
            return MovePitchInputTowards(0f);
        }

        float minimumSpeedScale = Mathf.Min(minimumPitchSpeedScale, maximumPitchSpeedScale);
        float maximumSpeedScale = Mathf.Max(minimumPitchSpeedScale, maximumPitchSpeedScale);
        float speedScale = Mathf.Clamp(
            Mathf.Max(0f, owner.levelFlightEquilibriumSpeed) / Mathf.Max(CurrentSpeed, 1f),
            minimumSpeedScale,
            maximumSpeedScale);

        float maximumPitchRate = Mathf.Max(0f, owner.pitchPerformance);
        if (maximumPitchRate <= 0f)
        {
            SignedTargetAngleOfAttack = 0f;
            AngleOfAttackError = 0f;
            return MovePitchInputTowards(0f);
        }

        float desiredPitchRate = Mathf.Clamp(
            effectiveLosError * losPitchRateGain * speedScale,
            -maximumPitchRate,
            maximumPitchRate);
        float desiredPitchInput = desiredPitchRate / maximumPitchRate;

        float inputSign = Mathf.Sign(desiredPitchInput);
        SignedTargetAngleOfAttack = inputSign * TargetAngleOfAttack;
        AngleOfAttackError = SignedTargetAngleOfAttack - owner.AngleOfAttack;

        // Target AOA is a symmetric limit. Fade the command near the limit so the
        // interpolated input does not continue driving through it at full strength.
        float remainingAoaMargin = inputSign > 0f
            ? TargetAngleOfAttack - owner.AngleOfAttack
            : TargetAngleOfAttack + owner.AngleOfAttack;
        if (remainingAoaMargin <= 0f)
        {
            desiredPitchInput = 0f;
        }
        else if (aoaErrorDeadZone > 0f && remainingAoaMargin < aoaErrorDeadZone)
        {
            desiredPitchInput *= remainingAoaMargin / aoaErrorDeadZone;
        }

        return MovePitchInputTowards(desiredPitchInput);
    }

    float MovePitchInputTowards(float targetInput)
    {
        PitchInput = Mathf.MoveTowards(
            PitchInput,
            Mathf.Clamp(targetInput, -1f, 1f),
            pitchInputAdjustmentRate * Time.fixedDeltaTime);
        return PitchInput;
    }

    void OnValidate()
    {
        speedThreshold = Mathf.Max(0f, speedThreshold);
        decelerationThreshold = Mathf.Max(0f, decelerationThreshold);
        prioritySwitchBaseCooldown = Mathf.Max(0f, prioritySwitchBaseCooldown);
        staminaCooldownPenalty = Mathf.Max(0f, staminaCooldownPenalty);
        targetAoaAdjustmentRate = Mathf.Max(0f, targetAoaAdjustmentRate);
        altitudeRecoveryThreshold = Mathf.Max(0f, altitudeRecoveryThreshold);
        altitudeRecoveryPitchAllowedRollAngle = Mathf.Clamp(
            altitudeRecoveryPitchAllowedRollAngle,
            0f,
            90f);
        altitudeRecoveryTargetPitch = Mathf.Clamp(altitudeRecoveryTargetPitch, -89f, 89f);
        altitudeRecoveryTargetAngleOfAttack = Mathf.Clamp(
            altitudeRecoveryTargetAngleOfAttack,
            0f,
            45f);
        losPitchDeadZoneDegrees = Mathf.Max(0f, losPitchDeadZoneDegrees);
        losPitchRateGain = Mathf.Max(0f, losPitchRateGain);
        minimumPitchSpeedScale = Mathf.Max(0f, minimumPitchSpeedScale);
        maximumPitchSpeedScale = Mathf.Max(0f, maximumPitchSpeedScale);
        pitchInputAdjustmentRate = Mathf.Max(0f, pitchInputAdjustmentRate);
        aoaErrorDeadZone = Mathf.Max(0f, aoaErrorDeadZone);
    }

    public float GetThrottleInput()
    {
        if (owner == null || ownerBody == null) return 1f;
        if (ManeuverPriority == AircraftManeuverPriority.Acceleration ||
            ManeuverPriority == AircraftManeuverPriority.AltitudeRecovery)
            return 1f;
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
