using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class EnemyAircraftAIGen4 : AircraftController
{
    public enum CombatMode { Offensive, Defensive }
    public enum OffensiveState { TwoCircle, OneCircle, Twisted }
    public enum ManeuverState
    {
        LeadPursuit, Extend, SplitS, ShiftTurnPlane,
        EvadeMissile, Brake, RecoverAltitude
    }

    protected virtual bool DeterministicAnalysisMode => false;
    protected virtual float CommandDirectionSmoothing => 0f;

    [Header("Hierarchical State Machine")]
    [SerializeField] CombatMode combatMode = CombatMode.Offensive;
    [SerializeField] OffensiveState offensiveState = OffensiveState.Twisted;
    [SerializeField] ManeuverState maneuverState = ManeuverState.LeadPursuit;
    [SerializeField] float defensiveDuration = 12f;
    [SerializeField] float defensiveCooldown = 45f;
    [SerializeField] float turnPredictionTime = 2f;
    [SerializeField] float turnPlaneThreshold = 60f;
    [SerializeField] float twoCircleCooldown = 15f;
    [SerializeField] float twoCirclePatternThreshold = 15f;
    [SerializeField] float twoCirclePatternRate = 0.1f;
    [SerializeField] float minimumExtendDuration = 3f;
    [SerializeField] float desiredExtendDistance = 1200f;
    [SerializeField] float splitSRelativePitchTolerance = 35f;
    [SerializeField, Range(-1f, 1f)] float splitSRelativeInversionDot = 0.85f;
    [SerializeField] float splitSPullDuration = 1.5f;
    [SerializeField] float splitSHardTimeout = 5f;
    [SerializeField] float oneCircleDuration = 18f;
    [SerializeField] float insideTurnLateralThreshold = 300f;
    [SerializeField] float shiftTurnPlaneDuration = 1.5f;
    [SerializeField] float shiftTurnPlaneRollInput = 0.8f;
    [SerializeField] float twistedMinimumPlaneAngle = 15f;
    [SerializeField] float twistedMaximumPlaneAngle = 60f;

    [Header("HSM Debug")]
    [SerializeField] float defensiveRemainTime;
    [SerializeField] float defensiveCooldownRemainTime;
    [SerializeField] float twoCircleElapsedTime;
    [SerializeField] float twoCircleCooldownRemainTime;
    [SerializeField] float oneCircleElapsedTime;
    [SerializeField] float maneuverElapsedTime;
    [SerializeField] float pitchAxisToEnemyAngle;
    [SerializeField] float pitchAxisToPredictedEnemyAngle;
    [SerializeField] float pitchAxisToEnemyVelocityAngle;
    [SerializeField] float splitSRelativePlaneError;
    [SerializeField] float splitSRelativeInversion;
    [SerializeField] float splitSCompletionTimer;
    [SerializeField] bool splitSRollAligned;
    [SerializeField] bool splitSPullStarted;
    [SerializeField] float toTargetAngle;
    [SerializeField] float targetNoseToMeAngle;
    [SerializeField] bool alignedForTwoCircle;
    [SerializeField] bool defensiveCondition;
    [SerializeField] bool targetInsideTurn;

    float splitSRollSign = 1f;
    float shiftTurnPlaneRollSign = 1f;
    float oneCirclePitchSign = 1f;
    float twistedTargetPlaneAngle = 15f;

    public enum CombatState
    {
        LeadPursuit,
        Offset,
        Brake,
        Extend,
        EvadeMissile,
        RecoverAltitude
    }

    [Header("Target")]
    public Transform target;
    public float detectRange = 6000f;
    public float lockAssistRange = 2500f;
    public float retargetInterval = 0.35f;
    public bool preferFcsTarget = true;

    [Header("Combat Ranges")]
    public float leadPursuitRange = 2200f;
    public float offsetRange = 900f;
    public float extendRange = 550f;

    [Header("Altitude")]
    public float minAltitude = 900f;
    public float maxAltitude = 8500f;
    public float desiredAltitude = 2500f;

    [Header("Missile Evasion")]
    public float missileDetectRange = 1800f;
    public float missileCriticalTime = 3.5f;
    public float evadeForwardWeight = 0.25f;
    public float evadeTargetWeight = 0.2f;
    public float missileApproachAngle = 45f;
    public float missileBarrelRollChance = 0.3f;
    public float barrelRollInput = 1f;
    public float attackApproachAngle = 45f;
    public float lagPursuitSeconds = 1.2f;

    [Header("Steering")]
    public float offsetRadius = 450f;
    public float offsetRefreshInterval = 1.2f;
    public float targetDirectionRefreshInterval = 0.12f;
    public float downwardPitchLimit = 0.3f;
    public float yawAssist = 1f;

    [Header("Throttle")]
    public float cruiseThrottle = 1f;
    public float attackThrottle = 3.2f;
    public float fullThrottle = 5f;
    public float brakeThrottle = 0.05f;

    [Header("Decision")]
    [SerializeField] float brakeDistance = 250f;
    [SerializeField] float missileThreatNearDistance = 100f;
    [SerializeField] float missileThreatFarDistance = 2000f;

    [Header("Debug")]
    [SerializeField] CombatState currentState = CombatState.LeadPursuit;
    [SerializeField] bool evadeMissileUseBarrelRoll;
    [SerializeField] float missileThreat;
    [SerializeField] float targetDistance;
    [FormerlySerializedAs("targetDirection")]
    [SerializeField] Vector3 commandedFlightDirection = Vector3.forward;
    [SerializeField] Vector3 missileEvadeDirection;

    FCS_e fcs;
    Rigidbody targetRb;
    float nextRetargetTime;
    float nextDirectionRefreshTime;
    float nextOffsetRefreshTime;
    float bookedStateTimer;
    float stateTimer;
    float interceptTimeCache;
    Vector3 offsetVector;
    readonly List<EnemyMissileThreatSensor.ThreatInfo> missileThreats = new();
    Vector3[] sensedMissileDirections = new Vector3[4];
    float barrelRollSign = 1f;
    float nextRandomManeuverCheckTime;
    float nextRandomManeuverTime;
    float nextTacticSwitchCheckTime;
    float nextTacticSwitchTime;
    bool invertOffensiveTactic;

    protected override void Awake()
    {
        base.Awake();
        fcs = GetComponent<FCS_e>();
        commandedFlightDirection = transform.forward;
        offsetVector = transform.right * offsetRadius;
    }

#if false // Legacy weighted-state implementation retained only for serialized migration reference.
    void TuningOverwrite()
    {
        StateTuning[] overwrittenTunings = new StateTuning[stateTunings.Length];
        for (int i = 0; i < stateTunings.Length; i++)
        {
            StateTuning tuning = stateTunings[i];
            if (!DeterministicAnalysisMode)
            {
                tuning.enterDelay += Random.Range(0f, enterDelayRandomAddMax);
                tuning.minimumDuration += Random.Range(0f, minimumDurationRandomAddMax);
            }
            tuning.needToChoiceTime = 6f;
            tuning.maxTime = 20f;
            overwrittenTunings[i] = tuning;
        }

        stateTunings = overwrittenTunings;
    }

    void InitializeStateRuntime()
    {
        StateRuntime[] initializedRuntime = new StateRuntime[stateTunings.Length];
        for (int i = 0; i < stateTunings.Length; i++)
        {
            int existingIndex = FindRuntimeIndex(stateTunings[i].state);
            StateRuntime runtime = existingIndex >= 0
                ? stateRuntime[existingIndex]
                : new StateRuntime();

            runtime.state = stateTunings[i].state;
            runtime.conditionMet = false;
            runtime.remainTime = 0f;
            runtime.weight = 0f;
            initializedRuntime[i] = runtime;
        }

        stateRuntime = initializedRuntime;
    }

#endif
    void Update()
    {
        RefreshTarget();

        if (target == null)
        {
            ResetDecision();
            missileThreat = 0f;
            targetDistance = 0f;
            commandedFlightDirection = transform.forward;
            return;
        }

        targetDistance = Vector3.Distance(transform.position, target.position);
        missileThreat = EvaluateMissileThreat(out Vector3 evaluatedMissileEvadeDirection);
        missileEvadeDirection = evaluatedMissileEvadeDirection;
        UpdateCombatGeometry();
        UpdateHierarchicalState();

        if (Time.time >= nextDirectionRefreshTime)
        {
            nextDirectionRefreshTime = Time.time + targetDirectionRefreshInterval;
            Vector3 desiredDirection = BuildTargetDirection();
            float smoothing = CommandDirectionSmoothing;
            if (smoothing <= 0f)
            {
                commandedFlightDirection = desiredDirection;
            }
            else
            {
                float blend = 1f - Mathf.Exp(-smoothing * Mathf.Max(Time.deltaTime, 0f));
                commandedFlightDirection = SafeNormalize(
                    Vector3.Slerp(commandedFlightDirection, desiredDirection, blend),
                    desiredDirection);
            }
        }
    }

    protected override Vector3 GetControlInput()
    {
        if (maneuverState == ManeuverState.SplitS)
            return GetSplitSControlInput();
        if (maneuverState == ManeuverState.ShiftTurnPlane)
            return GetShiftTurnPlaneControlInput();
        Vector3 input = SteerToward(commandedFlightDirection);
        if (combatMode == CombatMode.Offensive && offensiveState == OffensiveState.Twisted)
            input.y = Mathf.Clamp((pitchAxisToEnemyAngle - twistedTargetPlaneAngle) / 45f, -1f, 1f);
        return input;
    }

    void UpdateCombatGeometry()
    {
        Vector3 toEnemy = SafeNormalize(target.position - transform.position, transform.forward);
        Vector3 targetToMe = SafeNormalize(transform.position - target.position, -target.forward);
        toTargetAngle = Vector3.Angle(GetForwardReference(), toEnemy);
        targetNoseToMeAngle = Vector3.Angle(target.forward, targetToMe);
        defensiveCondition = toTargetAngle > 90f && targetNoseToMeAngle < 90f;
        if (targetRb == null || targetRb.transform != target) targetRb = target.GetComponent<Rigidbody>();
        Vector3 predicted = target.position + (targetRb != null ? targetRb.linearVelocity : Vector3.zero) * turnPredictionTime;
        pitchAxisToEnemyAngle = GetPitchAxisErrorAngle(toEnemy);
        pitchAxisToPredictedEnemyAngle = GetPitchAxisErrorAngle(SafeNormalize(predicted - transform.position, toEnemy));
        Vector3 enemyVelocityDirection = SafeNormalize(
            targetRb != null ? targetRb.linearVelocity : Vector3.zero,
            target.forward);
        pitchAxisToEnemyVelocityAngle = GetPitchAxisErrorAngle(enemyVelocityDirection);
        alignedForTwoCircle = Mathf.Abs(pitchAxisToEnemyAngle) <= turnPlaneThreshold
            && Mathf.Abs(pitchAxisToPredictedEnemyAngle) <= turnPlaneThreshold;
        Vector3 local = transform.InverseTransformPoint(target.position);
        targetInsideTurn = Mathf.Abs(local.x) < insideTurnLateralThreshold && local.z > 0f;
    }

    float GetPitchAxisErrorAngle(Vector3 direction)
    {
        Vector3 local = transform.InverseTransformDirection(SafeNormalize(direction, transform.forward));
        return Mathf.Asin(Mathf.Clamp(local.x, -1f, 1f)) * Mathf.Rad2Deg;
    }

    void UpdateHierarchicalState()
    {
        float dt = Time.deltaTime;
        defensiveCooldownRemainTime = Mathf.Max(0f, defensiveCooldownRemainTime - dt);
        twoCircleCooldownRemainTime = Mathf.Max(0f, twoCircleCooldownRemainTime - dt);
        maneuverElapsedTime += dt;
        if (missileThreat > 0f) { EnterManeuver(ManeuverState.EvadeMissile); return; }
        if (GetAltitudeDangerScore(transform.position.y) > 0f) { EnterManeuver(ManeuverState.RecoverAltitude); return; }
        if (maneuverState == ManeuverState.EvadeMissile
            || maneuverState == ManeuverState.RecoverAltitude)
            EnterManeuver(ManeuverState.LeadPursuit);
        if (combatMode == CombatMode.Defensive)
        {
            defensiveRemainTime -= dt;
            if (defensiveRemainTime <= 0f)
            {
                combatMode = CombatMode.Offensive;
                defensiveCooldownRemainTime = defensiveCooldown;
                EnterOffensiveState(OffensiveState.Twisted);
            }
            else EnterManeuver(targetDistance < brakeDistance ? ManeuverState.Brake : ManeuverState.LeadPursuit);
            return;
        }
        if (defensiveCondition && defensiveCooldownRemainTime <= 0f)
        {
            combatMode = CombatMode.Defensive;
            defensiveRemainTime = defensiveDuration;
            EnterManeuver(ManeuverState.Brake);
            return;
        }
        UpdateOffensiveStateHsm();
    }

    void UpdateOffensiveStateHsm()
    {
        if (maneuverState == ManeuverState.Extend)
        {
            if (maneuverElapsedTime >= minimumExtendDuration && targetDistance >= desiredExtendDistance)
                EnterOffensiveState(OffensiveState.Twisted);
            return;
        }
        if (maneuverState == ManeuverState.SplitS)
        {
            UpdateSplitS();
            return;
        }
        if (maneuverState == ManeuverState.ShiftTurnPlane)
        {
            if (maneuverElapsedTime >= shiftTurnPlaneDuration) EnterOffensiveState(OffensiveState.Twisted);
            return;
        }
        if (offensiveState == OffensiveState.Twisted)
        {
            if (alignedForTwoCircle)
                EnterOffensiveState(twoCircleCooldownRemainTime <= 0f ? OffensiveState.TwoCircle : OffensiveState.OneCircle);
        }
        else if (offensiveState == OffensiveState.OneCircle)
        {
            oneCircleElapsedTime += Time.deltaTime;
            if (targetInsideTurn) EnterManeuver(ManeuverState.ShiftTurnPlane);
            else if (!alignedForTwoCircle || oneCircleElapsedTime >= oneCircleDuration)
                EnterOffensiveState(OffensiveState.Twisted);
        }
        else
        {
            twoCircleElapsedTime += Time.deltaTime;
            float excess = Mathf.Max(0f, twoCircleElapsedTime - twoCirclePatternThreshold);
            bool change = DeterministicAnalysisMode
                ? twoCircleElapsedTime >= 20f
                : Random.value < 1f - Mathf.Exp(-excess * twoCirclePatternRate * Time.deltaTime);
            if (change) ChangeTwoCirclePattern();
        }
    }

    void EnterOffensiveState(OffensiveState next)
    {
        if (offensiveState == OffensiveState.TwoCircle && next != OffensiveState.TwoCircle)
            twoCircleCooldownRemainTime = twoCircleCooldown;
        offensiveState = next;
        maneuverState = ManeuverState.LeadPursuit;
        maneuverElapsedTime = 0f;
        if (next == OffensiveState.TwoCircle) twoCircleElapsedTime = 0f;
        if (next == OffensiveState.OneCircle) { oneCircleElapsedTime = 0f; oneCirclePitchSign = 1f; }
        if (next == OffensiveState.Twisted)
        {
            twistedTargetPlaneAngle = Mathf.Clamp(pitchAxisToEnemyAngle, -twistedMaximumPlaneAngle, twistedMaximumPlaneAngle);
            if (Mathf.Abs(twistedTargetPlaneAngle) < twistedMinimumPlaneAngle)
                twistedTargetPlaneAngle = pitchAxisToEnemyAngle >= 0f ? twistedMinimumPlaneAngle : -twistedMinimumPlaneAngle;
        }
    }

    void EnterManeuver(ManeuverState next)
    {
        if (maneuverState == next) return;
        maneuverState = next;
        maneuverElapsedTime = 0f;
        Vector3 local = target != null ? transform.InverseTransformPoint(target.position) : Vector3.forward;
        if (next == ManeuverState.SplitS)
        {
            splitSRollSign = local.x >= 0f ? 1f : -1f;
            splitSCompletionTimer = 0f;
            splitSRelativePlaneError = 0f;
            splitSRelativeInversion = -1f;
            splitSRollAligned = false;
            splitSPullStarted = false;
        }
        if (next == ManeuverState.ShiftTurnPlane) shiftTurnPlaneRollSign = local.x >= 0f ? -1f : 1f;
    }

    void ChangeTwoCirclePattern()
    {
        float roll = DeterministicAnalysisMode ? 0.5f : Random.value;
        EnterManeuver(roll < 0.34f ? ManeuverState.Extend
            : roll < 0.67f ? ManeuverState.SplitS : ManeuverState.ShiftTurnPlane);
    }

    Vector3 GetSplitSControlInput()
    {
        return splitSPullStarted
            ? new Vector3(1f, 0f, 0f)
            : new Vector3(0.15f, splitSRollSign, 0f);
    }

    void UpdateSplitS()
    {
        splitSRelativePlaneError = Mathf.Abs(Mathf.DeltaAngle(
            pitchAxisToEnemyAngle,
            pitchAxisToEnemyVelocityAngle));

        // ワールド上下ではなく、敵機の下面に対する相対的な背面姿勢を測る。
        splitSRelativeInversion = target != null
            ? Vector3.Dot(transform.up, -target.up)
            : -1f;

        splitSRollAligned =
            splitSRelativeInversion >= splitSRelativeInversionDot
            && splitSRelativePlaneError <= splitSRelativePitchTolerance;

        // 通常系は相対運動面への整列後にだけ開始する。
        // ハードタイムアウトは高精度な追跡相手でも機動を停止させない安全弁。
        if (!splitSPullStarted
            && (splitSRollAligned || maneuverElapsedTime >= splitSHardTimeout))
        {
            splitSPullStarted = true;
            splitSCompletionTimer = 0f;
        }

        if (!splitSPullStarted) return;

        splitSCompletionTimer += Time.deltaTime;
        if (splitSCompletionTimer >= splitSPullDuration)
            EnterOffensiveState(OffensiveState.OneCircle);
    }

    Vector3 GetShiftTurnPlaneControlInput()
    {
        return new Vector3(0.65f, shiftTurnPlaneRollInput * shiftTurnPlaneRollSign, 0f);
    }

    protected override float GetThrottleInput()
    {
        if (target == null) return cruiseThrottle;

        if (maneuverState == ManeuverState.Extend
            || maneuverState == ManeuverState.SplitS
            || maneuverState == ManeuverState.EvadeMissile
            || maneuverState == ManeuverState.RecoverAltitude)
            return fullThrottle;
        if (maneuverState == ManeuverState.Brake) return brakeThrottle;
        if (maneuverState == ManeuverState.ShiftTurnPlane) return attackThrottle;
        return attackThrottle;

#pragma warning disable CS0162
        switch (currentState)
        {
            case CombatState.LeadPursuit:
                return attackThrottle;
            case CombatState.Offset:
                return targetDistance < offsetRange ? brakeThrottle : attackThrottle;
            case CombatState.Brake:
                return brakeThrottle;
            case CombatState.Extend:
                return fullThrottle;
            case CombatState.EvadeMissile:
                return fullThrottle;
            case CombatState.RecoverAltitude:
                return fullThrottle;
            default:
                return cruiseThrottle;
        }
#pragma warning restore CS0162
    }

    void RefreshTarget()
    {
        if (Time.time < nextRetargetTime && target != null) return;
        nextRetargetTime = Time.time + retargetInterval;

        if (preferFcsTarget)
        {
            if (fcs == null) fcs = GetComponent<FCS_e>();
            if (fcs != null)
            {
                if (fcs.target != null)
                {
                    target = fcs.target.transform;
                    return;
                }

                if (fcs.waytarget != null)
                {
                    target = fcs.waytarget.transform;
                    return;
                }
            }
        }

        GameObject best = null;
        float bestScore = float.MinValue;
        if (ObjectManager.Instance == null || ObjectManager.Instance.allies == null) return;

        foreach (GameObject candidate in ObjectManager.Instance.allies)
        {
            if (candidate == null || candidate == gameObject) continue;

            Vector3 toCandidate = candidate.transform.position - transform.position;
            float distance = toCandidate.magnitude;
            if (distance > detectRange) continue;

            float forwardDot = Vector3.Dot(GetForwardReference(), toCandidate.normalized);
            float score = (detectRange - distance) + Mathf.Max(0f, forwardDot) * lockAssistRange;
            if (score <= bestScore) continue;

            bestScore = score;
            best = candidate;
        }

        target = best != null ? best.transform : null;
    }

#if false // Replaced by UpdateHierarchicalState.
    CombatState ChooseState()
    {
        ResetDecision();

        Vector3 toTarget = target.position - transform.position;
        Vector3 toTargetDir = SafeNormalize(toTarget, transform.forward);
        Vector3 targetToMeDir = SafeNormalize(transform.position - target.position, -target.forward);
        float altitude = transform.position.y;
        float toTargetAngle = Vector3.Angle(GetForwardReference(), toTargetDir);
        float targetNoseToMeAngle = Vector3.Angle(target.forward, targetToMeDir);
        bool baseOffensive = targetNoseToMeAngle > toTargetAngle;
        UpdateOffensiveTactic(baseOffensive);
        bool offensive = invertOffensiveTactic ? !baseOffensive : baseOffensive;
        bool threatened = targetNoseToMeAngle < threatenedAngle;
        bool targetBehindMe = toTargetAngle > 90f;
        bool leadPursuitReady = offensive && toTargetAngle < 30f && targetDistance < leadPursuitRange;
        bool brakeReady = targetBehindMe && threatened && targetDistance < brakeDistance * 0.6f;
        bool extendReady = targetBehindMe && !threatened && targetDistance > extendRange;
        float altitudeDangerScore = GetAltitudeDangerScore(altitude);
        UpdateStateConditions(offensive, threatened, targetBehindMe, leadPursuitReady, brakeReady, extendReady, altitudeDangerScore);

        if (TryPickRandomManeuver(offensive, threatened, targetBehindMe, brakeReady, extendReady, toTargetAngle))
            return currentState;

        if (TryGetStateCandidate(out CombatState candidateState))
            return candidateState;

        return currentState;
    }

    bool TryPickRandomManeuver(bool offensive, bool threatened, bool targetBehindMe, bool brakeReady, bool extendReady, float toTargetAngle)
    {
        if (DeterministicAnalysisMode) return false;

        if (Time.time < nextRandomManeuverTime || Time.time < nextRandomManeuverCheckTime)
            return false;

        nextRandomManeuverCheckTime = Time.time + randomManeuverCheckInterval;
        if (Random.value >= randomManeuverChance)
            return false;

        nextRandomManeuverTime = Time.time + randomManeuverCooldown;

        if (missileThreat > 0f)
        {
            ForceState(CombatState.EvadeMissile);
            return true;
        }

        if (offensive)
        {
            ForceState(CombatState.LeadPursuit);
            return true;
        }

        if (threatened || targetBehindMe)
        {
            float roll = Random.value;
            if (roll < 0.4f && brakeReady)
                ForceState(CombatState.Brake);
            else if (extendReady)
                ForceState(CombatState.Extend);
            else
                ForceState(CombatState.Offset);

            return true;
        }

        return false;
    }

    void UpdateOffensiveTactic(bool baseOffensive)
    {
        if (DeterministicAnalysisMode)
        {
            invertOffensiveTactic = false;
            return;
        }

        if (!baseOffensive) return;
        if (Time.time < nextTacticSwitchTime || Time.time < nextTacticSwitchCheckTime) return;

        nextTacticSwitchCheckTime = Time.time + tacticSwitchCheckInterval;
        if (Random.value >= tacticSwitchChance) return;

        invertOffensiveTactic = !invertOffensiveTactic;
        nextTacticSwitchTime = Time.time + tacticSwitchCooldown;
    }

    void ForceState(CombatState state)
    {
        currentState = state;
        bookedState = state;
        bookedStateTimer = 0f;
        stateTimer = 0f;
        nextDirectionRefreshTime = 0f;
        ResetStateRemainTime(state);
        PickEvadeMissileManeuver(state);
    }

    void PickEvadeMissileManeuver(CombatState state)
    {
        if (state != CombatState.EvadeMissile)
        {
            evadeMissileUseBarrelRoll = false;
            return;
        }

        if (DeterministicAnalysisMode)
        {
            evadeMissileUseBarrelRoll = false;
            barrelRollSign = 1f;
        }
        else
        {
            evadeMissileUseBarrelRoll = Random.value < missileBarrelRollChance;
            barrelRollSign = Random.value < 0.5f ? -1f : 1f;
        }
    }

    void UpdateStateChoiceTimes()
    {
        for (int i = 0; i < stateTunings.Length; i++)
        {
            StateTuning tuning = stateTunings[i];
            int runtimeIndex = FindRuntimeIndex(tuning.state);
            if (runtimeIndex < 0) continue;

            StateRuntime runtime = stateRuntime[runtimeIndex];
            float needToChoiceTime = Mathf.Max(0.01f, tuning.needToChoiceTime);
            float maxTime = Mathf.Max(needToChoiceTime, tuning.maxTime);
            float recoverSpeed = tuning.state == currentState
                ? 1f
                : needToChoiceTime / Mathf.Max(0.01f, stateRemainRecoverSeconds);

            runtime.remainTime = Mathf.Clamp(runtime.remainTime + Time.deltaTime * recoverSpeed, 0f, maxTime);
            runtime.weight = GetStateWeight(tuning, runtime);
            stateRuntime[runtimeIndex] = runtime;
        }
    }

    float GetStateWeight(StateTuning tuning, StateRuntime runtime)
    {
        float needToChoiceTime = Mathf.Max(0.01f, tuning.needToChoiceTime);
        if (!runtime.conditionMet || runtime.remainTime <= needToChoiceTime)
            return 0f;

        return runtime.remainTime / needToChoiceTime;
    }

    void ResetStateRemainTime(CombatState state)
    {
        int runtimeIndex = FindRuntimeIndex(state);
        if (runtimeIndex < 0) return;

        StateRuntime runtime = stateRuntime[runtimeIndex];
        runtime.remainTime = 0f;
        runtime.weight = 0f;
        stateRuntime[runtimeIndex] = runtime;
    }

    void UpdateStateConditions(bool offensive, bool threatened, bool targetBehindMe, bool leadPursuitReady, bool brakeReady, bool extendReady, float altitudeDangerScore)
    {
        SetStateCondition(CombatState.LeadPursuit, offensive && leadPursuitReady);
        SetStateCondition(CombatState.Offset, !offensive && threatened);
        SetStateCondition(CombatState.Brake, brakeReady);
        SetStateCondition(CombatState.Extend, extendReady);
        SetStateCondition(CombatState.EvadeMissile, missileThreat > 0f);
        SetStateCondition(CombatState.RecoverAltitude, altitudeDangerScore > 0f);
    }

    void SetStateCondition(CombatState state, bool conditionMet)
    {
        int tuningIndex = FindTuningIndex(state);
        int runtimeIndex = FindRuntimeIndex(state);
        if (tuningIndex < 0 || runtimeIndex < 0) return;

        StateRuntime runtime = stateRuntime[runtimeIndex];
        runtime.conditionMet = conditionMet;
        if (DeterministicAnalysisMode && !conditionMet)
            runtime.remainTime = 0f;
        runtime.weight = GetStateWeight(stateTunings[tuningIndex], runtime);
        stateRuntime[runtimeIndex] = runtime;
    }

    bool TryGetStateCandidate(out CombatState state)
    {
        if (DeterministicAnalysisMode)
        {
            int bestIndex = -1;
            float bestWeight = 0f;
            for (int i = 0; i < stateRuntime.Length; i++)
            {
                if (stateRuntime[i].weight <= bestWeight) continue;
                bestWeight = stateRuntime[i].weight;
                bestIndex = i;
            }

            state = bestIndex >= 0 ? stateRuntime[bestIndex].state : currentState;
            return bestIndex >= 0;
        }

        float totalWeight = 0f;
        for (int i = 0; i < stateRuntime.Length; i++)
            totalWeight += stateRuntime[i].weight;

        if (totalWeight <= 0f)
        {
            state = currentState;
            return false;
        }

        float roll = Random.value * totalWeight;
        for (int i = 0; i < stateRuntime.Length; i++)
        {
            roll -= stateRuntime[i].weight;
            if (roll > 0f) continue;

            state = stateRuntime[i].state;
            return true;
        }

        state = currentState;
        return false;
    }

    void ApplyStateTransition(CombatState nextState)
    {
        stateTimer += Time.deltaTime;
        if (nextState == currentState)
        {
            bookedState = currentState;
            bookedStateTimer = 0f;
            return;
        }

        if (stateTimer < GetTuning(currentState).minimumDuration) return;

        if (bookedState != nextState)
        {
            bookedState = nextState;
            bookedStateTimer = 0f;
        }

        bookedStateTimer += Time.deltaTime;
        if (bookedStateTimer < GetTuning(bookedState).enterDelay) return;

        currentState = bookedState;
        ResetStateRemainTime(currentState);
        PickEvadeMissileManeuver(currentState);
        bookedStateTimer = 0f;
        stateTimer = 0f;
        nextDirectionRefreshTime = 0f;
    }

#endif
    Vector3 BuildTargetDirection()
    {
        if (target == null) return transform.forward;

        Vector3 directTargetDirection = SafeNormalize(target.position - transform.position, transform.forward);
        Vector3 interceptDirection = CalculateLeadDirection(out Vector3 vectorToInterceptPoint);

        if (maneuverState == ManeuverState.EvadeMissile)
            return SafeNormalize(missileEvadeDirection + GetForwardReference() * evadeForwardWeight
                + interceptDirection * evadeTargetWeight, transform.right);
        if (maneuverState == ManeuverState.RecoverAltitude) return GetAltitudeRecoveryDirection();
        if (maneuverState == ManeuverState.Extend)
            return SafeNormalize(transform.position - target.position + Vector3.up * 120f, -directTargetDirection);
        if (maneuverState == ManeuverState.Brake)
            return SafeNormalize(GetForwardReference() * 0.7f + GetOffsetVector() * 0.0015f, transform.forward);

        Vector3 pursuit = targetDistance < offsetRange
            ? GetLagPursuitDirection()
            : BuildAttackApproachDirection(interceptDirection, vectorToInterceptPoint);
        if (combatMode == CombatMode.Offensive && offensiveState == OffensiveState.OneCircle)
            return BuildOneCircleDirection(pursuit);
        return pursuit;

#pragma warning disable CS0162
        switch (currentState)
        {
            case CombatState.LeadPursuit:
                return targetDistance < offsetRange ? GetLagPursuitDirection() : BuildAttackApproachDirection(interceptDirection, vectorToInterceptPoint);
            case CombatState.Offset:
                return SafeNormalize(BuildAttackApproachDirection(interceptDirection, vectorToInterceptPoint) + GetOffsetVector() * 0.001f, directTargetDirection);
            case CombatState.Brake:
                return SafeNormalize(GetForwardReference() * 0.7f + GetOffsetVector() * 0.0015f, transform.forward);
            case CombatState.Extend:
                return SafeNormalize(transform.position - target.position + Vector3.up * 120f, -directTargetDirection);
            case CombatState.EvadeMissile:
                if (evadeMissileUseBarrelRoll)
                {
                    return SafeNormalize(
                        GetForwardReference() * 0.65f
                        + missileEvadeDirection.normalized * 0.35f,
                        transform.forward);
                }

                return SafeNormalize(
                    missileEvadeDirection.normalized
                    + GetForwardReference() * evadeForwardWeight
                    + interceptDirection * evadeTargetWeight,
                    transform.right);
            case CombatState.RecoverAltitude:
                return GetAltitudeRecoveryDirection();
            default:
                return directTargetDirection;
        }
#pragma warning restore CS0162
    }

    Vector3 BuildOneCircleDirection(Vector3 pursuitDirection)
    {
        Vector3 local = transform.InverseTransformDirection(SafeNormalize(pursuitDirection, transform.forward));
        local.y = Mathf.Abs(local.y) * oneCirclePitchSign;
        return SafeNormalize(transform.TransformDirection(local), transform.forward);
    }

    Vector3 GetOffsetVector()
    {
        if (DeterministicAnalysisMode)
        {
            Vector3 toTarget = target != null
                ? target.position - transform.position
                : transform.forward;
            Vector3 fixedSide = Vector3.Cross(Vector3.up, SafeNormalize(toTarget, transform.forward));
            if (fixedSide.sqrMagnitude < 0.001f) fixedSide = transform.right;
            offsetVector = fixedSide.normalized * offsetRadius;
            return offsetVector;
        }

        if (Time.time < nextOffsetRefreshTime) return offsetVector;

        nextOffsetRefreshTime = Time.time + offsetRefreshInterval;
        Vector3 side = Vector3.Cross(Vector3.up, SafeNormalize(target.position - transform.position, transform.forward));
        if (side.sqrMagnitude < 0.001f) side = transform.right;

        side.Normalize();
        if (Random.value < 0.5f) side = -side;

        float vertical = Random.Range(-0.35f, 0.5f);
        offsetVector = (side + Vector3.up * vertical).normalized * offsetRadius;
        return offsetVector;
    }

    Vector3 BuildAttackApproachDirection(Vector3 interceptDirection, Vector3 vectorToInterceptPoint)
    {
        if (target == null) return interceptDirection;

        Vector3 directTargetDirection = SafeNormalize(target.position - transform.position, transform.forward);
        Vector3 lateralDirection = Vector3.Cross(Vector3.up, directTargetDirection);
        if (lateralDirection.sqrMagnitude < 0.001f) lateralDirection = transform.right;
        lateralDirection.Normalize();
        if (Vector3.Dot(lateralDirection, transform.right) < 0f) lateralDirection = -lateralDirection;

        Vector3 approachDirection = Quaternion.AngleAxis(attackApproachAngle, Vector3.up) * directTargetDirection;
        if (Vector3.Dot(approachDirection, lateralDirection) < 0f)
            approachDirection = Quaternion.AngleAxis(-attackApproachAngle, Vector3.up) * directTargetDirection;

        return SafeNormalize(interceptDirection + vectorToInterceptPoint.normalized * 0.15f + approachDirection * 0.35f, interceptDirection);
    }

    Vector3 GetLagPursuitDirection()
    {
        if (target == null) return transform.forward;

        Rigidbody otherRb = target.GetComponent<Rigidbody>();
        Vector3 targetVelocity = otherRb != null ? otherRb.linearVelocity : target.forward * 120f;
        Vector3 lagPoint = target.position - targetVelocity * lagPursuitSeconds;
        return SafeNormalize(lagPoint - transform.position, transform.forward);
    }

    Vector3 GetAltitudeRecoveryDirection()
    {
        if (transform.position.y < minAltitude)
            return SafeNormalize(Vector3.up + transform.forward * 0.3f, Vector3.up);

        if (transform.position.y > maxAltitude)
            return SafeNormalize(Vector3.down + transform.forward * 0.35f, Vector3.down);

        Vector3 desiredPoint = new Vector3(transform.position.x, desiredAltitude, transform.position.z) + transform.forward * 500f;
        return SafeNormalize(desiredPoint - transform.position, transform.forward);
    }

    float EvaluateMissileThreat(out Vector3 evadeDirection)
    {
        evadeDirection = Vector3.zero;
        SenseIncomingMissiles(out _, 4);
        if (missileThreats.Count == 0) return 0f;

        EnemyMissileThreatSensor.ThreatInfo highest = missileThreats[0];
        for (int i = 1; i < missileThreats.Count; i++)
        {
            if (GetMissileDistanceThreat(missileThreats[i]) > GetMissileDistanceThreat(highest))
                highest = missileThreats[i];
        }

        evadeDirection = highest.evadeDirection;
        return GetMissileDistanceThreat(highest);
    }

    float GetMissileDistanceThreat(EnemyMissileThreatSensor.ThreatInfo threat)
    {
        float range = Mathf.Max(1f, missileThreatFarDistance - missileThreatNearDistance);
        return Mathf.Clamp01((missileThreatFarDistance - threat.dist) / range) * 1000f;
    }

    float GetAltitudeDangerScore(float altitude)
    {
        float score = 0f;
        if (altitude < minAltitude)
            score = (minAltitude - altitude) * 2f;
        else if (altitude > maxAltitude)
            score = (altitude - maxAltitude) * 2f;

        if (rb != null)
        {
            float predictedY = altitude + rb.linearVelocity.y * 2f;
            if (rb.linearVelocity.y < -10f && predictedY < minAltitude)
                score = Mathf.Max(score, (minAltitude - predictedY) * 2f);
        }

        if (transform.forward.y < -0.2f && altitude < minAltitude + 400f)
            score = Mathf.Max(score, 1200f);

        return score;
    }

    Vector3 CalculateLeadDirection(out Vector3 vectorToInterceptPoint)
    {
        vectorToInterceptPoint = Vector3.zero;
        if (target == null || rb == null) return transform.forward;

        if (targetRb == null || targetRb.transform != target)
            targetRb = target.GetComponent<Rigidbody>();

        if (targetRb == null)
            return SafeNormalize(target.position - transform.position, transform.forward);

        float bulletSpeed = 200f;
        if (fcs == null) fcs = GetComponent<FCS_e>();
        if (fcs != null)
            bulletSpeed = Mathf.Max(1f, fcs.bulletSpeed);

        Vector3 muzzlePos = transform.position;
        Vector3 bulletVel0 = rb.linearVelocity + transform.forward * bulletSpeed;
        float t = PredictInterceptTime(muzzlePos, bulletVel0, targetRb.position, targetRb.linearVelocity, bulletSpeed);
        Vector3 aimPoint = targetRb.position + targetRb.linearVelocity * t;
        vectorToInterceptPoint = aimPoint - transform.position;
        return SafeNormalize(vectorToInterceptPoint, target.position - transform.position);
    }

    float PredictInterceptTime(Vector3 muzzlePos, Vector3 bulletVel0, Vector3 targetPos, Vector3 targetVel, float bulletSpeed)
    {
        if (bulletSpeed <= 0.01f) return 0f;

        float t = interceptTimeCache > 0f
            ? interceptTimeCache
            : Vector3.Distance(muzzlePos, targetPos) / (bulletSpeed + Mathf.Max(rb.linearVelocity.magnitude, 1f));

        for (int i = 0; i < 5; i++)
        {
            Vector3 futureTarget = targetPos + targetVel * t;
            Vector3 bulletFuture = muzzlePos + bulletVel0 * t + 0.5f * Physics.gravity * t * t;
            if (Vector3.Distance(bulletFuture, futureTarget) < 0.5f) break;

            float distance = Vector3.Distance(muzzlePos, futureTarget);
            t = distance / (bulletSpeed + Mathf.Max(rb.linearVelocity.magnitude, 1f));
        }

        if (float.IsNaN(t) || float.IsInfinity(t) || t < 0f)
            t = 0f;

        interceptTimeCache = Mathf.Clamp(t, 0f, 30f);
        return interceptTimeCache;
    }

    Vector3 SteerToward(Vector3 worldDirection)
    {
        Vector3 localDir = transform.InverseTransformDirection(SafeNormalize(worldDirection, transform.forward));
        float downFactor = Mathf.Clamp01(-localDir.y);

        float roll = Mathf.Clamp(localDir.x, -1f, 1f);
        float pitchScale = Mathf.Lerp(downwardPitchLimit, 1f, 1f - downFactor);
        float pitch = Mathf.Clamp(localDir.y, -1f, 1f) * pitchScale;
        float yaw = Mathf.Clamp(localDir.x, -1f, 1f) * downFactor * Mathf.Abs(roll) * yawAssist;

        if (maneuverState == ManeuverState.EvadeMissile && evadeMissileUseBarrelRoll)
        {
            roll = Mathf.Clamp(barrelRollInput * barrelRollSign, -1f, 1f);
            pitch = Mathf.Clamp(pitch + 0.25f, -1f, 1f);
        }

        return new Vector3(pitch, roll, yaw);
    }

    bool IsTargetInFront(float angle)
    {
        if (target == null) return false;

        Vector3 toTarget = target.position - transform.position;
        if (toTarget.sqrMagnitude <= 0.001f) return true;

        return Vector3.Angle(GetForwardReference(), toTarget.normalized) <= angle;
    }

    Vector3 GetForwardReference()
    {
        if (rb != null && rb.linearVelocity.sqrMagnitude > 25f)
            return rb.linearVelocity.normalized;

        return transform.forward;
    }

    Vector3 SafeNormalize(Vector3 value, Vector3 fallback)
    {
        if (value.sqrMagnitude > 0.0001f && IsFinite(value))
            return value.normalized;

        if (fallback.sqrMagnitude > 0.0001f && IsFinite(fallback))
            return fallback.normalized;

        return Vector3.forward;
    }

    bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x)
            && !float.IsNaN(value.y)
            && !float.IsNaN(value.z)
            && !float.IsInfinity(value.x)
            && !float.IsInfinity(value.y)
            && !float.IsInfinity(value.z);
    }

