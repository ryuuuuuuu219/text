using System;
using UnityEngine;

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

[Serializable]
public struct WeaponParameters
{
    [Header("Identity")]
    public string weaponName;
    public SupportedWeaponTypes weaponType;
    [Min(0f)] public float shotsPerSecond;
    [Min(0f)] public float weight;

    [Header("Projectile")]
    [Min(0f)] public float baseDamage;
    [Min(0.01f)] public float muzzleVelocity;
    [Min(0f)] public float dispersionAngle;
    [Min(0.01f)] public float maximumFlightTime;

    [Header("Propulsion Extension")]
    [Min(0f)] public float thrustAcceleration;
    [Min(0f)] public float poweredDuration;

    [Header("Guidance Extension")]
    [Min(0f)] public float guidanceTurnRate;
    public WeaponGuidanceMethod guidanceMethod;
    [Min(0f)] public float seekerAngle;
    [Range(0f, 1f)] public float irDecoyDiversionChance;
    [Min(0f)] public float sarhLookDownResistance;
    [Min(0f)] public float arhCandidateVelocityThreshold;
    [Range(0f, 1f)] public float arhCountermeasureResistance;

    [Header("Fuze Extension")]
    public WeaponFuzeType fuzeType;
    [Min(0f)] public float fuzeRadius;
    [Min(0f)] public float detonationTime;
    [Min(0f)] public float explosionRadius;

    public static WeaponParameters Create77mmGunPod()
    {
        return new WeaponParameters
        {
            weaponName = "7.7mmガンポッド",
            weaponType = SupportedWeaponTypes.GunPod,
            shotsPerSecond = 6f,
            weight = 80f,
            baseDamage = 4f,
            muzzleVelocity = 110f,
            dispersionAngle = 2.5f,
            maximumFlightTime = 1.4f,
            thrustAcceleration = 0f,
            poweredDuration = 0f,
            guidanceTurnRate = 0f,
            guidanceMethod = WeaponGuidanceMethod.None,
            seekerAngle = 0f,
            irDecoyDiversionChance = 0f,
            sarhLookDownResistance = 0f,
            arhCandidateVelocityThreshold = 0f,
            arhCountermeasureResistance = 0f,
            fuzeType = WeaponFuzeType.None,
            fuzeRadius = 0f,
            detonationTime = 0f,
            explosionRadius = 0f
        };
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
            "初速　110\n" +
            "散布界　2.5deg\n" +
            "射程　1.4s\n" +
            "拡張\n" +
            "推力　0m/s^2\n" +
            "噴進時間　0s\n" +
            "誘導性能　0deg/s\n" +
            "誘導方式　N/A（IR/SARH/ARH）\n" +
            "誘導シーカー　0deg\n" +
            "IR欺瞞耐性（別目標誘引確率）　0%\n" +
            "SARHルックダウン耐性　-0m\n" +
            "ARH目標候補化閾値　+/-0m/s\n" +
            "ARH欺瞞耐性　0%\n" +
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
        seekerAngle = Mathf.Max(0f, seekerAngle);
        irDecoyDiversionChance = Mathf.Clamp01(irDecoyDiversionChance);
        sarhLookDownResistance = Mathf.Max(0f, sarhLookDownResistance);
        arhCandidateVelocityThreshold = Mathf.Max(0f, arhCandidateVelocityThreshold);
        arhCountermeasureResistance = Mathf.Clamp01(arhCountermeasureResistance);
        fuzeRadius = Mathf.Max(0f, fuzeRadius);
        detonationTime = Mathf.Max(0f, detonationTime);
        explosionRadius = Mathf.Max(0f, explosionRadius);
    }
}
