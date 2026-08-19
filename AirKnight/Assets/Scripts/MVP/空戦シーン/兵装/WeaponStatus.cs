using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AircraftPartStatusConverter))]
public sealed class WeaponStatus : MonoBehaviour
{
    sealed class RuntimeWeaponMount
    {
        public float nextFireTime;
    }

    [Header("Runtime Mount")]
    [SerializeField] Transform muzzle;
    [SerializeField, Min(0f)] float muzzleForwardOffset = 6f;
    [SerializeField, Min(0.001f)] float projectileRadius = 0.04f;

    AircraftFlightAI owner;
    PilotStatus pilotStatus;
    FCS fireControlSystem;
    AircraftPartStatusConverter partStatusConverter;
    readonly List<RuntimeWeaponMount> runtimeWeaponMounts = new();

    public WeaponParameters Parameters => GetPrimaryParameters();
    public AircraftFlightAI CurrentTarget => fireControlSystem != null
        ? fireControlSystem.CurrentTarget
        : null;
    public float CorrectedDamage => Mathf.Max(0f, Parameters.baseDamage) *
        (pilotStatus != null ? Mathf.Max(0f, pilotStatus.proficiencyDamageMultiplier) : 1f);
    public float MaximumRangeDistance => Parameters.GetMaximumRangeDistance();
    public float EffectiveFiringRange => Parameters.GetFiringRangeDistance(
        pilotStatus != null ? pilotStatus.firingRangeRatio : 1f);

    public bool Initswitch=false;

    void switchinit()
    {
        if (Initswitch)
        {
            Initialize(GetComponent<AircraftFlightAI>());
            Initswitch = false;
        }
    }

    void Awake()
    {
        Initialize(GetComponent<AircraftFlightAI>());
    }

    public void Initialize(AircraftFlightAI aircraft)
    {
        owner = aircraft;
        pilotStatus = GetComponent<PilotStatus>();
        fireControlSystem = GetComponent<FCS>();
        partStatusConverter = GetComponent<AircraftPartStatusConverter>();
        EnsureRuntimeWeaponMounts();
    }

    public bool TryFire()
    {
        if (owner == null) return false;
        List<WeaponParameters> managedWeapons = GetManagedWeaponParameters();
        if (managedWeapons == null || managedWeapons.Count == 0) return false;
        EnsureRuntimeWeaponMounts();

        bool fired = false;
        for (int i = 0; i < managedWeapons.Count; i++)
            fired |= TryFire(managedWeapons[i], runtimeWeaponMounts[i]);
        return fired;
    }

    bool TryFire(WeaponParameters activeParameters, RuntimeWeaponMount weaponMount)
    {
        if (activeParameters.shotsPerSecond <= 0f || Time.time < weaponMount.nextFireTime)
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
            weaponMount.nextFireTime = Time.time + 1f / activeParameters.shotsPerSecond;
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
        weaponMount.nextFireTime = Time.time + 1f / activeParameters.shotsPerSecond;
        return true;
    }

    List<WeaponParameters> GetManagedWeaponParameters()
    {
        if (partStatusConverter == null)
            partStatusConverter = GetComponent<AircraftPartStatusConverter>();
        return partStatusConverter != null ? partStatusConverter.WeaponParameters : null;
    }

    WeaponParameters GetPrimaryParameters()
    {
        List<WeaponParameters> managedWeapons = GetManagedWeaponParameters();
        return managedWeapons != null && managedWeapons.Count > 0
            ? managedWeapons[0]
            : WeaponParameters.Create77mmGunPod();
    }

    void EnsureRuntimeWeaponMounts()
    {
        int requiredCount = GetManagedWeaponParameters()?.Count ?? 0;
        while (runtimeWeaponMounts.Count < requiredCount)
            runtimeWeaponMounts.Add(new RuntimeWeaponMount());
        if (runtimeWeaponMounts.Count > requiredCount)
            runtimeWeaponMounts.RemoveRange(
                requiredCount,
                runtimeWeaponMounts.Count - requiredCount);
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

    void OnValidate()
    {
        muzzleForwardOffset = Mathf.Max(0f, muzzleForwardOffset);
        projectileRadius = Mathf.Max(0.001f, projectileRadius);
    }
}
