using UnityEngine;

public enum EngineMountPosition { FuselageFront, FuselageRear, MainWing }

public sealed class EnginePartStatus : AircraftPartStatus
{
    [Header("Engine")]
    public EngineMountPosition mountPosition = EngineMountPosition.FuselageFront;
    [Min(0f)] public float thrust = 10000f;
    [Min(1)] public int propellerCount = 1;
    [Min(0f)] public float engineSpacing;
    [Range(0f, 1f)] public float placementPrecision = 1f;

    public float TotalThrust => Mathf.Max(0f, thrust) * Mathf.Max(1, quantity);
}
