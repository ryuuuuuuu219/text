using UnityEngine;

[RequireComponent(typeof(Rigidbody),typeof(AugumentStatus))]
public class AircraftController : MonoBehaviour
{
    [Header("Flight Parameters")]
    public float thrustPower;      // 推力
    public float maxSpeed;        // 最高速度
    public float stallSpeed = 60f;       // 失速速度
    public float liftPower = 9.8f;       // 揚力（重力軽減）
    public float dragPower = 0.1f;       // 抗力（空気抵抗）

    [Header("Control")]
    public Vector3 torquePower;       // 機体の旋回力
    public float unlimitedtorque = 10f; // 速度制限解除時の旋回力倍率
    public bool limiterOn;

    protected Rigidbody rb;
    public float throttle = 1f;

    public Vector3 Torque;
    public Vector3 Velocity;

    public AugumentStatus status; 
    Vector3 externalControlAssist;
    
    protected virtual void Awake()
    {
        status = GetComponent<AugumentStatus>();
        rb = GetComponent<Rigidbody>();
    }


    void InitFromStatus()
    {
        status.altGetVar("加速度", out thrustPower);
        status.altGetVar("最高速度", out maxSpeed);
        status.altGetVar("機動性(ピッチ)", out torquePower.x);
        status.altGetVar("機動性(ロール)", out torquePower.y);
        status.altGetVar("機動性(ヨー)", out torquePower.z);

        maxSpeed = Mathf.Max(1f, maxSpeed);
    }

    protected virtual void Start()
    {

        if (status.IsInitialized)
        {
            InitFromStatus();
        }
        else
        {
            status.OnInitialized += InitFromStatus;
        }

        rb.useGravity = true;
        rb.linearDamping = 0f;
        rb.angularDamping = 1f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.mass = 1;

        rb.linearVelocity = transform.forward * thrustPower;
        if (!IsValidVector(rb.linearVelocity))
        {
            Debug.LogWarning("Velocity NaN detected, reset.");
            rb.linearVelocity = transform.forward * 10f;
        }
    }

    protected virtual void FixedUpdate()
    {
        if (rb == null) return;

        Vector3 controlInput = (GetControlInput() + externalControlAssist) * 0.3f;
        externalControlAssist = Vector3.zero;
        float targetThrottle = GetThrottleInput();
        limiterOn = GetLimiter();

        Velocity = rb.linearVelocity;

        throttle = Mathf.Lerp(throttle, targetThrottle, Time.fixedDeltaTime);
        throttle = Mathf.Clamp(throttle, 0f, 5f);

        status.currentHeat = throttle;

        float speedRatio =
            maxSpeed > 0f
            ? Mathf.Clamp01(rb.linearVelocity.magnitude / maxSpeed)
            : 0f;

        // 推力
        float thrust = thrustPower * Mathf.Max(0f, throttle) * (1f - speedRatio);

        //慣性力を前方への推力へ変換
        Vector3 velocityvec = rb.linearVelocity;
        Vector3 thrustvec = transform.forward * thrust;
        Vector3 forwardvec = transform.forward * rb.linearVelocity.magnitude * speedRatio;

        rb.linearVelocity = Vector3.Lerp(velocityvec, thrustvec+forwardvec, 0.05f + 0.95f * speedRatio * Time.fixedDeltaTime);

        // 空気抵抗
        rb.AddForce(-rb.linearVelocity * dragPower * speedRatio);

        // 揚力
        float lift = liftPower * speedRatio * Mathf.Clamp01(Vector3.Dot(transform.up, Vector3.up));
        rb.AddForce(Vector3.up * lift);

        // 操舵トルク
        float stallRatio = Mathf.Clamp01(rb.linearVelocity.magnitude / stallSpeed);
        stallRatio = Mathf.Pow(stallRatio, 2f); // 失速を滑らかに

        if(stallRatio<1f)
        {
            limiterOn = true;
        }

        float torqueScale = limiterOn
            ? stallRatio
            : unlimitedtorque;

        Vector3 pitchTorque = transform.right * -controlInput.x * torquePower.x * torqueScale;
        Vector3 rollTorque = transform.forward * -controlInput.y * torquePower.y * torqueScale;
        Vector3 yawTorque = transform.up * controlInput.z * torquePower.z * torqueScale;
        Torque = pitchTorque + rollTorque + yawTorque;

        rb.AddTorque(pitchTorque);
        rb.AddTorque(rollTorque);
        rb.AddTorque(yawTorque);

        // 失速時の機首下げ
        if (stallRatio < 0.9f)
        {
            limiterOn = true;

            Vector3 forward = transform.forward;
            float angle = Vector3.Angle(forward, Vector3.down);
            if (angle > 0.1f)
            {
                Vector3 axis = Vector3.Cross(forward, Vector3.down).normalized;
                Vector3 stallTorque = axis.normalized * (0.9f - stallRatio) * torquePower.x * Time.fixedDeltaTime*10f;
                Torque += stallTorque;
                rb.AddTorque(stallTorque);
            }
        }

        // 速度制限
        rb.linearVelocity = Vector3.ClampMagnitude(rb.linearVelocity, maxSpeed);
        if (!IsValidVector(rb.linearVelocity))
        {
            Debug.LogWarning("Velocity NaN detected, reset.");
            rb.linearVelocity = transform.forward * 10f;
        }
    }
    bool IsValidVector(Vector3 v)
    {
        return
            !float.IsNaN(v.x) &&
            !float.IsNaN(v.y) &&
            !float.IsNaN(v.z) &&
            !float.IsInfinity(v.x) &&
            !float.IsInfinity(v.y) &&
            !float.IsInfinity(v.z);
    }

    // 継承先で上書き
    protected virtual Vector3 GetControlInput() => Vector3.zero; // pitch, roll, yaw
    protected virtual float GetThrottleInput() => 1f;

    protected virtual bool GetLimiter() => true;

    public void AddControlAssist(Vector3 assistInput)
    {
        externalControlAssist += Vector3.ClampMagnitude(assistInput, 1f);
    }
}