#if false
    StateTuning GetTuning(CombatState state)
    {
        int index = FindTuningIndex(state);
        if (index >= 0)
            return stateTunings[index];

        return new StateTuning
        {
            state = state,
            enterDelay = 0.2f,
            minimumDuration = 0.5f,
            needToChoiceTime = 6f,
            maxTime = 20f
        };
    }

    int FindTuningIndex(CombatState state)
    {
        for (int i = 0; i < stateTunings.Length; i++)
        {
            if (stateTunings[i].state == state)
                return i;
        }

        return -1;
    }

    int FindRuntimeIndex(CombatState state)
    {
        if (stateRuntime == null) return -1;

        for (int i = 0; i < stateRuntime.Length; i++)
        {
            if (stateRuntime[i].state == state)
                return i;
        }

        return -1;
    }
#endif
    public int SenseIncomingMissiles(out Vector3[] approachDirections, int maxCount)
    {
        int count = EnemyMissileThreatSensor.SenseIncomingMissiles(
            transform,
            rb,
            missileThreats,
            missileDetectRange,
            missileApproachAngle,
            missileCriticalTime);

        int outputCount = Mathf.Clamp(maxCount, 0, count);
        if (sensedMissileDirections.Length != outputCount)
            sensedMissileDirections = new Vector3[outputCount];

        for (int i = 0; i < outputCount; i++)
            sensedMissileDirections[i] = missileThreats[i].approachDirection;

        approachDirections = sensedMissileDirections;
        return count;
    }

    void ResetDecision()
    {
        evadeMissileUseBarrelRoll = false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + commandedFlightDirection.normalized * 500f);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + missileEvadeDirection.normalized * 400f);

        if (target != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, target.position);
        }
    }
}
