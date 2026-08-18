using UnityEngine;

public enum MainWingType { Conventional, Delta, Tailless }

public sealed class MainWingPartStatus : AircraftPartStatus
{
    [Header("Main Wing")]
    public MainWingType wingType = MainWingType.Conventional;
    [Min(0.01f)] public float width = 5f;
    [Min(0.01f)] public float height = 0.2f;
    [Min(0.01f)] public float length = 2f;
    [Range(-90f, 90f)] public float degree;
    [Min(0f)] public float volumeEfficiency = 0.35f;
    [Min(0)] public int hardpointCount = 1;
    [Min(0f)] public float maximumHardpointWeight = 250f;

    [Header("Pitch Turn Rate Curve (deg/s by m/s)")]
    [Tooltip("Pitch rate at 0 m/s, in degrees per second.")]
    [Min(0f)] public float lowSpeedPerformance = 30f;
    [Tooltip("Maximum pitch rate in degrees per second.")]
    [Min(0f)] public float maximumPerformance = 45f;
    [Tooltip("Aircraft speed in m/s at which maximum pitch rate is reached.")]
    [Min(0.01f)] public float optimalSpeed = 50f;
    [Tooltip("Aircraft speed in m/s at which pitch control reaches 0 deg/s.")]
    [Min(0.02f)] public float controlLimitSpeed = 100f;

    public float WingArea => Mathf.Max(0f, width * length);
    public override float BroadsideArea => WingArea;
    public override float ForwardProjectedArea
    {
        get
        {
            float radians = degree * Mathf.Deg2Rad;
            float projectedDepth = Mathf.Abs(height * Mathf.Cos(radians))
                + Mathf.Abs(length * Mathf.Sin(radians));
            return Mathf.Max(0f, width * projectedDepth);
        }
    }
    public override float InternalVolume => width * height * length * volumeEfficiency;
}
