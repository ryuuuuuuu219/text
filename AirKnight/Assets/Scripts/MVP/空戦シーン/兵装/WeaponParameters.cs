using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum WeaponGuidanceMethod
{
    [InspectorName("N/A")]
    None,
    IR,
    SARH,
    ARH
}

public enum WeaponFuzeType
{
    [InspectorName("なし")]
    None,
    Timed,
    Proximity,
    Contact
}

public enum WeaponProjectileVisualType
{
    None,
    Tracer
}

public enum WeaponExhaustVisualType
{
    None,
    Smoke
}

public enum CountermeasureSignatureType
{
    None,
    IR,
    SARH,
    ARH
}

[Serializable]
public struct WeaponSubmunition
{
    public string weaponName;
    [Min(1)] public int number;

    public WeaponSubmunition(string weaponName, int number)
    {
        this.weaponName = weaponName;
        this.number = Mathf.Max(1, number);
    }
}

[Serializable]
public struct WeaponParameters
{
    [Header("Identity")]
    public string weaponName;
    public SupportedWeaponTypes weaponType;
    public CountermeasureSignatureType countermeasureSignatureType;
    [Min(0f)] public float shotsPerSecond;
    [Min(0f)] public float weight;

    [Header("Projectile")]
    [Min(0f)] public float baseDamage;
    [Min(0.01f)] public float muzzleVelocity;
    [Min(0f)] public float dispersionAngle;
    [Min(0.01f)] public float maximumFlightTime;

    [Header("Visual")]
    public WeaponProjectileVisualType projectileVisualType;
    public WeaponExhaustVisualType exhaustVisualType;

    [Header("Propulsion Extension")]
    [Min(0f)] public float thrustAcceleration;
    [Min(0f)] public float poweredDuration;

    [Header("Intermediate Guidance Extension")]
    [Min(0f)] public float guidanceTurnRate;
    [Min(0f)] public float proportionalNavigationConstant;
    public WeaponGuidanceMethod guidanceMethod;
    [Min(0f)] public float seekerAngle;
    [Range(0f, 100f)] public float irDecoyDiversionChance;
    [Min(0f)] public float sarhLookDownResistance;
    [Min(0f)] public float arhCandidateVelocityThreshold;
    [FormerlySerializedAs("arhCountermeasureResistance")]
    [Range(0f, 100f)] public float arhDecoyDiversionChance;

    [Header("Terminal Guidance Extension")]
    public bool hasTerminalGuidance;
    [Min(0f)] public float terminalGuidanceActivationDistance;
    [Min(0f)] public float terminalGuidanceTurnRate;
    [Min(0f)] public float terminalProportionalNavigationConstant;
    public WeaponGuidanceMethod terminalGuidanceMethod;
    [Min(0f)] public float terminalSeekerAngle;
    [Range(0f, 100f)] public float terminalIrDecoyDiversionChance;
    [Min(0f)] public float terminalSarhLookDownResistance;
    [Min(0f)] public float terminalArhCandidateVelocityThreshold;
    [FormerlySerializedAs("terminalArhCountermeasureResistance")]
    [Range(0f, 100f)] public float terminalArhDecoyDiversionChance;

    [Header("Fuze Extension")]
    public WeaponFuzeType fuzeType;
    [Min(0f)] public float fuzeRadius;
    [Min(0f)] public float detonationTime;
    [Min(0f)] public float explosionRadius;

    [Header("Submunitions")]
    public List<WeaponSubmunition> submunitions;

    public static WeaponParameters Create77mmGunPod()
    {
        return new WeaponParameters
        {
            weaponName = "7.7mmガンポッド",
            weaponType = SupportedWeaponTypes.GunPod,
            countermeasureSignatureType = CountermeasureSignatureType.None,
            shotsPerSecond = 6f,
            weight = 80f,
            baseDamage = 4f,
            muzzleVelocity = 330f,
            dispersionAngle = 2.5f,
            maximumFlightTime = 4.2f,
            projectileVisualType = WeaponProjectileVisualType.Tracer,
            exhaustVisualType = WeaponExhaustVisualType.None,
            thrustAcceleration = 0f,
            poweredDuration = 0f,
            guidanceTurnRate = 0f,
            guidanceMethod = WeaponGuidanceMethod.None,
            seekerAngle = 0f,
            irDecoyDiversionChance = 0f,
            sarhLookDownResistance = 0f,
            arhCandidateVelocityThreshold = 0f,
            arhDecoyDiversionChance = 0f,
            fuzeType = WeaponFuzeType.None,
            fuzeRadius = 0f,
            detonationTime = 0f,
            explosionRadius = 0f
        };
    }

