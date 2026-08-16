using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class AircraftController : MonoBehaviour
{
    [Header("Flight Parameters")]
    [Min(0f)] public float thrustPower = 20f;
    [Min(1f)] public float maxSpeed = 50f;
    [Min(0.1f)] public float stallSpeed = 20f;
    [Min(0f)] public float liftPower = 9.81f;
    [Min(0f)] public float dragPower = 0.01f;

    [Header("Control")]
    public Vector3 torquePower = new Vector3(12f, 10f, 8f);
    [Min(0f)] public float angularDamping = 1f;
    [Min(0f)] public float maxTurnRateDegrees = 30f;

    [Header("Runtime")]
    [Range(0f, 1f)] public float throttle = 1f;
    public Vector3 Torque { get; private set; }
    public Vector3 Velocity { get; private set; }

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
        rb.useGravity = true;
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

        // External acceleration is accumulated for exactly one physics step.
        rb.AddForce(accumulatedExternalAcceleration, ForceMode.Acceleration);
        accumulatedExternalAcceleration = Vector3.zero;

        Vector3 controlInput = Vector3.ClampMagnitude(GetControlInput() + externalControlAssist, 1f);
        externalControlAssist = Vector3.zero;
        throttle = Mathf.MoveTowards(throttle, Mathf.Clamp01(GetThrottleInput()), Time.fixedDeltaTime * 2f);

        float speed = rb.linearVelocity.magnitude;
        float speedRatio = Mathf.Clamp01(speed / Mathf.Max(1f, maxSpeed));
        float stallRatio = Mathf.Pow(Mathf.Clamp01(speed / Mathf.Max(0.1f, stallSpeed)), 2f);

        rb.AddForce(transform.forward * (thrustPower * throttle * (1f - speedRatio)), ForceMode.Acceleration);
        EnvironmentManager environment = EnvironmentManager.Instance;
        Vector3 airVelocity = environment != null
            ? environment.GetRelativeAirVelocity(transform.position, rb.linearVelocity)
            : rb.linearVelocity;
        float atmosphereScale = environment != null
            ? environment.airDensity / 1.225f * environment.airViscosityScale
            : 1f;
        rb.AddForce(-airVelocity * (dragPower * speedRatio * atmosphereScale), ForceMode.Acceleration);
        float upright = Mathf.Clamp01(Vector3.Dot(transform.up, Vector3.up));
        rb.AddForce(Vector3.up * (liftPower * speedRatio * upright), ForceMode.Acceleration);

        Vector3 pitch = transform.right * (-controlInput.x * torquePower.x * stallRatio);
        Vector3 roll = transform.forward * (-controlInput.y * torquePower.y * stallRatio);
        Vector3 yaw = transform.up * (controlInput.z * torquePower.z * stallRatio);
        Torque = pitch + roll + yaw;
        rb.AddTorque(Torque, ForceMode.Acceleration);

        rb.linearVelocity = Vector3.ClampMagnitude(rb.linearVelocity, maxSpeed);
        rb.maxAngularVelocity = maxTurnRateDegrees * Mathf.Deg2Rad;
        Velocity = rb.linearVelocity;

        if (!IsValidVector(rb.linearVelocity))
        {
            Debug.LogWarning("Invalid aircraft velocity was reset.", this);
            rb.linearVelocity = transform.forward * Mathf.Min(maxSpeed, 10f);
        }
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
