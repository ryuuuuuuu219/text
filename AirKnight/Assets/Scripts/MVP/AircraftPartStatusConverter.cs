using UnityEngine;
using UnityEngine.Serialization;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(AircraftStatus))]
public sealed class AircraftPartStatusConverter : MonoBehaviour
{
    [Header("Live Test")]
    [Tooltip("InspectorでONにすると、次のUpdateで再計算して自動的にOFFへ戻ります。")]
    public bool calculateSwitch;
    public bool calculateOnStart = true;
    public bool resetCurrentHitPointsAfterCalculation = true;

    [Header("Conversion Coefficients")]
    [Min(0.0001f)] public float aerodynamicDragCoefficient = 1f;
    [Min(0.0001f)] public float airViscosity = 1f;
    [FormerlySerializedAs("maximumSpeedScale")]
    [Min(0f)] public float equilibriumSpeedScale = 1f;
    [Min(0f)] public float accelerationScale = 1.28f;
    [Min(0f)] public float idealDiveGravityAcceleration = 9.81f;
    [Min(0f)] public float operationMinutesPerFuelVolume = 1f;
    [Min(0f)] public float physicsMassPerWeight = 0.001f;
    [Min(0.01f)] public float minimumPhysicsMass = 0.1f;

    [Header("Maneuver Conversion")]
    [Tooltip("Roll rate gained per metre of total main-wing width (deg/s per m).")]
    [Min(0f)] public float wingLengthRollScale = 0.2f;
    [Tooltip("Base yaw rate in degrees per second.")]
    [Min(0f)] public float baseYawPerformance = 30f;

    [Header("Calculated Debug Values")]
    [SerializeField] float totalThrust;
    [SerializeField] float effectiveWingArea;
    [SerializeField] float totalForwardProjectedArea;
    [SerializeField] float minimumSafetyFactor = 1f;
    [SerializeField] float fuselageBottomArea;
    [SerializeField] float fuselageProjectedArea;
    [SerializeField] float wingBottomArea;
    [SerializeField] float wingProjectedArea;

    AircraftStatus targetStatus;
    AircraftController targetController;

    // Future scene-transition inputs. Runtime AI and JSON loading are intentionally not active yet.
    // [SerializeField] AircraftFlightAI targetAircraftAI;
    // [SerializeField] string aircraftPartsJsonPath;

    public float FuselageBottomArea => fuselageBottomArea;
    public float FuselageProjectedArea => fuselageProjectedArea;
    public float WingBottomArea => wingBottomArea;
    public float WingProjectedArea => wingProjectedArea;

    void Start()
    {
        if (calculateOnStart) Calc();
    }

    void Update()
    {
        if (!calculateSwitch) return;
        calculateSwitch = false;
        Calc();
    }

