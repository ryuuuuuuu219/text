using UnityEngine;

public sealed class ControlSurfacePartStatus : AircraftPartStatus
{
    [Header("Roll Control")]
    [Min(0f)] public float rollPerformance = 8f;
}
