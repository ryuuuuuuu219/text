using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CountermeasureManager : MonoBehaviour
{
    static CountermeasureManager instance;

    [SerializeField] List<CountermeasureSignature> irObjects = new();
    [SerializeField] List<CountermeasureSignature> sarhObjects = new();
    [SerializeField] List<CountermeasureSignature> arhObjects = new();

    public static CountermeasureManager Instance => EnsureInstance();
    public static CountermeasureManager ExistingInstance => instance;
    public IReadOnlyList<CountermeasureSignature> IRObjects => irObjects;
    public IReadOnlyList<CountermeasureSignature> SARHObjects => sarhObjects;
    public IReadOnlyList<CountermeasureSignature> ARHObjects => arhObjects;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
    }

    public static CountermeasureManager EnsureInstance()
    {
        if (instance != null) return instance;
        instance = FindAnyObjectByType<CountermeasureManager>();
        if (instance != null) return instance;

        GameObject managerObject = new("Countermeasure Manager");
        instance = managerObject.AddComponent<CountermeasureManager>();
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
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public void Register(CountermeasureSignature signature)
    {
        if (signature == null) return;
        Unregister(signature);
        GetMutableList(signature.SignatureType)?.Add(signature);
    }

    public void Unregister(CountermeasureSignature signature)
    {
        if (signature == null) return;
        irObjects.Remove(signature);
        sarhObjects.Remove(signature);
        arhObjects.Remove(signature);
    }

    public IReadOnlyList<CountermeasureSignature> GetObjects(WeaponGuidanceMethod method)
    {
        return method switch
        {
            WeaponGuidanceMethod.IR => irObjects,
            WeaponGuidanceMethod.SARH => sarhObjects,
            WeaponGuidanceMethod.ARH => arhObjects,
            _ => null
        };
    }

    List<CountermeasureSignature> GetMutableList(CountermeasureSignatureType type)
    {
        return type switch
        {
            CountermeasureSignatureType.IR => irObjects,
            CountermeasureSignatureType.SARH => sarhObjects,
            CountermeasureSignatureType.ARH => arhObjects,
            _ => null
        };
    }
}
