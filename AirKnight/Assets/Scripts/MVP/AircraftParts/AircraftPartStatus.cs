using UnityEngine;

public abstract class AircraftPartStatus : MonoBehaviour
{
    [Header("Common Part Status")]
    public string partName = "Part";
    [Min(1)] public int quantity = 1;
    [Min(0f)] public float weight;
    [Min(0f)] public float hitPoints;
    [Min(0.01f)] public float safetyFactor = 1f;
    [Min(0f)] public float materialQuality = 1f;
    [Min(0f)] public float jointQuality = 1f;

    public float TotalWeight => Mathf.Max(1, quantity) * Mathf.Max(0f, weight);
    public float TotalHitPoints => Mathf.Max(1, quantity) * Mathf.Max(0f, hitPoints);
    public float EffectiveSafetyFactor => Mathf.Max(0.01f, safetyFactor * Mathf.Min(materialQuality, jointQuality));
    public virtual float ForwardProjectedArea => 0f;
    public virtual float BroadsideArea => 0f;
    public virtual float InternalVolume => 0f;
}
