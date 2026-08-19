using UnityEngine;

public sealed class CountermeasureSignature : MonoBehaviour
{
    CountermeasureSignatureType signatureType;
    Vector3 velocity;
    float thrustAcceleration;
    float poweredDuration;
    float remainingLifetime;
    float totalLifetime;
    bool registered;

    public CountermeasureSignatureType SignatureType => signatureType;

    public static CountermeasureSignature Spawn(
        WeaponParameters parameters,
        Vector3 position,
        Vector3 direction)
    {
        Vector3 launchDirection = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector3.back;
        GameObject countermeasureObject = new(parameters.weaponName);
        countermeasureObject.transform.SetPositionAndRotation(
            position,
            Quaternion.LookRotation(launchDirection));
        CountermeasureSignature signature = countermeasureObject.AddComponent<CountermeasureSignature>();
        signature.signatureType = parameters.countermeasureSignatureType;
        signature.velocity = launchDirection * Mathf.Max(0f, parameters.muzzleVelocity);
        signature.thrustAcceleration = Mathf.Max(0f, parameters.thrustAcceleration);
        signature.poweredDuration = Mathf.Max(0f, parameters.poweredDuration);
        signature.remainingLifetime = Mathf.Max(0.01f, parameters.maximumFlightTime);
        signature.totalLifetime = signature.remainingLifetime;
        signature.Register();
        if (parameters.exhaustVisualType == WeaponExhaustVisualType.Smoke)
            signature.ConfigureParticles();
        return signature;
    }

    void FixedUpdate()
    {
        float deltaTime = Time.fixedDeltaTime;
        remainingLifetime -= deltaTime;
        if (remainingLifetime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        float elapsedTime = totalLifetime - remainingLifetime;
        if (elapsedTime <= poweredDuration && thrustAcceleration > 0f)
            velocity += transform.forward * (thrustAcceleration * deltaTime);
        transform.position += velocity * deltaTime;
    }

    void Register()
    {
        if (registered || signatureType == CountermeasureSignatureType.None) return;
        CountermeasureManager.Instance.Register(this);
        registered = true;
    }

    void OnDestroy()
    {
        if (registered && CountermeasureManager.ExistingInstance != null)
            CountermeasureManager.ExistingInstance.Unregister(this);
        registered = false;
    }

    void ConfigureParticles()
    {
        ParticleSystem particles = gameObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.startLifetime = 0.6f;
        main.startSpeed = 0.2f;
        main.startSize = 0.25f;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.35f, 0.05f, 0.8f),
            new Color(0.45f, 0.45f, 0.45f, 0.2f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 128;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 30f;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.05f;
        particles.Play();
    }
}