    [ContextMenu("Calculate Aircraft Status")]
    public void Calc()
    {
        targetStatus = GetComponentInParent<AircraftStatus>();
        if (targetStatus == null) targetStatus = GetComponentInChildren<AircraftStatus>(true);
        if (targetStatus == null)
        {
            Debug.LogError("AircraftPartStatusConverter could not find AircraftStatus.", this);
            return;
        }

        targetController = targetStatus.GetComponent<AircraftController>();
        AircraftPartStatus[] allParts = GetComponentsInChildren<AircraftPartStatus>(true);
        if (allParts.Length == 0)
        {
            Debug.LogWarning("No aircraft part status components were found.", this);
            return;
        }

        float totalWeight = 0f;
        float totalPartHitPoints = 0f;
        minimumSafetyFactor = float.PositiveInfinity;
        totalForwardProjectedArea = 0f;
        for (int i = 0; i < allParts.Length; i++)
        {
            AircraftPartStatus part = allParts[i];
            totalWeight += part.TotalWeight;
            totalPartHitPoints += part.TotalHitPoints;
            minimumSafetyFactor = Mathf.Min(minimumSafetyFactor, part.EffectiveSafetyFactor);
            totalForwardProjectedArea += part.ForwardProjectedArea;
        }
        if (!float.IsFinite(minimumSafetyFactor)) minimumSafetyFactor = 1f;

        FuselagePartStatus[] fuselages = GetComponentsInChildren<FuselagePartStatus>(true);
        MainWingPartStatus[] mainWings = GetComponentsInChildren<MainWingPartStatus>(true);
        AuxiliaryWingPartStatus[] auxiliaryWings = GetComponentsInChildren<AuxiliaryWingPartStatus>(true);
        EnginePartStatus[] engines = GetComponentsInChildren<EnginePartStatus>(true);
        ControlSurfacePartStatus[] controlSurfaces = GetComponentsInChildren<ControlSurfacePartStatus>(true);
        ArmorPartStatus[] armorParts = GetComponentsInChildren<ArmorPartStatus>(true);
        FuelTankPartStatus[] fuelTanks = GetComponentsInChildren<FuelTankPartStatus>(true);
        HardpointPartStatus[] hardpoints = GetComponentsInChildren<HardpointPartStatus>(true);

        float fuselageHpCoefficient = 1f;
        float fuselageVolume = 0f;
        fuselageBottomArea = 0f;
        fuselageProjectedArea = 0f;
        for (int i = 0; i < fuselages.Length; i++)
        {
            fuselageHpCoefficient *= Mathf.Max(0f, fuselages[i].hitPointCoefficient);
            fuselageVolume += fuselages[i].InternalVolume;
            fuselageBottomArea += fuselages[i].BroadsideArea;
            fuselageProjectedArea += fuselages[i].ForwardProjectedArea;
        }

        effectiveWingArea = 0f;
        wingBottomArea = 0f;
        wingProjectedArea = 0f;
        float totalMainWingLength = 0f;
        int hardpointCount = 0;
        float maximumHardpointWeight = 0f;
        for (int i = 0; i < mainWings.Length; i++)
        {
            effectiveWingArea += mainWings[i].WingArea;
            wingBottomArea += mainWings[i].BroadsideArea;
            wingProjectedArea += mainWings[i].ForwardProjectedArea;
            totalMainWingLength += mainWings[i].width;
            int wingHardpointCount = Mathf.Max(0, mainWings[i].hardpointCount);
            hardpointCount += wingHardpointCount;
            maximumHardpointWeight += mainWings[i].maximumHardpointWeight * wingHardpointCount;
        }

        float auxiliaryRollMultiplier = 1f;
        for (int i = 0; i < auxiliaryWings.Length; i++)
        {
            if (auxiliaryWings[i].contributesToEffectiveWingArea)
                effectiveWingArea += auxiliaryWings[i].WingArea;
            wingBottomArea += auxiliaryWings[i].BroadsideArea;
            wingProjectedArea += auxiliaryWings[i].ForwardProjectedArea;
            auxiliaryRollMultiplier *= Mathf.Max(0f, auxiliaryWings[i].rollPerformanceMultiplier);
        }

        totalThrust = 0f;
        float rollAccuracySum = 0f;
        int rollAccuracyCount = 0;
        for (int i = 0; i < engines.Length; i++)
        {
            totalThrust += engines[i].TotalThrust;
            float spacingPenalty = 1f / (1f + engines[i].engineSpacing * 0.01f);
            rollAccuracySum += engines[i].placementPrecision * spacingPenalty;
            rollAccuracyCount++;
        }

        float defenseMultiplier = 1f;
        for (int i = 0; i < armorParts.Length; i++)
            defenseMultiplier *= Mathf.Max(0f, armorParts[i].defenseMultiplier);

        float fuelVolume = 0f;
        for (int i = 0; i < fuelTanks.Length; i++) fuelVolume += fuelTanks[i].InternalVolume;
        for (int i = 0; i < hardpoints.Length; i++)
        {
            hardpointCount += Mathf.Max(1, hardpoints[i].hardpointCount);
            maximumHardpointWeight += hardpoints[i].TotalMaximumWeaponWeight;
        }

        float safeWeight = Mathf.Max(0.01f, totalWeight);
        float safeProjectedArea = Mathf.Max(0.01f, totalForwardProjectedArea);
        float levelFlightEquilibriumSpeed = Mathf.Sqrt(
            Mathf.Max(0f, totalThrust)
            / (Mathf.Max(0.0001f, aerodynamicDragCoefficient)
                * Mathf.Max(0.0001f, airViscosity)
                * safeProjectedArea)) * equilibriumSpeedScale;
        levelFlightEquilibriumSpeed = Mathf.Max(1f, levelFlightEquilibriumSpeed);
        float acceleration = totalThrust / safeWeight * accelerationScale;
        float idealDiveEquilibriumSpeed = acceleration > 0.0001f
            ? levelFlightEquilibriumSpeed * Mathf.Sqrt(
                (acceleration + Mathf.Max(0f, idealDiveGravityAcceleration)) / acceleration)
            : levelFlightEquilibriumSpeed;

        BuildPitchPerformance(
            mainWings,
            out AnimationCurve pitchCurve);

        float rollPerformance = totalMainWingLength * wingLengthRollScale;
        for (int i = 0; i < controlSurfaces.Length; i++)
            rollPerformance += controlSurfaces[i].rollPerformance;
        rollPerformance *= auxiliaryRollMultiplier;

        targetStatus.totalWeight = totalWeight;
        targetStatus.rigidbodyMass = Mathf.Max(minimumPhysicsMass, totalWeight * physicsMassPerWeight);
        targetStatus.maxHitPoints = Mathf.Max(1f, totalPartHitPoints * fuselageHpCoefficient * defenseMultiplier);
        targetStatus.levelFlightEquilibriumSpeed = levelFlightEquilibriumSpeed;
        targetStatus.idealDiveEquilibriumSpeed = Mathf.Max(
            levelFlightEquilibriumSpeed,
            idealDiveEquilibriumSpeed);
        targetStatus.acceleration = Mathf.Max(0f, acceleration);
        targetStatus.breakupSpeed = Mathf.Max(
            1f,
            idealDiveEquilibriumSpeed * minimumSafetyFactor);
        targetStatus.pitchPerformance = pitchCurve;
        targetStatus.rollPerformance = Mathf.Max(0f, rollPerformance);
        targetStatus.rollAccuracy = rollAccuracyCount > 0
            ? Mathf.Clamp01(rollAccuracySum / rollAccuracyCount)
            : 1f;
        targetStatus.yawPerformance = baseYawPerformance;
        targetStatus.operationDurationMinutes = fuelVolume * operationMinutesPerFuelVolume;
        targetStatus.hardpointCount = hardpointCount;
        targetStatus.maximumHardpointWeight = maximumHardpointWeight;
        targetStatus.fuselageBottomArea = fuselageBottomArea;
        targetStatus.fuselageProjectedArea = fuselageProjectedArea;
        targetStatus.wingBottomArea = wingBottomArea;
        targetStatus.wingProjectedArea = wingProjectedArea;
        targetStatus.internalVolume = fuselageVolume;
        targetStatus.NotifyCalculatedValuesChanged(resetCurrentHitPointsAfterCalculation);

        if (targetController != null)
            targetStatus.ApplyTo(targetController, targetStatus.GetComponent<Rigidbody>());
    }

