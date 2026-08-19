using UnityEngine;

public enum FuelTankLocation { Fuselage, MainWing }

public sealed class FuelTankPartStatus : AircraftPartStatus
{
    [Header("Fuel Tank")]
    public FuelTankLocation storageLocation = FuelTankLocation.Fuselage;
    [Min(0f)] public float volume = 30f;

    public override float InternalVolume => Mathf.Max(0f, volume);
}
