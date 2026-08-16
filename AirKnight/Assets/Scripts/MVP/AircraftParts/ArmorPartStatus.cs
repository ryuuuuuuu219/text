using UnityEngine;

public sealed class ArmorPartStatus : AircraftPartStatus
{
    [Header("Armor")]
    [Min(0f)] public float defenseMultiplier = 1.2f;
}
