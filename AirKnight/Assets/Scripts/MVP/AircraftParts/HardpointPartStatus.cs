using System;
using UnityEngine;

[Flags]
public enum SupportedWeaponTypes
{
    None = 0,
    Bomb = 1 << 0,
    Rocket = 1 << 1,
    GunPod = 1 << 2,
    Missile = 1 << 3
}

public sealed class HardpointPartStatus : AircraftPartStatus
{
    [Header("Hardpoint")]
    public SupportedWeaponTypes supportedWeapons = SupportedWeaponTypes.Missile;
    [Min(0f)] public float maximumWeaponWeight = 250f;
}