    public static WeaponParameters CreateSTDIRM()
    {
        return new WeaponParameters
        {
            weaponName = "STDIRM",
            weaponType = SupportedWeaponTypes.Missile,
            countermeasureSignatureType = CountermeasureSignatureType.None,
            shotsPerSecond = 0.3f,
            weight = 150f,
            baseDamage = 20f,
            muzzleVelocity = 100f,
            dispersionAngle = 0.5f,
            maximumFlightTime = 8f,
            projectileVisualType = WeaponProjectileVisualType.Tracer,
            exhaustVisualType = WeaponExhaustVisualType.Smoke,
            thrustAcceleration = 25f,
            poweredDuration = 5f,
            guidanceTurnRate = 12f,
            proportionalNavigationConstant = 3f,
            guidanceMethod = WeaponGuidanceMethod.IR,
            seekerAngle = 35f,
            irDecoyDiversionChance = 0.03f,
            sarhLookDownResistance = 0f,
            arhCandidateVelocityThreshold = 0f,
            arhDecoyDiversionChance = 0f,
            fuzeType = WeaponFuzeType.Proximity,
            fuzeRadius = 1f,
            detonationTime = 0f,
            explosionRadius = 1.2f
        };
    }

    public static WeaponParameters CreateIRCM()
    {
        return new WeaponParameters
        {
            weaponName = "IRCM-A",
            weaponType = SupportedWeaponTypes.Missile,
            countermeasureSignatureType = CountermeasureSignatureType.IR,
            shotsPerSecond = 2f,
            weight = 30f,
            baseDamage = 0f,
            muzzleVelocity = 0.1f,
            dispersionAngle = 0.5f,
            maximumFlightTime = 2f,
            projectileVisualType = WeaponProjectileVisualType.Tracer,
            exhaustVisualType = WeaponExhaustVisualType.Smoke,
            thrustAcceleration = 0.1f,
            poweredDuration = 1f,
            guidanceTurnRate = 0f,
            guidanceMethod = WeaponGuidanceMethod.IR,
            seekerAngle = 0f,
            irDecoyDiversionChance = 0f,
            sarhLookDownResistance = 0f,
            arhCandidateVelocityThreshold = 0f,
            arhDecoyDiversionChance = 0f,
            fuzeType = WeaponFuzeType.None,
            fuzeRadius = 0f,
            detonationTime = 0f,
            explosionRadius = 0f
        };
    }

    public static WeaponParameters Create127mmGunPod()
    {
        WeaponParameters parameters = Create77mmGunPod();
        parameters.weaponName = "12.7mmガンポッド";
        parameters.shotsPerSecond = 12f;
        parameters.baseDamage = 5f;
        parameters.muzzleVelocity = 540f;
        parameters.dispersionAngle = 2.8f;
        parameters.maximumFlightTime = 5.4f;
        return parameters;
    }

    public static WeaponParameters CreateCommonShell()
    {
        WeaponParameters parameters = Create77mmGunPod();
        parameters.weaponName = "共通弾殻";
        parameters.weaponType = SupportedWeaponTypes.Rocket;
        parameters.shotsPerSecond = 0f;
        parameters.weight = 0f;
        parameters.baseDamage = 5f;
        parameters.muzzleVelocity = 500f;
        parameters.dispersionAngle = 0f;
        parameters.maximumFlightTime = 0.4f;
        return parameters;
    }

    public static WeaponParameters Create32mmRocket()
    {
        return new WeaponParameters
        {
            weaponName = "32mmロケット",
            weaponType = SupportedWeaponTypes.Rocket,
            shotsPerSecond = 0.3f,
            weight = 80f,
            baseDamage = 50f,
            muzzleVelocity = 15f,
            maximumFlightTime = 5f,
            projectileVisualType = WeaponProjectileVisualType.Tracer,
            exhaustVisualType = WeaponExhaustVisualType.Smoke,
            thrustAcceleration = 25f,
            poweredDuration = 2f,
            guidanceMethod = WeaponGuidanceMethod.None,
            fuzeType = WeaponFuzeType.Timed,
            detonationTime = 5f,
            explosionRadius = 3f,
            submunitions = new List<WeaponSubmunition>
            {
                new("共通弾殻", 15)
            }
        };
    }

    public static WeaponParameters Create45mmFRocket()
    {
        WeaponParameters parameters = Create32mmRocket();
        parameters.weaponName = "45mmFロケット";
        parameters.baseDamage = 70f;
        parameters.submunitions = new List<WeaponSubmunition>
        {
            new("共通弾殻", 36)
        };
        return parameters;
    }

