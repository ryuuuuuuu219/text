using System;
using UnityEngine;

public enum PilotPersonality
{
    Cautious,
    Balanced,
    Aggressive
}

[DisallowMultipleComponent]
public sealed class PilotStatus : MonoBehaviour
{
    [Header("Identity")]
    public int pilotId;
    public string pilotName = "Pilot";
    public PilotPersonality personality = PilotPersonality.Balanced;
    [Min(0f)] public float fame;

    [Header("Control Input")]
    [Range(0f, 45f)] public float targetAngleOfAttack = 30f;
    [Range(0f, 45f)] public float accelerationTargetAngleOfAttack = 8f;
    [Min(0f)] public float minimumSpeedAwareness = 25f;

    [Header("Maneuver Judgment")]
    [Min(0f)] public float accelerationPrioritySpeedThreshold = 25f;
    [Min(0f)] public float accelerationPriorityDecelerationThreshold = 18f;
    [Min(0f)] public float altitudeRecoveryThreshold = 800f;

    [Header("Vitality")]
    [Range(0f, 1f)] public float escapeChance = 0.75f;
    [Min(0f)] public float maximumLongTermStamina = 100f;
    [SerializeField, Min(0f)] float longTermStamina = 100f;
    [Min(0.01f)] public float maximumShortTermStamina = 100f;
    [SerializeField, Min(0f)] float shortTermStamina = 100f;
    [Min(0f)] public float shortTermRecoveryPerSecond = 8f;
    [Range(0f, 1f)] public float shortTermPenaltyThreshold = 0.3f;
    [Min(0f)] public float staminaPenaltySlope = 1f;
    [Min(0f)] public float positiveGTolerance = 6f;
    [Min(0f)] public float negativeGTolerance = 3f;

    [Header("Awareness & Skill")]
    [Min(1f)] public float detectionRadius = 2000f;
    [Range(0f, 1f)] public float firingRangeRatio = 0.8f;
    [Min(0f)] public float proficiencyDamageMultiplier = 1f;
    [Range(0f, 1f)] public float aircraftFamiliarity = 0.5f;

    [Header("Persistent Condition")]
    [Range(0f, 1f)] public float fatigue;
    [Range(0f, 1f)] public float injury;

    public event Action<PilotStatus> Changed;
    public float LongTermStamina => longTermStamina;
    public float ShortTermStamina => shortTermStamina;
    public float ShortTermStaminaRatio => maximumShortTermStamina > 0f
        ? shortTermStamina / maximumShortTermStamina : 0f;

    public float ControlEffectiveness
    {
        get
        {
            float staminaRatio = ShortTermStaminaRatio;
            float staminaFactor = staminaRatio >= shortTermPenaltyThreshold
                ? 1f
                : Mathf.Clamp01(1f - (shortTermPenaltyThreshold - staminaRatio) * staminaPenaltySlope);
            return Mathf.Clamp01(staminaFactor * (1f - fatigue * 0.5f) * (1f - injury));
        }
    }

    void Awake()
    {
        longTermStamina = Mathf.Clamp(longTermStamina, 0f, maximumLongTermStamina);
        shortTermStamina = Mathf.Clamp(shortTermStamina, 0f, maximumShortTermStamina);
    }

    void Update()
    {
        RecoverShortTermStamina(shortTermRecoveryPerSecond * Time.deltaTime, false);
    }

    void OnValidate()
    {
        longTermStamina = Mathf.Clamp(longTermStamina, 0f, maximumLongTermStamina);
        shortTermStamina = Mathf.Clamp(shortTermStamina, 0f, maximumShortTermStamina);
        targetAngleOfAttack = Mathf.Clamp(targetAngleOfAttack, 0f, 45f);
        accelerationTargetAngleOfAttack = Mathf.Clamp(accelerationTargetAngleOfAttack, 0f, 45f);
        accelerationPrioritySpeedThreshold = Mathf.Max(0f, accelerationPrioritySpeedThreshold);
        accelerationPriorityDecelerationThreshold = Mathf.Max(0f, accelerationPriorityDecelerationThreshold);
        altitudeRecoveryThreshold = Mathf.Max(0f, altitudeRecoveryThreshold);
    }

    public void ResetRuntimeState()
    {
        longTermStamina = maximumLongTermStamina;
        shortTermStamina = maximumShortTermStamina;
        Changed?.Invoke(this);
    }

    public void ConsumeLongTermStamina(float amount)
    {
        if (!float.IsFinite(amount) || amount <= 0f) return;
        longTermStamina = Mathf.Max(0f, longTermStamina - amount);
        Changed?.Invoke(this);
    }

    public void ConsumeShortTermStamina(float amount)
    {
        if (!float.IsFinite(amount) || amount <= 0f) return;
        shortTermStamina = Mathf.Max(0f, shortTermStamina - amount);
        Changed?.Invoke(this);
    }

    public void RecoverShortTermStamina(float amount, bool notify = true)
    {
        if (!float.IsFinite(amount) || amount <= 0f || shortTermStamina >= maximumShortTermStamina) return;
        shortTermStamina = Mathf.Min(maximumShortTermStamina, shortTermStamina + amount);
        if (notify) Changed?.Invoke(this);
    }

    public bool RollEscape(float randomValue01)
    {
        return Mathf.Clamp01(randomValue01) <= Mathf.Clamp01(escapeChance * (1f - injury));
    }
}
