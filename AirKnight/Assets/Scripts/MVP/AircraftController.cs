using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody))]
public class AircraftController : MonoBehaviour
{
    [Header("Flight Parameters")]
    [Min(0f)] public float thrustPower = 20f;
    [FormerlySerializedAs("maxSpeed")]
    [Min(1f)] public float levelFlightEquilibriumSpeed = 50f;
    [Min(1f)] public float idealDiveEquilibriumSpeed = 60f;
    [Min(1f)] public float breakupSpeed = 75f;
    [FormerlySerializedAs("dragPower")]
    [Min(0f)] public float forwardDragCoefficient = 0.008f;

    [Header("Simplified Aerodynamics")]
    [Min(0.01f)] public float aoaMinimumSpeed = 1f;
    [Min(0f)] public float fuselageBottomArea = 20f;
    [Min(0f)] public float fuselageProjectedArea = 4f;
    [Min(0f)] public float wingBottomArea = 30f;
    [Min(0f)] public float wingProjectedArea = 2f;
    [Min(0f)] public float turnDragPower = 0.02f;
    [Min(0.01f)] public float dragReferenceArea = 10f;

    [Header("Altitude Limit")]
    [Min(0f)] public float altitudeLimitStart = 8000f;
    [Min(0f)] public float altitudeLimitFullStrengthAltitude = 10000f;
    [Min(0f)] public float maximumAltitudeLimitAcceleration = 98.1f;
    public AnimationCurve altitudeLimitCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Control Rates (deg/s)")]
    [FormerlySerializedAs("torquePower")]
    public Vector3 turnRateDegrees = new Vector3(45f, 135f, 30f);

    [Header("Runtime")]
    [Range(0f, 1f)] public float throttle = 1f;
    public Vector3 TurnRateDegrees { get; private set; }
    public Vector3 RotationDeltaDegrees { get; private set; }
    public Vector3 Velocity { get; private set; }
    public Vector3 ThrustVector { get; private set; }
    public Vector3 AltitudeLimitAcceleration { get; private set; }
    public float AngleOfAttack { get; private set; }
    public float EffectiveTurnArea { get; private set; }
    public float TurnDragAcceleration { get; private set; }

