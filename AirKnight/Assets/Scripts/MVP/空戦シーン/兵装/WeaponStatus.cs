using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WeaponStatus : MonoBehaviour
{
    sealed class RuntimeWeaponGroup
    {
        public WeaponParameters parameters;
        public int mountCount;
        public float nextFireTime;
    }

    [SerializeField] WeaponParameters parameters = WeaponParameters.Create77mmGunPod();

    [Header("Runtime Mount")]
    [SerializeField] Transform muzzle;
    [SerializeField, Min(0f)] float muzzleForwardOffset = 6f;
    [SerializeField, Min(0.001f)] float projectileRadius = 0.04f;

    AircraftFlightAI owner;
    PilotStatus pilotStatus;
    FCS fireControlSystem;
    readonly List<RuntimeWeaponGroup> runtimeWeapons = new();

    public WeaponParameters Parameters => parameters;
    public AircraftFlightAI CurrentTarget => fireControlSystem != null
        ? fireControlSystem.CurrentTarget
        : null;
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
        RebuildRuntimeWeapons();
    }

    public bool TryFire()
    {
        if (owner == null) return false;
        bool fired = false;
        for (int i = 0; i < runtimeWeapons.Count; i++)
            fired |= TryFire(runtimeWeapons[i]);
        return fired;
    }

    bool TryFire(RuntimeWeaponGroup weapon)
    {
        WeaponParameters activeParameters = weapon.parameters;
        if (activeParameters.shotsPerSecond <= 0f || Time.time < weapon.nextFireTime)
            return false;

        if (activeParameters.countermeasureSignatureType != CountermeasureSignatureType.None)
        {
            Vector3 launchDirection = -transform.forward;
            Vector3 countermeasureSpawnPosition = muzzle != null
                ? muzzle.position
                : transform.position + launchDirection * muzzleForwardOffset;
            CountermeasureSignature.Spawn(
                activeParameters,
                countermeasureSpawnPosition,
                launchDirection);
            weapon.nextFireTime = Time.time + 1f / activeParameters.shotsPerSecond;
            return true;
        }

        if (fireControlSystem == null) return false;
        AircraftFlightAI target = fireControlSystem.CurrentTarget;
        if (target == null ||
            Vector3.Distance(transform.position, target.transform.position) >
            GetEffectiveFiringRange(activeParameters))
            return false;
        if (!fireControlSystem.TryGetShotDirection(
                activeParameters.muzzleVelocity,
                activeParameters.dispersionAngle,
                out Vector3 shotDirection))
            return false;

        Vector3 spawnPosition = muzzle != null
            ? muzzle.position
            : transform.position + transform.forward * muzzleForwardOffset;
        Vector3 initialVelocity = shotDirection * activeParameters.muzzleVelocity;
        WeaponProjectile.Spawn(
            activeParameters,
            owner,
            target.transform,
            spawnPosition,
            initialVelocity,
            GetCorrectedDamage(activeParameters),
            activeParameters.maximumFlightTime,
            projectileRadius);
        weapon.nextFireTime = Time.time + 1f / activeParameters.shotsPerSecond;
        return true;
    }

    void RebuildRuntimeWeapons()
    {
        runtimeWeapons.Clear();
        AddRuntimeWeapon(parameters);

        HardpointPartStatus hardpoint = GetComponent<HardpointPartStatus>();
        if (hardpoint == null || hardpoint.equipweapon == null) return;
        for (int i = 0; i < hardpoint.equipweapon.Count; i++)
        {
            string weaponName = hardpoint.equipweapon[i];
            if (WeaponParameters.TryCreate(weaponName, out WeaponParameters equippedParameters))
                AddRuntimeWeapon(equippedParameters);
            else if (!string.IsNullOrWhiteSpace(weaponName))
                Debug.LogWarning($"Unsupported equipped weapon: {weaponName}", this);
        }
    }

    void AddRuntimeWeapon(WeaponParameters weaponParameters)
    {
        for (int i = 0; i < runtimeWeapons.Count; i++)
        {
            if (!string.Equals(
                    runtimeWeapons[i].parameters.weaponName,
                    weaponParameters.weaponName,
                    StringComparison.Ordinal))
                continue;
            runtimeWeapons[i].mountCount++;
            return;
        }

        runtimeWeapons.Add(new RuntimeWeaponGroup
        {
            parameters = weaponParameters,
            mountCount = 1
        });
    }

    float GetCorrectedDamage(WeaponParameters weaponParameters)
    {
        return Mathf.Max(0f, weaponParameters.baseDamage) *
               (pilotStatus != null
                   ? Mathf.Max(0f, pilotStatus.proficiencyDamageMultiplier)
                   : 1f);
    }

    float GetEffectiveFiringRange(WeaponParameters weaponParameters)
    {
        return weaponParameters.GetFiringRangeDistance(
            pilotStatus != null ? pilotStatus.firingRangeRatio : 1f);
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