    public void GetAoaProjectedAreas(
        float absoluteAoaDegrees,
        out float effectiveFuselageArea,
        out float effectiveWingAreaResult)
    {
        float radians = Mathf.Clamp(Mathf.Abs(absoluteAoaDegrees), 0f, 90f) * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        effectiveFuselageArea = sin * fuselageBottomArea + cos * fuselageProjectedArea;
        effectiveWingAreaResult = sin * wingBottomArea + cos * wingProjectedArea;
    }

    public float GetTotalAoaProjectedArea(float absoluteAoaDegrees)
    {
        GetAoaProjectedAreas(absoluteAoaDegrees, out float fuselageArea, out float wingArea);
        return Mathf.Max(0f, fuselageArea + wingArea);
    }

    void BuildPitchPerformance(
        MainWingPartStatus[] mainWings,
        out AnimationCurve curve)
    {
        if (mainWings.Length == 0)
        {
            curve = AnimationCurve.Linear(0f, 0f, 1f, 0f);
            return;
        }

        float low = 0f;
        float maximum = 0f;
        float optimal = 0f;
        float limit = float.PositiveInfinity;
        int wingSampleCount = 0;
        for (int i = 0; i < mainWings.Length; i++)
        {
            low += mainWings[i].lowSpeedPerformance;
            maximum += mainWings[i].maximumPerformance;
            optimal += mainWings[i].optimalSpeed;
            limit = Mathf.Min(limit, mainWings[i].controlLimitSpeed);
            wingSampleCount++;
        }

        low /= Mathf.Max(1, wingSampleCount);
        maximum /= Mathf.Max(1, wingSampleCount);
        optimal /= Mathf.Max(1, wingSampleCount);
        limit = Mathf.Max(optimal + 0.01f, limit);
        maximum = Mathf.Max(0f, maximum);

        if (!TrySolvePitchCubic(low, maximum, optimal, limit,
                out float a, out float b, out float c, out float d))
        {
            Debug.LogWarning("Invalid pitch curve inputs; a linear fallback curve was generated.", this);
            curve = AnimationCurve.Linear(0f, Mathf.Max(0f, low), limit, 0f);
            return;
        }

        float endTangent = 3f * a * limit * limit + 2f * b * limit + c;
        Keyframe start = new(0f, d, c, c);
        Keyframe end = new(limit, 0f, endTangent, endTangent);
        curve = new AnimationCurve(start, end)
        {
            preWrapMode = WrapMode.ClampForever,
            postWrapMode = WrapMode.ClampForever
        };
    }

