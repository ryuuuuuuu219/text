using UnityEngine;

public sealed class ControlSurfacePartStatus : AircraftPartStatus
{
    [Header("Roll Control (deg/s)")]
    [Tooltip("Roll rate added by one control surface, in degrees per second.")]
    [Min(0f)] public float rollPerformance = 133f;
}
