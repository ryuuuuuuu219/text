using UnityEngine;

public sealed class AuxiliaryEquipmentPartStatus : AircraftPartStatus
{
    [Header("Auxiliary Equipment")]
    public string functionId = "Sensor";
    [Min(0f)] public float performance = 1f;
}
