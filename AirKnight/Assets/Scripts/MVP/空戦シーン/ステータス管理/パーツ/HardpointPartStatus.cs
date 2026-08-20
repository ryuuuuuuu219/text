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
    Missile = 1 << 3,
    CM = 1 << 4
}

public sealed class HardpointPartStatus : AircraftPartStatus
{
    [Header("Hardpoint")]
    [Min(1)] public int hardpointCount = 4;
    public List<SupportedWeaponTypes> supportedWeapons = new()
    {
        SupportedWeaponTypes.GunPod,
        SupportedWeaponTypes.GunPod,
        SupportedWeaponTypes.Missile,
        SupportedWeaponTypes.CM
    };
    [Min(0f)] public float maximumWeaponWeight = 250f;
    public List<string> equipweapon = new()
    {
        "7.7mmガンポッド",
        "7.7mmガンポッド",
        "STDIRM",
        "IRCM-A"
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
        supportedWeapons ??= new List<SupportedWeaponTypes>();
        while (supportedWeapons.Count < Count)
            supportedWeapons.Add(GetDefaultSupportedWeaponType(supportedWeapons.Count));
        if (supportedWeapons.Count > Count)
            supportedWeapons.RemoveRange(Count, supportedWeapons.Count - Count);

        equipweapon ??= new List<string>();
        while (equipweapon.Count < Count)
            equipweapon.Add(GetDefaultWeaponName(supportedWeapons[equipweapon.Count]));
        if (equipweapon.Count > Count)
            equipweapon.RemoveRange(Count, equipweapon.Count - Count);
    }

    static SupportedWeaponTypes GetDefaultSupportedWeaponType(int slotIndex)
    {
        return slotIndex switch
        {
            0 or 1 => SupportedWeaponTypes.GunPod,
            2 => SupportedWeaponTypes.Missile,
            3 => SupportedWeaponTypes.CM,
            _ => SupportedWeaponTypes.Missile
        };
    }

    static string GetDefaultWeaponName(SupportedWeaponTypes supportedWeaponType)
    {
        if ((supportedWeaponType & SupportedWeaponTypes.GunPod) != 0)
            return "7.7mmガンポッド";
        if ((supportedWeaponType & SupportedWeaponTypes.Rocket) != 0)
            return "32mmロケット";
        if ((supportedWeaponType & SupportedWeaponTypes.CM) != 0)
            return "IRCM-A";
        return "STDIRM";
    }
}
