using UnityEngine;

public enum AuxiliaryWingType { Tail, Canard }

public sealed class AuxiliaryWingPartStatus : AircraftPartStatus
{
    [Header("Auxiliary Wing")]
    public AuxiliaryWingType wingType = AuxiliaryWingType.Tail;
    [Min(0.01f)] public float width = 2f;
    [Min(0.01f)] public float height = 0.15f;
    [Min(0.01f)] public float length = 1.5f;
    [Range(-90f, 90f)] public float degree;
    public bool contributesToEffectiveWingArea = true;
    [Min(0f)] public float rollPerformanceMultiplier = 1f;

    public float WingArea => Mathf.Max(0f, width * length * Mathf.Max(1, quantity));
    public override float BroadsideArea => WingArea;
    public override float ForwardProjectedArea
    {
        get
        {
            float radians = degree * Mathf.Deg2Rad;
            float projectedDepth = Mathf.Abs(height * Mathf.Cos(radians))
                + Mathf.Abs(length * Mathf.Sin(radians));
            return Mathf.Max(0f, width * projectedDepth * Mathf.Max(1, quantity));
        }
    }
}
