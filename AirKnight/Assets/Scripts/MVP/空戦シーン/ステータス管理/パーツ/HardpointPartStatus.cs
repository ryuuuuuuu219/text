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
    [Min(1)] public int hardpointCount = 1;
    public SupportedWeaponTypes supportedWeapons = SupportedWeaponTypes.Missile;
    [Min(0f)] public float maximumWeaponWeight = 250f;

    int Count => Mathf.Max(1, hardpointCount);
    public override float TotalWeight => base.TotalWeight * Count;
    public override float TotalHitPoints => base.TotalHitPoints * Count;
    public float TotalMaximumWeaponWeight => Mathf.Max(0f, maximumWeaponWeight) * Count;
}
