using UnityEngine;

[DisallowMultipleComponent]
public sealed class FCS : MonoBehaviour
{
    const float MinimumProjectileSpeed = 0.01f;

    [Header("Lead Shooting")]
    [SerializeField, Range(0f, 180f)] float leadShootFov = 15f;

    [Header("Runtime")]
    [SerializeField] Vector3 lasttgtPosition;

    AircraftFlightAI aircraftFlightAI;
    PilotStatus pilotStatus;
    Rigidbody ownerBody;
    AircraftFlightAI sampledTarget;
    Vector3 estimatedTargetVelocity;
    bool hasTargetPositionSample;

    public AircraftFlightAI CurrentTarget => aircraftFlightAI != null
        ? aircraftFlightAI.CurrentTarget
        : null;
    public float LeadShootFov => leadShootFov;
    public float SpreadArea => pilotStatus != null
        ? Mathf.Max(0f, pilotStatus.spreadArea)
        : 0f;
    public Vector3 LastTargetPosition => lasttgtPosition;
    public Vector3 EstimatedTargetVelocity => estimatedTargetVelocity;
    public bool IsTargetInsideLeadShootFov => IsInsideLeadShootFov(CurrentTarget);

    void Awake()
    {
        Initialize(GetComponent<AircraftFlightAI>());
    }

    public void Initialize(AircraftFlightAI aircraft)
    {
        aircraftFlightAI = aircraft;
        pilotStatus = GetComponent<PilotStatus>();
        ownerBody = GetComponent<Rigidbody>();
        ResetTargetSample();
    }

    void FixedUpdate()
    {
        AircraftFlightAI target = CurrentTarget;
        if (target == null)
        {
            ResetTargetSample();
            return;
        }

        Vector3 targetPosition = target.transform.position;
        if (target != sampledTarget)
        {
            sampledTarget = target;
            lasttgtPosition = targetPosition;
            estimatedTargetVelocity = Vector3.zero;
            hasTargetPositionSample = true;
            return;
        }

        float deltaTime = Time.fixedDeltaTime;
        if (hasTargetPositionSample && deltaTime > 0f)
            estimatedTargetVelocity = (targetPosition - lasttgtPosition) / deltaTime;

        lasttgtPosition = targetPosition;
        hasTargetPositionSample = true;
    }

    public bool TryGetLeadAimPoint(float projectileSpeed, out Vector3 aimPoint)
    {
        AircraftFlightAI target = CurrentTarget;
        aimPoint = target != null ? target.transform.position : transform.position;
        if (target == null || projectileSpeed <= MinimumProjectileSpeed ||
            !IsInsideLeadShootFov(target))
            return false;

        Vector3 relativePosition = target.transform.position - transform.position;
        Vector3 ownerVelocity = ownerBody != null ? ownerBody.linearVelocity : Vector3.zero;
        Vector3 relativeVelocity = estimatedTargetVelocity - ownerVelocity;
        float interceptTime = CalculateInterceptTime(
            relativePosition,
            relativeVelocity,
            projectileSpeed);
        aimPoint = target.transform.position + estimatedTargetVelocity * interceptTime;
        return IsValidVector(aimPoint);
    }

    public bool TryGetShotDirection(float projectileSpeed, out Vector3 shotDirection)
    {
        return TryGetShotDirection(projectileSpeed, 0f, out shotDirection);
    }

    public bool TryGetShotDirection(
        float projectileSpeed,
        float weaponSpreadArea,
        out Vector3 shotDirection)
    {
        shotDirection = transform.forward;
        if (!TryGetLeadAimPoint(projectileSpeed, out Vector3 aimPoint))
            return false;

        Vector3 leadDirection = aimPoint - transform.position;
        if (!IsValidVector(leadDirection) || leadDirection.sqrMagnitude <= 0.0001f)
            return false;

        Quaternion aimRotation = Quaternion.LookRotation(leadDirection.normalized, transform.up);
        float totalSpreadArea = SpreadArea + Mathf.Max(0f, weaponSpreadArea);
        Vector2 spread = Random.insideUnitCircle * totalSpreadArea;
        shotDirection = aimRotation * Quaternion.Euler(-spread.y, spread.x, 0f) * Vector3.forward;
        return IsValidVector(shotDirection);
    }

    bool IsInsideLeadShootFov(AircraftFlightAI target)
    {
        if (target == null) return false;
        Vector3 targetDirection = target.transform.position - transform.position;
        if (!IsValidVector(targetDirection) || targetDirection.sqrMagnitude <= 0.0001f)
            return false;
        return Vector3.Angle(transform.forward, targetDirection) <= leadShootFov;
    }

    void ResetTargetSample()
    {
        sampledTarget = null;
        lasttgtPosition = Vector3.zero;
        estimatedTargetVelocity = Vector3.zero;
        hasTargetPositionSample = false;
    }

    static float CalculateInterceptTime(
        Vector3 relativePosition,
        Vector3 relativeVelocity,
        float projectileSpeed)
    {
        float a = relativeVelocity.sqrMagnitude - projectileSpeed * projectileSpeed;
        float b = 2f * Vector3.Dot(relativePosition, relativeVelocity);
        float c = relativePosition.sqrMagnitude;

        if (Mathf.Abs(a) <= 0.0001f)
        {
            if (Mathf.Abs(b) > 0.0001f)
            {
                float linearTime = -c / b;
                if (linearTime > 0f) return linearTime;
            }
            return Mathf.Sqrt(c) / projectileSpeed;
        }

        float discriminant = b * b - 4f * a * c;
        if (discriminant < 0f)
            return Mathf.Sqrt(c) / projectileSpeed;

        float squareRoot = Mathf.Sqrt(discriminant);
        float firstTime = (-b - squareRoot) / (2f * a);
        float secondTime = (-b + squareRoot) / (2f * a);
        if (firstTime > 0f && secondTime > 0f)
            return Mathf.Min(firstTime, secondTime);
        if (firstTime > 0f) return firstTime;
        if (secondTime > 0f) return secondTime;
        return Mathf.Sqrt(c) / projectileSpeed;
    }

    static bool IsValidVector(Vector3 value)
    {
        return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }

    void OnValidate()
    {
        leadShootFov = Mathf.Clamp(leadShootFov, 0f, 180f);
    }

    void OnDrawGizmosSelected()
    {
        AircraftFlightAI target = CurrentTarget;
        if (target == null) return;

        Gizmos.color = IsInsideLeadShootFov(target) ? Color.red : Color.gray;
        Gizmos.DrawLine(transform.position, target.transform.position);
    }
}