    public static WeaponParameters Create250FRocket()
    {
        return new WeaponParameters
        {
            weaponName = "250(6*45mm)Fロケット",
            weaponType = SupportedWeaponTypes.Rocket,
            shotsPerSecond = 0.26f,
            weight = 220f,
            baseDamage = 700f,
            muzzleVelocity = 8f,
            maximumFlightTime = 5f,
            projectileVisualType = WeaponProjectileVisualType.Tracer,
            exhaustVisualType = WeaponExhaustVisualType.Smoke,
            thrustAcceleration = 65f,
            poweredDuration = 0.8f,
            guidanceMethod = WeaponGuidanceMethod.None,
            fuzeType = WeaponFuzeType.Timed,
            detonationTime = 5f,
            explosionRadius = 18f,
            submunitions = new List<WeaponSubmunition>
            {
                new("45mmFロケット", 6),
                new("共通弾殻", 50)
            }
        };
    }

    public static WeaponParameters CreateQIRM()
    {
        WeaponParameters parameters = CreateSTDIRM();
        parameters.weaponName = "QIRM";
        parameters.weight = 80f;
        parameters.guidanceTurnRate = 110f;
        return parameters;
    }

    public static WeaponParameters CreateQIRM2()
    {
        WeaponParameters parameters = CreateQIRM();
        parameters.weaponName = "QIRM-2";
        parameters.baseDamage = 25f;
        parameters.seekerAngle = 25f;
        parameters.irDecoyDiversionChance = 0.024f;
        return parameters;
    }

    public static WeaponParameters CreateLRQIRM2()
    {
        WeaponParameters parameters = CreateQIRM2();
        parameters.weaponName = "LR-QIRM-2";
        parameters.shotsPerSecond = 0.1f;
        parameters.baseDamage = 250f;
        parameters.muzzleVelocity = 150f;
        parameters.maximumFlightTime = 30f;
        parameters.thrustAcceleration = 25f;
        parameters.poweredDuration = 15f;
        parameters.guidanceTurnRate = 20f;
        parameters.seekerAngle = 5f;
        parameters.irDecoyDiversionChance = 0.007f;
        parameters.fuzeRadius = 12f;
        parameters.explosionRadius = 15f;
        return parameters;
    }

    public static WeaponParameters CreateSarhArhm2Eccm()
    {
        WeaponParameters parameters = CreateLRQIRM2();
        parameters.weaponName = "SARH-ARHM-2(ECCM)";
        parameters.guidanceTurnRate = 12f;
        parameters.guidanceMethod = WeaponGuidanceMethod.SARH;
        parameters.seekerAngle = 0f;
        parameters.irDecoyDiversionChance = 0f;
        parameters.sarhLookDownResistance = 50f;
        parameters.hasTerminalGuidance = true;
        parameters.terminalGuidanceActivationDistance = 300f;
        parameters.terminalGuidanceTurnRate = 8f;
        parameters.terminalProportionalNavigationConstant = 3f;
        parameters.terminalGuidanceMethod = WeaponGuidanceMethod.ARH;
        parameters.terminalSeekerAngle = 0f;
        parameters.terminalArhCandidateVelocityThreshold = 15f;
        parameters.terminalArhDecoyDiversionChance = 0.02f;
        return parameters;
    }

    public static bool TryCreate(string name, out WeaponParameters parameters)
    {
        switch (name)
        {
            case "7.7mmガンポッド":
                parameters = Create77mmGunPod();
                return true;
            case "STDIRM":
                parameters = CreateSTDIRM();
                return true;
            case "IRCM":
            case "IRCM-A":
                parameters = CreateIRCM();
                return true;
            case "12.7mmガンポッド":
                parameters = Create127mmGunPod();
                return true;
            case "32mmロケット":
                parameters = Create32mmRocket();
                return true;
            case "共通弾殻":
                parameters = CreateCommonShell();
                return true;
            case "250(6*45mm)Fロケット":
            case "250(6×45mm)Fロケット":
            case "250(3*45mm)Fロケット":
            case "250(3×45mm)Fロケット":
                parameters = Create250FRocket();
                return true;
            case "45mmFロケット":
            case "45mm)Fロケット":
                parameters = Create45mmFRocket();
                return true;
            case "QIRM":
                parameters = CreateQIRM();
                return true;
            case "QIRM-2":
                parameters = CreateQIRM2();
                return true;
            case "LR-QIRM-2":
                parameters = CreateLRQIRM2();
                return true;
            case "SARH-ARHM-2(ECCM)":
                parameters = CreateSarhArhm2Eccm();
                return true;
            default:
                parameters = default;
                return false;
        }
    }

