using UnityEngine;

public sealed class WeaponProjectile : MonoBehaviour
{
    const int InitialHitBufferSize = 16;
    static readonly RaycastHit[] HitBuffer = new RaycastHit[InitialHitBufferSize];
    static Material tracerMaterial;

    WeaponParameters weaponParameters;
    AircraftFlightAI owner;
    Vector3 velocity;
    float damage;
    float remainingFlightTime;
    float radius;

    public static WeaponProjectile Spawn(
        WeaponStatus sourceWeapon,
        AircraftFlightAI sourceAircraft,
        Vector3 position,
        Vector3 initialVelocity,
        float projectileDamage,
        float flightTime,
        float projectileRadius)
    {
        WeaponParameters parameters = sourceWeapon != null
            ? sourceWeapon.Parameters
            : WeaponParameters.Create77mmGunPod();
        GameObject projectileObject = new(parameters.weaponName + " Projectile");
        projectileObject.transform.SetPositionAndRotation(
            position,
            initialVelocity.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(initialVelocity.normalized)
                : Quaternion.identity);
        WeaponProjectile projectile = projectileObject.AddComponent<WeaponProjectile>();
        projectile.weaponParameters = parameters;
        projectile.owner = sourceAircraft;
        projectile.velocity = initialVelocity;
        projectile.damage = Mathf.Max(0f, projectileDamage);
        projectile.remainingFlightTime = Mathf.Max(0.01f, flightTime);
        projectile.radius = Mathf.Max(0.001f, projectileRadius);
        projectile.ConfigureVisuals();
        return projectile;
    }

    void ConfigureVisuals()
    {
        if (weaponParameters.projectileVisualType == WeaponProjectileVisualType.Tracer)
            ConfigureTracer();

        // Exhaust is an explicit weapon attribute. Smoke particles can be attached here
        // without coupling the visual choice to thrustAcceleration.
    }

    void ConfigureTracer()
    {
        TrailRenderer trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = 0.15f;
        trail.minVertexDistance = 0.1f;
        trail.startWidth = 0.08f;
        trail.endWidth = 0f;
        trail.startColor = new Color(1f, 0.85f, 0.35f, 1f);
        trail.endColor = new Color(1f, 0.35f, 0.05f, 0f);
        trail.alignment = LineAlignment.View;
        Material material = GetTracerMaterial();
        if (material != null) trail.sharedMaterial = material;
    }

    static Material GetTracerMaterial()
    {
        if (tracerMaterial != null) return tracerMaterial;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) return null;

        tracerMaterial = new Material(shader)
        {
            name = "Runtime Tracer Material",
            hideFlags = HideFlags.HideAndDontSave
        };
        return tracerMaterial;
    }

    void FixedUpdate()
    {
        float deltaTime = Time.fixedDeltaTime;
        remainingFlightTime -= deltaTime;
        if (remainingFlightTime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        ApplyPropulsion(deltaTime);
        Vector3 displacement = velocity * deltaTime;
        float distance = displacement.magnitude;
        if (distance > 0.0001f && TryFindHit(displacement / distance, distance, out RaycastHit hit))
        {
            ApplyHit(hit);
            Destroy(gameObject);
            return;
        }

        transform.position += displacement;
        if (velocity.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(velocity.normalized);
    }

    void ApplyPropulsion(float deltaTime)
    {
        if (weaponParameters.thrustAcceleration <= 0f || weaponParameters.poweredDuration <= 0f)
            return;

        float elapsedTime = weaponParameters.maximumFlightTime - remainingFlightTime;
        if (elapsedTime <= weaponParameters.poweredDuration)
            velocity += transform.forward * (weaponParameters.thrustAcceleration * deltaTime);
    }

    bool TryFindHit(Vector3 direction, float distance, out RaycastHit closestHit)
    {
        int hitCount = Physics.SphereCastNonAlloc(
            transform.position,
            radius,
            direction,
            HitBuffer,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);
        float closestDistance = float.PositiveInfinity;
        closestHit = default;
        bool found = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit candidate = HitBuffer[i];
            AircraftFlightAI hitAircraft = candidate.collider.GetComponentInParent<AircraftFlightAI>();
            if (hitAircraft == owner || candidate.distance >= closestDistance) continue;
            closestDistance = candidate.distance;
            closestHit = candidate;
            found = true;
        }
        return found;
    }

    void ApplyHit(RaycastHit hit)
    {
        AircraftStatus targetStatus = hit.collider.GetComponentInParent<AircraftStatus>();
        if (targetStatus == null) return;

        targetStatus.ApplyDamage(damage);
        AircraftFlightAI targetAircraft = hit.collider.GetComponentInParent<AircraftFlightAI>();
        if (targetAircraft != null)
            DamagePopupManager.ShowDamage(hit.point, damage, targetAircraft);
    }
}
