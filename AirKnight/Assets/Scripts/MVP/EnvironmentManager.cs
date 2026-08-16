using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnvironmentManager : MonoBehaviour
{
    public static EnvironmentManager Instance { get; private set; }

    [Header("Atmosphere")]
    [Min(0f)] public float airDensity = 1.225f;
    [Min(0f)] public float airViscosityScale = 1f;
    public Vector3 windVelocity;

    [Header("World")]
    public Vector3 gravity = new(0f, -9.81f, 0f);
    public float seaLevel;

    Vector3 previousGravity;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple EnvironmentManagers found; the newer one was disabled.", this);
            enabled = false;
            return;
        }

        Instance = this;
        previousGravity = Physics.gravity;
        ApplyEnvironment();
    }

    void OnValidate()
    {
        airDensity = Mathf.Max(0f, airDensity);
        airViscosityScale = Mathf.Max(0f, airViscosityScale);
        if (Application.isPlaying && Instance == this) ApplyEnvironment();
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        Physics.gravity = previousGravity;
        Instance = null;
    }

    public void ApplyEnvironment()
    {
        if (IsValidVector(gravity)) Physics.gravity = gravity;
        else Debug.LogWarning("Invalid environment gravity was ignored.", this);
    }

    public Vector3 GetWindVelocity(Vector3 worldPosition)
    {
        return IsValidVector(windVelocity) ? windVelocity : Vector3.zero;
    }

    public Vector3 GetRelativeAirVelocity(Vector3 worldPosition, Vector3 worldVelocity)
    {
        return worldVelocity - GetWindVelocity(worldPosition);
    }

    public float GetAltitude(Vector3 worldPosition) => worldPosition.y - seaLevel;

    static bool IsValidVector(Vector3 value)
    {
        return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
