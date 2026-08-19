using UnityEngine;

public sealed class WeaponProjectile : MonoBehaviour
{
    const int InitialHitBufferSize = 16;
    static readonly RaycastHit[] HitBuffer = new RaycastHit[InitialHitBufferSize];
    static Material tracerMaterial;

    WeaponParameters weaponParameters;
    AircraftFlightAI owner;
    Transform currentTarget;
    CountermeasureSignature divertedCountermeasure;
    Vector3 seekerDirection;
    Vector3 velocity;
    float damage;
    float remainingFlightTime;
    float radius;

    public Transform CurrentTarget => currentTarget;
    public Vector3 SeekerDirection => seekerDirection;

    public static WeaponProjectile Spawn(
        WeaponParameters parameters,
        AircraftFlightAI sourceAircraft,
        Transform initialTarget,
        Vector3 position,
        Vector3 initialVelocity,
        float projectileDamage,
        float flightTime,
        float projectileRadius)
    {
        GameObject projectileObject = new(parameters.weaponName + " Projectile");
        projectileObject.transform.SetPositionAndRotation(
            position,
            initialVelocity.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(initialVelocity.normalized)
                : Quaternion.identity);
        WeaponProjectile projectile = projectileObject.AddComponent<WeaponProjectile>();
        projectile.weaponParameters = parameters;
        projectile.owner = sourceAircraft;
        projectile.currentTarget = initialTarget;
        projectile.velocity = initialVelocity;
        projectile.seekerDirection = initialVelocity.sqrMagnitude > 0.0001f
            ? initialVelocity.normalized
            : projectileObject.transform.forward;
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
        if (weaponParameters.exhaustVisualType == WeaponExhaustVisualType.Smoke)
            ConfigureSmokeParticles();
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

    void ConfigureSmokeParticles()
    {
        ParticleSystem particles = gameObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.startLifetime = 0.8f;
        main.startSpeed = 0.1f;
        main.startSize = 0.3f;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.7f, 0.7f, 0.7f, 0.55f),
            new Color(0.25f, 0.25f, 0.25f, 0.15f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 192;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 35f;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.04f;
        particles.Play();
    }

    void Update()
    {
        UpdateSeekerTarget();
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

        ApplyGuidance(deltaTime);
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

    void UpdateSeekerTarget()
    {
        if (weaponParameters.guidanceMethod == WeaponGuidanceMethod.None) return;

        if (divertedCountermeasure != null &&
            TryGetSeekerDot(divertedCountermeasure.transform.position, out _))
        {
            currentTarget = divertedCountermeasure.transform;
            return;
        }

        divertedCountermeasure = null;

        AcquireAircraftTarget();
        TryDivertToCountermeasure();
    }

    void AcquireAircraftTarget()
    {
        AircraftFlightAI bestTarget = null;
        float bestDot = -1f;
        var aircraft = AircraftFlightAI.ActiveAircraft;
        for (int i = 0; i < aircraft.Count; i++)
        {
            AircraftFlightAI candidate = aircraft[i];
            if (candidate == null || candidate == owner) continue;
            if (owner != null && candidate.affiliation == owner.affiliation) continue;
            if (!TryGetSeekerDot(candidate.transform.position, out float dot)) continue;
            if (dot <= bestDot) continue;
            bestDot = dot;
            bestTarget = candidate;
        }

        currentTarget = bestTarget != null ? bestTarget.transform : null;
    }

    void TryDivertToCountermeasure()
    {
        CountermeasureManager manager = CountermeasureManager.ExistingInstance;
        if (manager == null) return;
        var countermeasures = manager.GetObjects(weaponParameters.guidanceMethod);
        if (countermeasures == null) return;

        float diversionChance = GetCountermeasureDiversionChance();
        if (diversionChance <= 0f) return;
        for (int i = 0; i < countermeasures.Count; i++)
        {
            CountermeasureSignature candidate = countermeasures[i];
            if (candidate == null) continue;
            if (!TryGetSeekerDot(candidate.transform.position, out _)) continue;
            if (Random.Range(0f, 100f) >= diversionChance) continue;
            divertedCountermeasure = candidate;
            currentTarget = candidate.transform;
            return;
        }
    }

    bool TryGetSeekerDot(Vector3 targetPosition, out float dot)
    {
        Vector3 offset = targetPosition - transform.position;
        if (offset.sqrMagnitude <= 0.0001f)
        {
            dot = 1f;
            return true;
        }

        Vector3 normalizedSeekerDirection = seekerDirection.sqrMagnitude > 0.0001f
            ? seekerDirection.normalized
            : transform.forward;
        dot = Vector3.Dot(normalizedSeekerDirection, offset.normalized);
        float minimumDot = Mathf.Cos(
            Mathf.Clamp(weaponParameters.seekerAngle, 0f, 180f) * Mathf.Deg2Rad);
        return dot >= minimumDot;
    }

    float GetCountermeasureDiversionChance()
    {
        return weaponParameters.guidanceMethod switch
        {
            WeaponGuidanceMethod.IR => Mathf.Clamp(
                weaponParameters.irDecoyDiversionChance,
                0f,
                100f),
            WeaponGuidanceMethod.SARH => 100f,
            WeaponGuidanceMethod.ARH => 100f - Mathf.Clamp(
                weaponParameters.arhCountermeasureResistance,
                0f,
                100f),
            _ => 0f
        };
    }

    void ApplyGuidance(float deltaTime)
    {
        if (currentTarget == null || weaponParameters.guidanceTurnRate <= 0f) return;
        Vector3 targetDirection = currentTarget.position - transform.position;
        if (targetDirection.sqrMagnitude <= 0.0001f) return;

        float maximumTurnRadians = weaponParameters.guidanceTurnRate * Mathf.Deg2Rad * deltaTime;
        seekerDirection = Vector3.RotateTowards(
            seekerDirection,
            targetDirection.normalized,
            maximumTurnRadians,
            0f).normalized;
        float speed = velocity.magnitude;
        velocity = seekerDirection * speed;
        transform.rotation = Quaternion.LookRotation(seekerDirection);
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
