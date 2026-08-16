using UnityEngine;

public sealed class FlapPartStatus : AircraftPartStatus
{
    [Header("Flap")]
    public bool deployed;
    [Min(0f)] public float deployedStallSpeedMultiplier = 0.85f;
    [Min(0f)] public float deployedTurnPerformanceMultiplier = 1.15f;
}
