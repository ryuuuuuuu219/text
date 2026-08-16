using System.Collections.Generic;
using UnityEngine;

public enum AircraftAffiliation { A, E }

public sealed class AircraftFlightAI : AircraftController
{
    static readonly List<AircraftFlightAI> Aircraft = new();

    [Header("Identity")]
    public AircraftAffiliation affiliation;
    public int aircraftId;
    public int trackingTargetId;

    [Header("Targeting")]
    [Range(1f, 180f)] public float fieldOfView = 100f;
    [Min(1f)] public float detectionRange = 2000f;
    [Min(0.05f)] public float targetRefreshInterval = 0.25f;

    public AircraftFlightAI CurrentTarget { get; private set; }
    float nextTargetRefresh;

    protected override void Awake()
    {
        base.Awake();
        Aircraft.Add(this);
    }

    void OnDestroy()
    {
        Aircraft.Remove(this);
    }

    void Update()
    {
        if (Time.time < nextTargetRefresh) return;
        nextTargetRefresh = Time.time + targetRefreshInterval;
        RefreshTarget();
    }

    void RefreshTarget()
    {
        // Resolve the configured initial/current ID first, while it remains visible.
        if (CurrentTarget == null && trackingTargetId != 0)
            CurrentTarget = FindById(trackingTargetId);

        if (IsValidVisibleEnemy(CurrentTarget)) return;

        AircraftFlightAI best = null;
        float bestAngle = float.PositiveInfinity;
        float rangeSquared = detectionRange * detectionRange;
        for (int i = 0; i < Aircraft.Count; i++)
        {
            AircraftFlightAI candidate = Aircraft[i];
            if (candidate == null || candidate == this || candidate.affiliation == affiliation) continue;
            Vector3 offset = candidate.transform.position - transform.position;
            if (offset.sqrMagnitude > rangeSquared) continue;
            float angle = Vector3.Angle(transform.forward, offset);
            if (angle > fieldOfView * 0.5f || angle >= bestAngle) continue;
            best = candidate;
            bestAngle = angle;
        }

        CurrentTarget = best;
        trackingTargetId = best != null ? best.aircraftId : 0;
    }

    bool IsValidVisibleEnemy(AircraftFlightAI target)
    {
        if (target == null || target.affiliation == affiliation) return false;
        Vector3 offset = target.transform.position - transform.position;
        return offset.sqrMagnitude <= detectionRange * detectionRange &&
               Vector3.Angle(transform.forward, offset) <= fieldOfView * 0.5f;
    }

    static AircraftFlightAI FindById(int id)
    {
        for (int i = 0; i < Aircraft.Count; i++)
            if (Aircraft[i] != null && Aircraft[i].aircraftId == id)
                return Aircraft[i];
        return null;
    }

    protected override Vector3 GetControlInput()
    {
        if (CurrentTarget == null) return Vector3.zero;
        Vector3 localDirection = transform.InverseTransformDirection(
            (CurrentTarget.transform.position - transform.position).normalized);
        float yaw = Mathf.Clamp(localDirection.x * 2f, -1f, 1f);
        float pitch = Mathf.Clamp(localDirection.y * 2f, -1f, 1f);
        float roll = Mathf.Clamp(localDirection.x * 1.5f, -1f, 1f);
        return new Vector3(pitch, roll, yaw);
    }

    protected override float GetThrottleInput()
    {
        if (rb == null) return 1f;
        return rb.linearVelocity.magnitude < maxSpeed - 0.5f ? 1f : 0f;
    }
}
