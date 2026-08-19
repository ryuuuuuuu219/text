using System.Collections.Generic;
using UnityEngine;

public enum AircraftAffiliation { A, E }
public enum AircraftTargetSelectionCriterion
{
    [InspectorName("対正面（現行）")] Front,
    [InspectorName("対近距離")] Nearest,
    [InspectorName("対遠距離")] Farthest,
    [InspectorName("カウンター")] Counter,
    [InspectorName("帰還中")] Returning
}

public sealed class AircraftFlightAI : AircraftController
{
    static readonly List<AircraftFlightAI> Aircraft = new();
    const float TargetRefreshIntervalStep = 0.25f;
    const float MinimumTargetRefreshInterval = 0.25f;
    const float MaximumTargetRefreshInterval = 30f;

    [Header("Identity")]
    public AircraftAffiliation affiliation;
    public int aircraftId;
    public int trackingTargetId;

    [Header("Targeting")]
    public AircraftTargetSelectionCriterion targetSelectionCriterion = AircraftTargetSelectionCriterion.Front;
    [Min(1f)] public float detectionRange = 2000f;
    [Range(MinimumTargetRefreshInterval, MaximumTargetRefreshInterval)]
    public float targetRefreshInterval = MinimumTargetRefreshInterval;

    public AircraftFlightAI CurrentTarget { get; private set; }
    public static IReadOnlyList<AircraftFlightAI> ActiveAircraft => Aircraft;
    float nextTargetRefresh;
    AircraftTargetSelectionCriterion appliedTargetSelectionCriterion;
    PilotStatus pilotStatus;
    AircraftManeuverController maneuverController;
    FCS fireControlSystem;
    WeaponStatus weaponStatus;

    protected override void Awake()
    {
        base.Awake();
        ClampTargetRefreshInterval();
        pilotStatus = GetComponent<PilotStatus>();
        maneuverController = GetComponent<AircraftManeuverController>();
        if (maneuverController == null)
            maneuverController = gameObject.AddComponent<AircraftManeuverController>();
        maneuverController.Initialize(this);
        fireControlSystem = GetComponent<FCS>();
        if (fireControlSystem == null)
            fireControlSystem = gameObject.AddComponent<FCS>();
        fireControlSystem.Initialize(this);
        weaponStatus = GetComponent<WeaponStatus>();
        if (weaponStatus == null)
            weaponStatus = gameObject.AddComponent<WeaponStatus>();
        weaponStatus.Initialize(this);
        appliedTargetSelectionCriterion = targetSelectionCriterion;
        Aircraft.Add(this);
    }

    void OnDestroy()
    {
        Aircraft.Remove(this);
    }

    void Update()
    {
        bool criterionChanged = targetSelectionCriterion != appliedTargetSelectionCriterion;
        if (!criterionChanged && Time.time < nextTargetRefresh) return;

        appliedTargetSelectionCriterion = targetSelectionCriterion;
        nextTargetRefresh = Time.time + targetRefreshInterval;
        RefreshTarget(criterionChanged);
    }

    public void SetTargetSelectionCriterion(AircraftTargetSelectionCriterion criterion)
    {
        if (targetSelectionCriterion == criterion) return;
        targetSelectionCriterion = criterion;
        nextTargetRefresh = 0f;
    }

    public void IncreaseTargetRefreshInterval()
    {
        SetTargetRefreshInterval(targetRefreshInterval + TargetRefreshIntervalStep);
    }

    public void DecreaseTargetRefreshInterval()
    {
        SetTargetRefreshInterval(targetRefreshInterval - TargetRefreshIntervalStep);
    }

    void SetTargetRefreshInterval(float interval)
    {
        targetRefreshInterval = Mathf.Clamp(
            interval,
            MinimumTargetRefreshInterval,
            MaximumTargetRefreshInterval);
        nextTargetRefresh = Time.time + targetRefreshInterval;
    }

    void ClampTargetRefreshInterval()
    {
        targetRefreshInterval = Mathf.Clamp(
            targetRefreshInterval,
            MinimumTargetRefreshInterval,
            MaximumTargetRefreshInterval);
    }

    void OnValidate()
    {
        ClampTargetRefreshInterval();
    }

    void RefreshTarget(bool forceReselection)
    {
        if (targetSelectionCriterion == AircraftTargetSelectionCriterion.Returning)
        {
            SetCurrentTarget(null);
            return;
        }

        // Resolve the configured initial/current ID first, while it remains visible.
        if (!forceReselection && CurrentTarget == null && trackingTargetId != 0)
            SetCurrentTarget(FindById(trackingTargetId));

        if (!forceReselection && IsValidVisibleEnemy(CurrentTarget))
        {
            maneuverController.SetTrackingTarget(CurrentTarget);
            return;
        }

        AircraftFlightAI best = null;
        float bestScore = float.PositiveInfinity;
        bool bestIsCountering = false;
        float effectiveRange = pilotStatus != null ? Mathf.Min(detectionRange, pilotStatus.detectionRadius) : detectionRange;
        float rangeSquared = effectiveRange * effectiveRange;
        for (int i = 0; i < Aircraft.Count; i++)
        {
            AircraftFlightAI candidate = Aircraft[i];
            if (candidate == null || candidate == this || candidate.affiliation == affiliation) continue;
            Vector3 offset = candidate.transform.position - transform.position;
            float distanceSquared = offset.sqrMagnitude;
            if (distanceSquared > rangeSquared) continue;
            float angle = Vector3.Angle(transform.forward, offset);

            bool isCountering = candidate.CurrentTarget == this ||
                                aircraftId != 0 && candidate.trackingTargetId == aircraftId;
            float score = targetSelectionCriterion switch
            {
                AircraftTargetSelectionCriterion.Nearest => distanceSquared,
                AircraftTargetSelectionCriterion.Farthest => -distanceSquared,
                _ => angle
            };

            if (targetSelectionCriterion == AircraftTargetSelectionCriterion.Counter)
            {
                if (best != null && bestIsCountering && !isCountering) continue;
                if (best != null && bestIsCountering == isCountering && score >= bestScore) continue;
            }
            else if (score >= bestScore)
            {
                continue;
            }

            best = candidate;
            bestScore = score;
            bestIsCountering = isCountering;
        }

        SetCurrentTarget(best);
    }

    void SetCurrentTarget(AircraftFlightAI target)
    {
        CurrentTarget = target;
        trackingTargetId = target != null ? target.aircraftId : 0;
        maneuverController?.SetTrackingTarget(target);
    }

    bool IsValidVisibleEnemy(AircraftFlightAI target)
    {
        if (target == null || target.affiliation == affiliation) return false;
        Vector3 offset = target.transform.position - transform.position;
        float effectiveRange = pilotStatus != null ? Mathf.Min(detectionRange, pilotStatus.detectionRadius) : detectionRange;
        return offset.sqrMagnitude <= effectiveRange * effectiveRange;
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
