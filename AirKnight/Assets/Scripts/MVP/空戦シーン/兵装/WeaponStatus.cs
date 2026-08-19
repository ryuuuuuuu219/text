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
    Rigidbody ownerBody;
    FCS fireControlSystem;

    public WeaponParameters Parameters => parameters;
    public float CorrectedDamage => Mathf.Max(0f, parameters.baseDamage) *
        (pilotStatus != null ? Mathf.Max(0f, pilotStatus.proficiencyDamageMultiplier) : 1f);

    void Awake()
    {
        Initialize(GetComponent<AircraftFlightAI>());
    }

    public void Initialize(AircraftFlightAI aircraft)
    {
        owner = aircraft;
        pilotStatus = GetComponent<PilotStatus>();
        ownerBody = GetComponent<Rigidbody>();
        fireControlSystem = GetComponent<FCS>();
    }

    public bool TryFire()
    {
        if (owner == null || fireControlSystem == null) return false;
        if (!fireControlSystem.TryGetShotDirection(
                parameters.muzzleVelocity,
                parameters.dispersionAngle,
                out Vector3 shotDirection))
            return false;

        Vector3 spawnPosition = muzzle != null
            ? muzzle.position
            : transform.position + transform.forward * muzzleForwardOffset;
        Vector3 inheritedVelocity = ownerBody != null ? ownerBody.linearVelocity : Vector3.zero;
        Vector3 initialVelocity = inheritedVelocity + shotDirection * parameters.muzzleVelocity;
        WeaponProjectile.Spawn(
            this,
            owner,
            spawnPosition,
            initialVelocity,
            CorrectedDamage,
            parameters.maximumFlightTime,
            projectileRadius);
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