    public string returnstatus(string name)
    {
        if (!string.Equals(name, "7.7mmガンポッド", StringComparison.Ordinal))
            return string.Empty;

        return
            "7.7mmガンポッド\n" +
            "秒間発射速度　6\n" +
            "重量　80\n" +
            "威力（パイロットステータス補正前）　4\n" +
            "初速　330\n" +
            "散布界　2.5deg\n" +
            "射程　4.2s\n" +
            "拡張\n" +
            "推力　0m/s^2\n" +
            "噴進時間　0s\n" +
            "誘導性能　0deg/s\n" +
            "誘導方式　N/A（IR/SARH/ARH）\n" +
            "誘導シーカー　0deg\n" +
            "IR欺瞞耐性（別目標誘引確率）　0%\n" +
            "SARHルックダウン耐性　-0m\n" +
            "ARH目標候補化閾値　+/-0m/s\n" +
            "ARHデコイ誘引確率　0%\n" +
            "信管種別　なし（なし/時限/近接/接触）\n" +
            "信管半径　0m\n" +
            "炸裂時間　N/A\n" +
            "爆発半径　0m";
    }

    public float GetMaximumRangeDistance()
    {
        float flightTime = Mathf.Max(0f, maximumFlightTime);
        float propulsionTime = Mathf.Max(0f, poweredDuration);
        float terminalSpeed = Mathf.Max(0f, muzzleVelocity) +
                              Mathf.Max(0f, thrustAcceleration) * propulsionTime;
        return terminalSpeed * flightTime;
    }

    public float GetFiringRangeDistance(float firingRangeRatio)
    {
        return GetMaximumRangeDistance() * Mathf.Clamp01(firingRangeRatio);
    }

    public void Clamp()
    {
        shotsPerSecond = Mathf.Max(0f, shotsPerSecond);
        weight = Mathf.Max(0f, weight);
        baseDamage = Mathf.Max(0f, baseDamage);
        muzzleVelocity = Mathf.Max(0.01f, muzzleVelocity);
        dispersionAngle = Mathf.Max(0f, dispersionAngle);
        maximumFlightTime = Mathf.Max(0.01f, maximumFlightTime);
        thrustAcceleration = Mathf.Max(0f, thrustAcceleration);
        poweredDuration = Mathf.Max(0f, poweredDuration);
        guidanceTurnRate = Mathf.Max(0f, guidanceTurnRate);
        proportionalNavigationConstant = Mathf.Max(0f, proportionalNavigationConstant);
        seekerAngle = Mathf.Max(0f, seekerAngle);
        irDecoyDiversionChance = Mathf.Clamp(irDecoyDiversionChance, 0f, 100f);
        sarhLookDownResistance = Mathf.Max(0f, sarhLookDownResistance);
        arhCandidateVelocityThreshold = Mathf.Max(0f, arhCandidateVelocityThreshold);
        arhDecoyDiversionChance = Mathf.Clamp(arhDecoyDiversionChance, 0f, 100f);
        terminalGuidanceActivationDistance = Mathf.Max(0f, terminalGuidanceActivationDistance);
        terminalGuidanceTurnRate = Mathf.Max(0f, terminalGuidanceTurnRate);
        terminalProportionalNavigationConstant = Mathf.Max(
            0f,
            terminalProportionalNavigationConstant);
        terminalSeekerAngle = Mathf.Max(0f, terminalSeekerAngle);
        terminalIrDecoyDiversionChance = Mathf.Clamp(terminalIrDecoyDiversionChance, 0f, 100f);
        terminalSarhLookDownResistance = Mathf.Max(0f, terminalSarhLookDownResistance);
        terminalArhCandidateVelocityThreshold = Mathf.Max(0f, terminalArhCandidateVelocityThreshold);
        terminalArhDecoyDiversionChance = Mathf.Clamp(
            terminalArhDecoyDiversionChance,
            0f,
            100f);
        fuzeRadius = Mathf.Max(0f, fuzeRadius);
        detonationTime = Mathf.Max(0f, detonationTime);
        explosionRadius = Mathf.Max(0f, explosionRadius);
        if (submunitions == null) return;
        for (int i = 0; i < submunitions.Count; i++)
        {
            WeaponSubmunition entry = submunitions[i];
            entry.number = Mathf.Max(1, entry.number);
            submunitions[i] = entry;
        }
    }
}
