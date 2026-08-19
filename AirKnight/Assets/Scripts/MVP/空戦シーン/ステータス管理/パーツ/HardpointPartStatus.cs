using System;
using System.Collections.Generic;
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
    const string DefaultWeaponName = "STDIRM";

    [Header("Hardpoint")]
    [Min(1)] public int hardpointCount = 4;
    public SupportedWeaponTypes supportedWeapons = SupportedWeaponTypes.Missile;
    [Min(0f)] public float maximumWeaponWeight = 250f;
    public List<string> equipweapon = new()
    {
        "STDIRM",
        "STDIRM",
        "IRCM",
        "IRCM"
    };

    int Count => Mathf.Max(1, hardpointCount);
    public override float TotalWeight => base.TotalWeight * Count;
    public override float TotalHitPoints => base.TotalHitPoints * Count;
    public float TotalMaximumWeaponWeight => Mathf.Max(0f, maximumWeaponWeight) * Count;

    void Awake()
    {
        SynchronizeWeaponSlots();
    }

    void OnValidate()
    {
        hardpointCount = Mathf.Max(1, hardpointCount);
        maximumWeaponWeight = Mathf.Max(0f, maximumWeaponWeight);
        SynchronizeWeaponSlots();
    }

    void SynchronizeWeaponSlots()
    {
        equipweapon ??= new List<string>();
        while (equipweapon.Count < Count)
            equipweapon.Add(DefaultWeaponName);
        if (equipweapon.Count > Count)
            equipweapon.RemoveRange(Count, equipweapon.Count - Count);
    }
}
