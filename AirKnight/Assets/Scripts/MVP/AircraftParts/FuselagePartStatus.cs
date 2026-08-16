using UnityEngine;

public enum FuselageShape { Cylinder, Box }

public sealed class FuselagePartStatus : AircraftPartStatus
{
    [Header("Fuselage")]
    public FuselageShape shape = FuselageShape.Cylinder;
    [Min(0.01f)] public float width = 2.5f;
    [Min(0.01f)] public float height = 1.5f;
    [Min(0.01f)] public float length = 10f;
    [Min(0f)] public float hitPointCoefficient = 1f;
    [Min(0f)] public float volumeEfficiency = 0.7f;

    public override float ForwardProjectedArea => (shape == FuselageShape.Cylinder
        ? Mathf.PI * Mathf.Pow(Mathf.Max(width, height) * 0.5f, 2f)
        : Mathf.Max(0f, width * height)) * Mathf.Max(1, quantity);

    public override float BroadsideArea => Mathf.Max(0f, length * Mathf.Max(width, height) * Mathf.Max(1, quantity));

    public override float InternalVolume => shape == FuselageShape.Cylinder
        ? Mathf.PI * Mathf.Pow(Mathf.Max(width, height) * 0.5f, 2f) * length * volumeEfficiency * Mathf.Max(1, quantity)
        : width * height * length * volumeEfficiency * Mathf.Max(1, quantity);
}
