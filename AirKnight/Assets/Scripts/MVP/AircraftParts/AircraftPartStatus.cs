using UnityEngine;

public abstract class AircraftPartStatus : MonoBehaviour
{
    [Header("Common Part Status")]
    public string partName = "Part";
    [Min(0f)] public float weight;
    [Min(0f)] public float hitPoints;
    [Min(0.01f)] public float safetyFactor = 1f;

    public virtual float TotalWeight => Mathf.Max(0f, weight);
    public virtual float TotalHitPoints => Mathf.Max(0f, hitPoints);
    public float EffectiveSafetyFactor => Mathf.Max(0.01f, safetyFactor);
    public virtual float ForwardProjectedArea => 0f;
    public virtual float BroadsideArea => 0f;
    public virtual float InternalVolume => 0f;
}
