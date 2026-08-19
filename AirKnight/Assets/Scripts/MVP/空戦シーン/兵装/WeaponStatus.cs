using UnityEngine;

[DisallowMultipleComponent]
public sealed class WeaponStatus : MonoBehaviour
{
    [SerializeField] WeaponParameters parameters = WeaponParameters.Create77mmGunPod();

    [Header("Runtime Mount")]
    [SerializeField] Transform muzzle;
    [SerializeField, Min(0f)] float muzzleForwardOffset = 6f;
    [SerializeField, Min(0.001f)] float projectileRadius = 0.04f;

    AircraftFlightAI owner;
    PilotStatus pilotStatus;
    FCS fireControlSystem;
    float nextFireTime;

    public WeaponParameters Parameters => parameters;
    public float CorrectedDamage => Mathf.Max(0f, parameters.baseDamage) *
        (pilotStatus != null ? Mathf.Max(0f, pilotStatus.proficiencyDamageMultiplier) : 1f);
    public float MaximumRangeDistance => parameters.GetMaximumRangeDistance();
    public float EffectiveFiringRange => parameters.GetFiringRangeDistance(
        pilotStatus != null ? pilotStatus.firingRangeRatio : 1f);

    void Awake()
    {
        Initialize(GetComponent<AircraftFlightAI>());
    }

    public void Initialize(AircraftFlightAI aircraft)
    {
        owner = aircraft;
        pilotStatus = GetComponent<PilotStatus>();
        fireControlSystem = GetComponent<FCS>();
    }

    public bool TryFire()
    {
        if (owner == null || fireControlSystem == null) return false;
        if (parameters.shotsPerSecond <= 0f || Time.time < nextFireTime) return false;
        AircraftFlightAI target = fireControlSystem.CurrentTarget;
        if (target == null ||
            Vector3.Distance(transform.position, target.transform.position) > EffectiveFiringRange)
            return false;
        if (!fireControlSystem.TryGetShotDirection(
                parameters.muzzleVelocity,
                parameters.dispersionAngle,
                out Vector3 shotDirection))
            return false;

        Vector3 spawnPosition = muzzle != null
            ? muzzle.position
            : transform.position + transform.forward * muzzleForwardOffset;
        Vector3 initialVelocity = shotDirection * parameters.muzzleVelocity;
        WeaponProjectile.Spawn(
            this,
            owner,
            spawnPosition,
            initialVelocity,
            CorrectedDamage,
            parameters.maximumFlightTime,
            projectileRadius);
        nextFireTime = Time.time + 1f / parameters.shotsPerSecond;
        return true;
    }

    void Reset()
    {
        parameters = WeaponParameters.Create77mmGunPod();
    }

    void OnValidate()
    {
        parameters.Clamp();
        muzzleForwardOffset = Mathf.Max(0f, muzzleForwardOffset);
        projectileRadius = Mathf.Max(0.001f, projectileRadius);
    }
}
