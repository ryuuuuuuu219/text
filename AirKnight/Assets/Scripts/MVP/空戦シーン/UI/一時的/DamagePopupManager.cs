using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DamagePopupManager : MonoBehaviour
{
    static DamagePopupManager instance;

    [SerializeField] Camera targetCamera;
    [SerializeField] AircraftAffiliation playerAffiliation = AircraftAffiliation.A;
    [SerializeField, Min(0)] int initialPoolSize = 16;
    [SerializeField, Min(1)] int maximumPopupCount = 64;
    [SerializeField, Min(0.01f)] float displayTime = 1f;
    [SerializeField, Min(0f)] float viewportRiseSpeed = 0.035f;
    [SerializeField, Min(0.001f)] float textViewportHeight = 0.025f;
    [SerializeField, Min(0f)] float randomViewportOffset = 0.0125f;
    [SerializeField] Color friendlyDamageColor = new(0f, 0f, 0.1f, 0.7f);
    [SerializeField] Color enemyDamageColor = new(1f, 0f, 0f, 0.7f);

    readonly Queue<DamagePopupUI> availablePopups = new();
    Font runtimeFont;
    int createdPopupCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
    }

    public static void ShowDamage(
        Vector3 worldPosition,
        float damage,
        AircraftAffiliation damagedAffiliation)
    {
        if (damage <= 0f) return;
        EnsureInstance().Show(worldPosition, damage, damagedAffiliation);
    }

    public static void ShowDamage(Vector3 worldPosition, float damage, AircraftFlightAI damagedAircraft)
    {
        if (damagedAircraft == null || damage <= 0f) return;
        ShowDamage(worldPosition, damage, damagedAircraft.affiliation);
    }

    static DamagePopupManager EnsureInstance()
    {
        if (instance != null) return instance;
        instance = FindAnyObjectByType<DamagePopupManager>();
        if (instance != null) return instance;

        GameObject managerObject = new("Damage Popup Manager");
        instance = managerObject.AddComponent<DamagePopupManager>();
        DontDestroyOnLoad(managerObject);
        return instance;
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        int poolSize = Mathf.Min(initialPoolSize, maximumPopupCount);
        for (int i = 0; i < poolSize; i++) availablePopups.Enqueue(CreatePopup());
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void OnValidate()
    {
        initialPoolSize = Mathf.Max(0, initialPoolSize);
        maximumPopupCount = Mathf.Max(1, maximumPopupCount);
        initialPoolSize = Mathf.Min(initialPoolSize, maximumPopupCount);
        displayTime = Mathf.Max(0.01f, displayTime);
        viewportRiseSpeed = Mathf.Max(0f, viewportRiseSpeed);
        textViewportHeight = Mathf.Max(0.001f, textViewportHeight);
        randomViewportOffset = Mathf.Max(0f, randomViewportOffset);
    }

    void Show(Vector3 worldPosition, float damage, AircraftAffiliation damagedAffiliation)
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null) return;

        DamagePopupUI popup = availablePopups.Count > 0
            ? availablePopups.Dequeue()
            : createdPopupCount < maximumPopupCount
                ? CreatePopup()
                : null;
        if (popup == null) return;

        Vector2 randomOffset = Random.insideUnitCircle * randomViewportOffset;
        Color color = damagedAffiliation == playerAffiliation
            ? friendlyDamageColor
            : enemyDamageColor;
        popup.Show(
            targetCamera,
            worldPosition,
            Mathf.RoundToInt(damage).ToString(),
            color,
            randomOffset,
            displayTime,
            viewportRiseSpeed,
            textViewportHeight);
    }

    DamagePopupUI CreatePopup()
    {
        GameObject popupObject = new("Damage Popup");
        popupObject.transform.SetParent(transform, false);
        DamagePopupUI popup = popupObject.AddComponent<DamagePopupUI>();
        popup.Initialize(runtimeFont, ReleasePopup);
        createdPopupCount++;
        return popup;
    }

    void ReleasePopup(DamagePopupUI popup)
    {
        if (popup != null) availablePopups.Enqueue(popup);
    }
}
