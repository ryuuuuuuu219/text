using UnityEngine;

public sealed class ControlSurfacePartStatus : AircraftPartStatus
{
    [Header("Pitch Performance Curve Inputs")]
    [Min(0f)] public float lowSpeedPerformance = 8f;
    [Min(0f)] public float maximumPerformance = 12f;
    [Min(0.01f)] public float optimalSpeed = 30f;
    [Min(0.02f)] public float controlLimitSpeed = 75f;
    [Min(0f)] public float rollPerformance = 8f;
}
