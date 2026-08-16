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

    [Header("Maneuver")]
    [Tooltip("追尾目標のローカル座標で表す仮想追跡点。Xは目標の右方向、Yは上方向、Zは前方向です。")]
    public Vector3 trackingOffset = new(60f, 0f, 0f);

    public AircraftFlightAI CurrentTarget { get; private set; }
    float nextTargetRefresh;
    PilotStatus pilotStatus;
    AircraftManeuverController maneuverController;

    protected override void Awake()
    {
        base.Awake();
        pilotStatus = GetComponent<PilotStatus>();
        maneuverController = GetComponent<AircraftManeuverController>();
        if (maneuverController == null)
            maneuverController = gameObject.AddComponent<AircraftManeuverController>();
        maneuverController.Initialize(this);
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
            SetCurrentTarget(FindById(trackingTargetId));

        if (IsValidVisibleEnemy(CurrentTarget))
        {
            maneuverController.SetTrackingTarget(CurrentTarget, trackingOffset);
            return;
        }

        AircraftFlightAI best = null;
        float bestAngle = float.PositiveInfinity;
        float effectiveRange = pilotStatus != null ? Mathf.Min(detectionRange, pilotStatus.detectionRadius) : detectionRange;
        float rangeSquared = effectiveRange * effectiveRange;
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

        SetCurrentTarget(best);
    }

    void SetCurrentTarget(AircraftFlightAI target)
    {
        CurrentTarget = target;
        trackingTargetId = target != null ? target.aircraftId : 0;
        maneuverController?.SetTrackingTarget(target, trackingOffset);
    }

    bool IsValidVisibleEnemy(AircraftFlightAI target)
    {
        if (target == null || target.affiliation == affiliation) return false;
        Vector3 offset = target.transform.position - transform.position;
        float effectiveRange = pilotStatus != null ? Mathf.Min(detectionRange, pilotStatus.detectionRadius) : detectionRange;
        return offset.sqrMagnitude <= effectiveRange * effectiveRange &&
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
        return maneuverController != null
            ? maneuverController.GetControlInput()
            : Vector3.zero;
    }

    protected override float GetThrottleInput()
    {
        return maneuverController != null
            ? maneuverController.GetThrottleInput()
            : 1f;
    }
}