    static bool TrySolvePitchCubic(
        float low,
        float maximum,
        float optimal,
        float limit,
        out float a,
        out float b,
        out float c,
        out float d)
    {
        a = b = c = 0f;
        d = low;
        if (!(optimal > 0f && optimal < limit)) return false;

        float[,] matrix =
        {
            { optimal * optimal * optimal, optimal * optimal, optimal, maximum - low },
            { 3f * optimal * optimal, 2f * optimal, 1f, 0f },
            { limit * limit * limit, limit * limit, limit, -low }
        };

        for (int column = 0; column < 3; column++)
        {
            int pivot = column;
            for (int row = column + 1; row < 3; row++)
                if (Mathf.Abs(matrix[row, column]) > Mathf.Abs(matrix[pivot, column])) pivot = row;
            if (Mathf.Abs(matrix[pivot, column]) < 0.000001f) return false;
            if (pivot != column)
                for (int j = column; j < 4; j++)
                {
                    float swap = matrix[column, j];
                    matrix[column, j] = matrix[pivot, j];
                    matrix[pivot, j] = swap;
                }

            float divisor = matrix[column, column];
            for (int j = column; j < 4; j++) matrix[column, j] /= divisor;
            for (int row = 0; row < 3; row++)
            {
                if (row == column) continue;
                float factor = matrix[row, column];
                for (int j = column; j < 4; j++) matrix[row, j] -= factor * matrix[column, j];
            }
        }

        a = matrix[0, 3];
        b = matrix[1, 3];
        c = matrix[2, 3];
        float secondDerivativeAtOptimal = 6f * a * optimal + 2f * b;
        if (!float.IsFinite(a) || !float.IsFinite(b) || !float.IsFinite(c)
            || secondDerivativeAtOptimal >= 0f)
            return false;

        const int validationSamples = 32;
        float previous = low;
        for (int i = 1; i <= validationSamples; i++)
        {
            float velocity = optimal * i / validationSamples;
            float value = ((a * velocity + b) * velocity + c) * velocity + d;
            if (!float.IsFinite(value) || value < -0.001f) return false;
            if (value + 0.001f < previous) return false;
            previous = value;
        }

        previous = maximum;
        for (int i = 1; i <= validationSamples; i++)
        {
            float velocity = Mathf.Lerp(optimal, limit, (float)i / validationSamples);
            float value = ((a * velocity + b) * velocity + c) * velocity + d;
            if (!float.IsFinite(value) || value < -0.001f) return false;
            if (value - 0.001f > previous) return false;
            previous = value;
        }
        return true;
    }
}
