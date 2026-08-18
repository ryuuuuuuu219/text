using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AircraftController))]
public class maneuverLog : MonoBehaviour
{
    [Header("Logging")]
    [Min(0.02f)] public float loginterval = 0.25f;
    [Min(1)] public int maximumLogEntries = 200;

    [Header("Runtime Display")]
    public bool showOverlay = true;
    public Rect overlayRect = new(10f, 10f, 430f, 260f);

    [TextArea(8, 20)] public string log;
    [TextArea(8, 20)] public string latest;

    float timer;
    float peakDeceleration;
    int entryNumber;

    AircraftController aircraftController;
    AircraftManeuverController maneuverController;
    PilotStatus pilotStatus;

    void Start()
    {
        aircraftController = GetComponent<AircraftController>();
        maneuverController = GetComponent<AircraftManeuverController>();
        pilotStatus = GetComponent<PilotStatus>();
    }

    void FixedUpdate()
    {
        if (maneuverController != null)
        {
            peakDeceleration = Mathf.Max(
                peakDeceleration,
                maneuverController.CurrentDeceleration);
        }
    }

    void Update()
    {
        latest = BuildSnapshot();
        timer += Time.deltaTime;
        if (timer < loginterval) return;

        timer = 0f;
        if (entryNumber >= maximumLogEntries)
        {
            log = string.Empty;
            entryNumber = 0;
        }

        entryNumber++;
        log += $"\n\nNo.{entryNumber}\n{latest}";
        peakDeceleration = 0f;
    }

    string BuildSnapshot()
    {
        if (aircraftController == null || maneuverController == null)
            return $"{name}\nManeuver components are not ready.";

        float speed = maneuverController.CurrentSpeed;
        float speedThreshold = maneuverController.SpeedThreshold;
        float deceleration = maneuverController.CurrentDeceleration;
        float decelerationThreshold = maneuverController.DecelerationThreshold;
        float staminaRatio = pilotStatus != null ? pilotStatus.ShortTermStaminaRatio : 1f;
        float staminaThreshold = pilotStatus != null
            ? pilotStatus.shortTermPenaltyThreshold
            : 0f;
        bool speedTriggered = speed <= speedThreshold;
        bool decelerationTriggered = deceleration >= decelerationThreshold;
        bool staminaTriggered = pilotStatus != null && staminaRatio < staminaThreshold;
        float pitchInputMemory = pilotStatus != null ? pilotStatus.pitchInputMemory : 0f;

        return
            $"{name}  t={Time.time:F2}s\n" +
            $"Priority: {maneuverController.ManeuverPriority}\n" +
            $"Speed: {speed:F2} / <= {speedThreshold:F2} m/s  Trigger={speedTriggered}\n" +
            $"Decel: {deceleration:F2} (peak {peakDeceleration:F2}) / >= {decelerationThreshold:F2} m/s^2  Trigger={decelerationTriggered}\n" +
            $"Stamina: {staminaRatio * 100f:F1}% / < {staminaThreshold * 100f:F1}%  Trigger={staminaTriggered}\n" +
            $"Throttle: {aircraftController.throttle:F2}  PitchMemory: {pitchInputMemory:F3}\n" +
            $"PitchLimit: {aircraftController.pitchPerformance:F2} deg/s  PitchDelta: {aircraftController.pitchDeltaDegrees:F3} deg\n" +
            $"AOA: {aircraftController.AngleOfAttack:F2} deg  TurnDrag: {aircraftController.TurnDragAcceleration:F2} m/s^2\n" +
            $"Position: {transform.position}  Rotation: {transform.eulerAngles}";
    }

    void OnGUI()
    {
        if (showOverlay && !string.IsNullOrEmpty(latest))
            GUI.Box(overlayRect, latest);
    }

    void OnValidate()
    {
        loginterval = Mathf.Max(0.02f, loginterval);
        maximumLogEntries = Mathf.Max(1, maximumLogEntries);
    }
}