    protected Rigidbody rb;
    protected AircraftStatus aircraftStatus;
    Vector3 externalControlAssist;
    Vector3 accumulatedExternalAcceleration;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        aircraftStatus = GetComponent<AircraftStatus>();
        ConfigureRigidbody();
        aircraftStatus?.ApplyTo(this, rb);
    }

    protected virtual void Start()
    {
        rb.linearVelocity = transform.forward * levelFlightEquilibriumSpeed;
        if (!IsValidVector(rb.linearVelocity))
            rb.linearVelocity = Vector3.zero;
    }

    void ConfigureRigidbody()
    {
        // Gravity is integrated explicitly so the FM update order remains deterministic.
        rb.useGravity = false;
        rb.mass = 1f;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    protected virtual void FixedUpdate()
    {
        if (rb == null) return;

        Vector3 controlInput = Vector3.ClampMagnitude(GetControlInput() + externalControlAssist, 1f);
        externalControlAssist = Vector3.zero;
        throttle = Mathf.MoveTowards(throttle, Mathf.Clamp01(GetThrottleInput()), Time.fixedDeltaTime * 2f);

        // All aerodynamic calculations use the velocity captured at the beginning of this step.
        Vector3 previousVelocity = rb.linearVelocity;
        if (!IsValidVector(previousVelocity))
        {
            Debug.LogWarning("Invalid aircraft velocity was reset.", this);
            previousVelocity = transform.forward * Mathf.Min(levelFlightEquilibriumSpeed, 10f);
        }

        float deltaTime = Time.fixedDeltaTime;
        float speed = previousVelocity.magnitude;
        EnvironmentManager environment = EnvironmentManager.Instance;
        Vector3 relativeAirVelocity = environment != null
            ? environment.GetRelativeAirVelocity(transform.position, previousVelocity)
            : previousVelocity;
        float atmosphereScale = environment != null
            ? environment.airDensity / 1.225f * environment.airViscosityScale
            : 1f;

        // 1-2. Rigidbody performs movement from the previous velocity. Calculate signed pitch AOA,
        // then apply turn drag without allowing velocity to reverse in a single physics step.
        AngleOfAttack = CalculateSignedPitchAoa(previousVelocity);
        EffectiveTurnArea = CalculateEffectiveTurnArea(Mathf.Abs(AngleOfAttack) * Mathf.Deg2Rad);
        float airSpeed = relativeAirVelocity.magnitude;
        float forwardArea = fuselageProjectedArea + wingProjectedArea;
        float additionalTurnArea = Mathf.Max(0f, EffectiveTurnArea - forwardArea);
        float turnAreaScale = additionalTurnArea / Mathf.Max(0.01f, dragReferenceArea);
        TurnDragAcceleration = turnDragPower * atmosphereScale * turnAreaScale * airSpeed;

        Vector3 nextVelocity = ApplyDragWithoutReversal(
            previousVelocity,
            relativeAirVelocity,
            TurnDragAcceleration,
            deltaTime);

        float forwardDragAcceleration = forwardDragCoefficient
            * atmosphereScale
            * airSpeed
            * airSpeed;
        Vector3 remainingRelativeAirVelocity = environment != null
            ? environment.GetRelativeAirVelocity(transform.position, nextVelocity)
            : nextVelocity;
        nextVelocity = ApplyDragWithoutReversal(
            nextVelocity,
            remainingRelativeAirVelocity,
            forwardDragAcceleration,
            deltaTime);

        // 3. Thrust is independent of the velocity direction and follows the aircraft nose.
        ThrustVector = transform.forward * (thrustPower * throttle);
        nextVelocity += ThrustVector * deltaTime;

        // 4. Lift is omitted. Scale gravity by the absolute vertical alignment of the nose so
        // level flight receives no gravity and a vertical climb/dive receives full gravity.
        Vector3 environmentGravity = environment != null ? environment.gravity : Physics.gravity;
        Vector3 gravityAcceleration = CalculateSimplifiedGravityAcceleration(environmentGravity);
        AltitudeLimitAcceleration = CalculateAltitudeLimitAcceleration(transform.position.y);
        nextVelocity += (accumulatedExternalAcceleration + gravityAcceleration + AltitudeLimitAcceleration) * deltaTime;
        accumulatedExternalAcceleration = Vector3.zero;

        float pitchPerformance = aircraftStatus != null
            ? aircraftStatus.EvaluatePitchPerformance(speed)
            : turnRateDegrees.x;
        float pitchRate = Mathf.Max(0f, pitchPerformance);
        float rollRate = Mathf.Max(0f, turnRateDegrees.y);
        float yawRate = Mathf.Max(0f, turnRateDegrees.z);
        TurnRateDegrees = new Vector3(
            -controlInput.x * pitchRate,
            controlInput.z * yawRate,
            -controlInput.y * rollRate);
        RotationDeltaDegrees = TurnRateDegrees * deltaTime;

        rb.linearVelocity = Vector3.ClampMagnitude(nextVelocity, Mathf.Max(1f, breakupSpeed));
        ApplyDirectRotation(RotationDeltaDegrees);
        Velocity = rb.linearVelocity;

        if (!IsValidVector(rb.linearVelocity))
        {
            Debug.LogWarning("Invalid aircraft velocity was reset.", this);
            rb.linearVelocity = transform.forward * Mathf.Min(levelFlightEquilibriumSpeed, 10f);
        }
    }

    float CalculateSignedPitchAoa(Vector3 velocity)
    {
        if (velocity.sqrMagnitude < aoaMinimumSpeed * aoaMinimumSpeed)
            return 0f;

        Vector3 pitchPlaneVelocity = Vector3.ProjectOnPlane(velocity, transform.right);
        if (pitchPlaneVelocity.sqrMagnitude < 0.0001f)
            return 0f;

        return Vector3.SignedAngle(transform.forward, pitchPlaneVelocity.normalized, transform.right);
    }

    float CalculateEffectiveTurnArea(float absoluteAoaRadians)
    {
        float angle = Mathf.Clamp(absoluteAoaRadians, 0f, Mathf.PI * 0.5f);
        float sin = Mathf.Sin(angle);
        float cos = Mathf.Cos(angle);
        float fuselageArea = sin * fuselageBottomArea + cos * fuselageProjectedArea;
        float wingArea = sin * wingBottomArea + cos * wingProjectedArea;
        return Mathf.Max(0f, fuselageArea + wingArea);
    }

    static Vector3 ApplyDragWithoutReversal(
        Vector3 velocity,
        Vector3 relativeAirVelocity,
        float acceleration,
        float deltaTime)
    {
        float airSpeed = relativeAirVelocity.magnitude;
        if (airSpeed <= 0.0001f || acceleration <= 0f)
            return velocity;

        float velocityChange = Mathf.Min(airSpeed, acceleration * deltaTime);
        return velocity - relativeAirVelocity / airSpeed * velocityChange;
    }

    Vector3 CalculateSimplifiedGravityAcceleration(Vector3 environmentGravity)
    {
        if (!IsValidVector(environmentGravity) || environmentGravity.sqrMagnitude <= 0.0001f)
            return Vector3.zero;

        float gravityScale = Mathf.Abs(Vector3.Dot(
            transform.forward,
            environmentGravity.normalized));
        return environmentGravity * gravityScale;
    }

    Vector3 CalculateAltitudeLimitAcceleration(float worldAltitude)
    {
        if (worldAltitude <= altitudeLimitStart || maximumAltitudeLimitAcceleration <= 0f)
            return Vector3.zero;

        float fullStrengthAltitude = Mathf.Max(
            altitudeLimitStart + 0.01f,
            altitudeLimitFullStrengthAltitude);
        float normalizedAltitude = Mathf.InverseLerp(
            altitudeLimitStart,
            fullStrengthAltitude,
            worldAltitude);
        float curveValue = altitudeLimitCurve != null
            ? altitudeLimitCurve.Evaluate(normalizedAltitude)
            : normalizedAltitude;
        return Vector3.down * (Mathf.Max(0f, curveValue) * maximumAltitudeLimitAcceleration);
    }

    void ApplyDirectRotation(Vector3 localRotationDeltaDegrees)
    {
        Quaternion targetRotation = rb.rotation * Quaternion.Euler(localRotationDeltaDegrees);
        rb.angularVelocity = Vector3.zero;
        rb.MoveRotation(targetRotation);
    }

    protected virtual Vector3 GetControlInput() => Vector3.zero;
    protected virtual float GetThrottleInput() => 1f;

    public void AddControlAssist(Vector3 assistInput)
    {
        if (IsValidVector(assistInput))
            externalControlAssist += Vector3.ClampMagnitude(assistInput, 1f);
    }

    public void AddExternalAcceleration(Vector3 acceleration)
    {
        if (!IsValidVector(acceleration))
        {
            Debug.LogWarning("Invalid external acceleration was ignored.", this);
            return;
        }

        accumulatedExternalAcceleration += acceleration;
    }

    protected static bool IsValidVector(Vector3 value)
    {
        return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
