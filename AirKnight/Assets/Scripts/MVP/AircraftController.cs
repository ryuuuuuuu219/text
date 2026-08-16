using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class AircraftController : MonoBehaviour
{
    [Header("Flight Parameters")]
    [Min(0f)] public float thrustPower = 20f;
    [Min(1f)] public float maxSpeed = 50f;
    [Min(0.1f)] public float stallSpeed = 20f;
    [Min(0f)] public float dragPower = 0.01f;

    [Header("Simplified Aerodynamics")]
    [Min(0.01f)] public float aoaMinimumSpeed = 1f;
    [Min(0f)] public float fuselageBottomArea = 20f;
    [Min(0f)] public float fuselageProjectedArea = 4f;
    [Min(0f)] public float wingBottomArea = 30f;
    [Min(0f)] public float wingProjectedArea = 2f;
    [Min(0f)] public float turnDragPower = 0.02f;
    [Min(0.01f)] public float dragReferenceArea = 10f;

    [Header("Altitude Limit")]
    [Min(0f)] public float altitudeLimitStart = 1000f;
    [Min(0f)] public float altitudeLimitFullStrengthAltitude = 3000f;
    [Min(0f)] public float maximumAltitudeLimitAcceleration = 19.62f;
    public AnimationCurve altitudeLimitCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Attitude Limits")]
    [Range(0f, 90f)] public float maximumPitchAngle = 90f;
    [Range(0f, 90f)] public float maximumRollAngle = 90f;

    [Header("Control")]
    public Vector3 torquePower = new Vector3(12f, 10f, 8f);
    [Min(0f)] public float angularDamping = 1f;
    [Min(0f)] public float maxTurnRateDegrees = 30f;

    [Header("Runtime")]
    [Range(0f, 1f)] public float throttle = 1f;
    public Vector3 Torque { get; private set; }
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
        rb.linearVelocity = transform.forward * maxSpeed;
        if (!IsValidVector(rb.linearVelocity))
            rb.linearVelocity = Vector3.zero;
    }

    void ConfigureRigidbody()
    {
        // Gravity is integrated explicitly so the FM update order remains deterministic.
        rb.useGravity = false;
        rb.mass = 1f;
        rb.linearDamping = 0f;
        rb.angularDamping = angularDamping;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.maxAngularVelocity = maxTurnRateDegrees * Mathf.Deg2Rad;
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
            previousVelocity = transform.forward * Mathf.Min(maxSpeed, 10f);
        }

        float deltaTime = Time.fixedDeltaTime;
        float speed = previousVelocity.magnitude;
        float speedRatio = Mathf.Clamp01(speed / Mathf.Max(1f, maxSpeed));
        float stallRatio = Mathf.Pow(Mathf.Clamp01(speed / Mathf.Max(0.1f, stallSpeed)), 2f);
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
        float areaScale = EffectiveTurnArea / Mathf.Max(0.01f, dragReferenceArea);
        TurnDragAcceleration = turnDragPower * atmosphereScale * areaScale * airSpeed;

        Vector3 nextVelocity = ApplyDragWithoutReversal(
            previousVelocity,
            relativeAirVelocity,
            TurnDragAcceleration,
            deltaTime);

        float forwardArea = fuselageProjectedArea + wingProjectedArea;
        float forwardDragAcceleration = dragPower
            * atmosphereScale
            * (forwardArea / Mathf.Max(0.01f, dragReferenceArea))
            * airSpeed
            * speedRatio;
        Vector3 remainingRelativeAirVelocity = environment != null
            ? environment.GetRelativeAirVelocity(transform.position, nextVelocity)
            : nextVelocity;
        nextVelocity = ApplyDragWithoutReversal(
            nextVelocity,
            remainingRelativeAirVelocity,
            forwardDragAcceleration,
            deltaTime);

        // 3. Thrust is independent of the velocity direction and follows the aircraft nose.
        ThrustVector = transform.forward * (thrustPower * throttle * (1f - speedRatio));
        nextVelocity += ThrustVector * deltaTime;

        // 4. External acceleration, normal gravity and altitude-limit gravity are separate vectors.
        Vector3 gravityAcceleration = environment != null ? environment.gravity : Physics.gravity;
        AltitudeLimitAcceleration = CalculateAltitudeLimitAcceleration(transform.position.y);
        nextVelocity += (accumulatedExternalAcceleration + gravityAcceleration + AltitudeLimitAcceleration) * deltaTime;
        accumulatedExternalAcceleration = Vector3.zero;

        float pitchPerformance = aircraftStatus != null
            ? aircraftStatus.EvaluatePitchPerformance(speed)
            : torquePower.x;
        Vector3 pitch = transform.right * (-controlInput.x * pitchPerformance * stallRatio);
        Vector3 roll = transform.forward * (-controlInput.y * torquePower.y * stallRatio);
        Vector3 yaw = transform.up * (controlInput.z * torquePower.z * stallRatio);
        Torque = pitch + roll + yaw;
        rb.AddTorque(Torque, ForceMode.Acceleration);

        rb.linearVelocity = Vector3.ClampMagnitude(nextVelocity, maxSpeed);
        rb.maxAngularVelocity = maxTurnRateDegrees * Mathf.Deg2Rad;
        ConstrainPitchAndRoll();
        Velocity = rb.linearVelocity;

        if (!IsValidVector(rb.linearVelocity))
        {
            Debug.LogWarning("Invalid aircraft velocity was reset.", this);
            rb.linearVelocity = transform.forward * Mathf.Min(maxSpeed, 10f);
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

    void ConstrainPitchAndRoll()
    {
        Vector3 euler = rb.rotation.eulerAngles;
        float pitch = NormalizeSignedAngle(euler.x);
        float roll = NormalizeSignedAngle(euler.z);
        float clampedPitch = Mathf.Clamp(pitch, -maximumPitchAngle, maximumPitchAngle);
        float clampedRoll = Mathf.Clamp(roll, -maximumRollAngle, maximumRollAngle);

        if (!Mathf.Approximately(pitch, clampedPitch) || !Mathf.Approximately(roll, clampedRoll))
            rb.MoveRotation(Quaternion.Euler(clampedPitch, euler.y, clampedRoll));

        Vector3 localAngularVelocity = transform.InverseTransformDirection(rb.angularVelocity);
        if ((clampedPitch >= maximumPitchAngle && localAngularVelocity.x > 0f)
            || (clampedPitch <= -maximumPitchAngle && localAngularVelocity.x < 0f))
            localAngularVelocity.x = 0f;
        if ((clampedRoll >= maximumRollAngle && localAngularVelocity.z > 0f)
            || (clampedRoll <= -maximumRollAngle && localAngularVelocity.z < 0f))
            localAngularVelocity.z = 0f;
        rb.angularVelocity = transform.TransformDirection(localAngularVelocity);
    }

    static float NormalizeSignedAngle(float angle)
    {
        return Mathf.DeltaAngle(0f, angle);
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
