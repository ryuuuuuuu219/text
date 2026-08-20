using System.Collections.Generic;
using UnityEngine;

public sealed class WeaponProjectile : MonoBehaviour
{
    const int InitialHitBufferSize = 16;
    const float ProjectileVisualScale = 20f;
    static readonly RaycastHit[] HitBuffer = new RaycastHit[InitialHitBufferSize];
    static Material tracerMaterial;
    static Material defaultParticleMaterial;

    WeaponParameters weaponParameters;
    AircraftFlightAI owner;
    Transform currentTarget;
    CountermeasureSignature divertedCountermeasure;
    Vector3 seekerDirection;
    Vector3 velocity;
    float damage;
    float remainingFlightTime;
    float elapsedFlightTime;
    float radius;
    public bool terminalGuidanceActive;
    bool hasArhVelocityReference;
    float arhReferenceRadialVelocity;
    bool submunitionsSpawned;

    public Transform CurrentTarget => currentTarget;
    public Vector3 SeekerDirection => seekerDirection;
    public bool IsTerminalGuidanceActive => terminalGuidanceActive;
    public WeaponGuidanceMethod CurrentGuidanceMethod => ActiveGuidanceMethod;

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
        projectile.InitializeGuidanceReference();
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
        trail.startWidth = 0.08f * ProjectileVisualScale;
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
        ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        Material particleMaterial = GetDefaultParticleMaterial();
        if (particleMaterial != null)
            particleRenderer.sharedMaterial = particleMaterial;
        particles.Play();
    }

    public static Material GetDefaultParticleMaterial()
    {
        if (defaultParticleMaterial != null) return defaultParticleMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) return null;

        defaultParticleMaterial = new Material(shader)
        {
            name = "Runtime Default Particle Material",
            hideFlags = HideFlags.HideAndDontSave
        };
        return defaultParticleMaterial;
    }

    void Update()
    {
        UpdateTerminalGuidanceState();
        UpdateSeekerTarget();
    }

    WeaponGuidanceMethod ActiveGuidanceMethod => terminalGuidanceActive
        ? weaponParameters.terminalGuidanceMethod
        : weaponParameters.guidanceMethod;
    float ActiveGuidanceTurnRate => terminalGuidanceActive
        ? weaponParameters.terminalGuidanceTurnRate
        : weaponParameters.guidanceTurnRate;
    float ActiveProportionalNavigationConstant
    {
        get
        {
            float configuredValue = terminalGuidanceActive
                ? weaponParameters.terminalProportionalNavigationConstant
                : weaponParameters.proportionalNavigationConstant;
            return configuredValue > 0f ? configuredValue : 3f;
        }
    }
    float ActiveSeekerAngle => terminalGuidanceActive
        ? weaponParameters.terminalSeekerAngle
        : weaponParameters.seekerAngle;
    float ActiveSarhLookDownResistance => terminalGuidanceActive
        ? weaponParameters.terminalSarhLookDownResistance
        : weaponParameters.sarhLookDownResistance;
    float ActiveArhCandidateVelocityThreshold => terminalGuidanceActive
        ? weaponParameters.terminalArhCandidateVelocityThreshold
        : weaponParameters.arhCandidateVelocityThreshold;

    void InitializeGuidanceReference()
    {
        if (ActiveGuidanceMethod == WeaponGuidanceMethod.ARH)
            CaptureArhVelocityReference();
    }

    void UpdateTerminalGuidanceState()
    {
        if (terminalGuidanceActive ||
            !weaponParameters.hasTerminalGuidance ||
            currentTarget == null ||
            weaponParameters.terminalGuidanceActivationDistance <= 0f)
        {
            return;
        }

        float activationDistance = weaponParameters.terminalGuidanceActivationDistance;
        if ((currentTarget.position - transform.position).sqrMagnitude >
            activationDistance * activationDistance)
            return;

        terminalGuidanceActive = true;
        divertedCountermeasure = null;
        if (ActiveGuidanceMethod == WeaponGuidanceMethod.ARH)
            CaptureArhVelocityReference();
    }

    void CaptureArhVelocityReference()
    {
        if (currentTarget == null) return;
        arhReferenceRadialVelocity = GetRadialVelocity(
            currentTarget.position,
            GetTargetVelocity(currentTarget));
        hasArhVelocityReference = true;
    }

    float GetRadialVelocity(Vector3 targetPosition, Vector3 targetVelocity)
    {
        Vector3 offset = targetPosition - transform.position;
        if (offset.sqrMagnitude <= 0.0001f) return 0f;
        return Vector3.Dot(targetVelocity, offset.normalized);
    }

    static Vector3 GetTargetVelocity(Transform target)
    {
        if (target == null) return Vector3.zero;
        CountermeasureSignature countermeasure = target.GetComponent<CountermeasureSignature>();
        if (countermeasure != null) return countermeasure.Velocity;
        AircraftFlightAI aircraft = target.GetComponentInParent<AircraftFlightAI>();
        if (aircraft != null) return aircraft.Velocity;
        Rigidbody targetBody = target.GetComponentInParent<Rigidbody>();
        return targetBody != null ? targetBody.linearVelocity : Vector3.zero;
    }

    bool IsBlockedBySarhLookDown(Vector3 targetPosition)
    {
        if (ActiveGuidanceMethod != WeaponGuidanceMethod.SARH) return false;
        float lookDownDistance = transform.position.y - targetPosition.y;
        return lookDownDistance > ActiveSarhLookDownResistance;
    }

    void FixedUpdate()
    {
        float deltaTime = Time.fixedDeltaTime;
        UpdateTerminalGuidanceState();
        elapsedFlightTime += deltaTime;
        remainingFlightTime -= deltaTime;
        if (weaponParameters.fuzeType == WeaponFuzeType.Timed &&
            weaponParameters.detonationTime > 0f &&
            elapsedFlightTime >= weaponParameters.detonationTime)
        {
            Detonate(transform.position);
            return;
        }
        if (TryDetonateByProximity()) return;
        if (remainingFlightTime <= 0f)
        {
            Expire();
            return;
        }

        ApplyGuidance(deltaTime);
        ApplyPropulsion(deltaTime);
        Vector3 displacement = velocity * deltaTime;
        float distance = displacement.magnitude;
        if (distance > 0.0001f && TryFindHit(displacement / distance, distance, out RaycastHit hit))
        {
            HandleImpact(hit);
            return;
        }

        transform.position += displacement;
        if (velocity.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(velocity.normalized);
        TryDetonateByProximity();
    }

    void UpdateSeekerTarget()
    {
        WeaponGuidanceMethod guidanceMethod = ActiveGuidanceMethod;
        if (guidanceMethod == WeaponGuidanceMethod.None) return;

        if (divertedCountermeasure != null &&
            IsValidGuidanceCandidate(
                divertedCountermeasure.transform.position,
                divertedCountermeasure.Velocity,
                out _))
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
            if (candidate.IsDestroyed) continue;
            if (!IsValidGuidanceCandidate(
                    candidate.transform.position,
                    candidate.Velocity,
                    out float dot)) continue;
            if (dot <= bestDot) continue;
            bestDot = dot;
            bestTarget = candidate;
        }

        currentTarget = bestTarget != null ? bestTarget.transform : null;
    }

    void TryDivertToCountermeasure()
    {
        WeaponGuidanceMethod guidanceMethod = ActiveGuidanceMethod;
        if (guidanceMethod == WeaponGuidanceMethod.SARH ||
            guidanceMethod == WeaponGuidanceMethod.None)
            return;
        CountermeasureManager manager = CountermeasureManager.ExistingInstance;
        if (manager == null) return;
        var countermeasures = manager.GetObjects(guidanceMethod);
        if (countermeasures == null) return;

        float diversionChance = GetCountermeasureDiversionChance();
        if (diversionChance <= 0f) return;
        for (int i = 0; i < countermeasures.Count; i++)
        {
            CountermeasureSignature candidate = countermeasures[i];
            if (candidate == null) continue;
            if (!IsValidGuidanceCandidate(
                    candidate.transform.position,
                    candidate.Velocity,
                    out _)) continue;
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
        float seekerAngle = ActiveSeekerAngle;
        if (seekerAngle <= 0f) return true;
        float minimumDot = Mathf.Cos(Mathf.Clamp(seekerAngle, 0f, 180f) * Mathf.Deg2Rad);
        return dot >= minimumDot;
    }

    bool IsValidGuidanceCandidate(
        Vector3 targetPosition,
        Vector3 targetVelocity,
        out float dot)
    {
        if (!TryGetSeekerDot(targetPosition, out dot)) return false;
        if (IsBlockedBySarhLookDown(targetPosition)) return false;
        if (ActiveGuidanceMethod != WeaponGuidanceMethod.ARH ||
            !hasArhVelocityReference ||
            ActiveArhCandidateVelocityThreshold <= 0f)
            return true;

        float radialVelocity = GetRadialVelocity(targetPosition, targetVelocity);
        return Mathf.Abs(radialVelocity - arhReferenceRadialVelocity) <=
               ActiveArhCandidateVelocityThreshold;
    }

    float GetCountermeasureDiversionChance()
    {
        return ActiveGuidanceMethod switch
        {
            WeaponGuidanceMethod.IR => Mathf.Clamp(
                terminalGuidanceActive
                    ? weaponParameters.terminalIrDecoyDiversionChance
                    : weaponParameters.irDecoyDiversionChance,
                0f,
                100f),
            WeaponGuidanceMethod.SARH => 0f,
            WeaponGuidanceMethod.ARH => Mathf.Clamp(
                terminalGuidanceActive
                    ? weaponParameters.terminalArhDecoyDiversionChance
                    : weaponParameters.arhDecoyDiversionChance,
                0f,
                100f),
            _ => 0f
        };
    }

    void ApplyGuidance(float deltaTime)
    {
        float guidanceTurnRate = ActiveGuidanceTurnRate;
        if (currentTarget == null || guidanceTurnRate <= 0f) return;
        float speed = velocity.magnitude;
        if (speed <= 0.0001f) return;

        Vector3 lineOfSight = currentTarget.position - transform.position;
        float range = lineOfSight.magnitude;
        if (range <= 0.0001f) return;

        Vector3 lineOfSightDirection = lineOfSight / range;
        Vector3 missileDirection = velocity / speed;
        Vector3 relativeVelocity = GetTargetVelocity(currentTarget) - velocity;
        float closingSpeed = Mathf.Max(
            0f,
            -Vector3.Dot(relativeVelocity, lineOfSightDirection));
        if (closingSpeed <= 0f) return;

        Vector3 lineOfSightAngularVelocity = Vector3.Cross(
            lineOfSightDirection,
            relativeVelocity) / range;
        Vector3 commandedAcceleration =
            ActiveProportionalNavigationConstant *
            closingSpeed *
            Vector3.Cross(lineOfSightAngularVelocity, missileDirection);
        Vector3 commandedVelocity = velocity + commandedAcceleration * deltaTime;
        if (commandedVelocity.sqrMagnitude <= 0.0001f) return;

        float maximumTurnRadians = guidanceTurnRate * Mathf.Deg2Rad * deltaTime;
        seekerDirection = Vector3.RotateTowards(
            missileDirection,
            commandedVelocity.normalized,
            maximumTurnRadians,
            0f).normalized;
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

    bool TryDetonateByProximity()
    {
        if (weaponParameters.fuzeType != WeaponFuzeType.Proximity ||
            weaponParameters.fuzeRadius <= 0f ||
            currentTarget == null)
            return false;

        float fuzeRadius = weaponParameters.fuzeRadius;
        if ((currentTarget.position - transform.position).sqrMagnitude > fuzeRadius * fuzeRadius)
            return false;
        Detonate(transform.position);
        return true;
    }

    void HandleImpact(RaycastHit hit)
    {
        if (weaponParameters.fuzeType == WeaponFuzeType.None)
        {
            ApplyDirectHit(hit);
            Destroy(gameObject);
            return;
        }
        Detonate(hit.point);
    }

    void Expire()
    {
        if (weaponParameters.fuzeType != WeaponFuzeType.None ||
            weaponParameters.explosionRadius > 0f)
        {
            Detonate(transform.position);
            return;
        }
        SpawnSubmunitions(transform.position);
        Destroy(gameObject);
    }

    void Detonate(Vector3 position)
    {
        if (weaponParameters.explosionRadius > 0f)
        {
            ApplyExplosionDamage(position);
            SpawnExplosionVisual(position, weaponParameters.explosionRadius);
        }
        SpawnSubmunitions(position);
        Destroy(gameObject);
    }

    void ApplyDirectHit(RaycastHit hit)
    {
        AircraftStatus targetStatus = hit.collider.GetComponentInParent<AircraftStatus>();
        if (targetStatus == null) return;
        AircraftFlightAI targetAircraft = hit.collider.GetComponentInParent<AircraftFlightAI>();
        ApplyDamage(targetStatus, targetAircraft, hit.point);
    }

    void ApplyExplosionDamage(Vector3 position)
    {
        Collider[] colliders = Physics.OverlapSphere(
            position,
            weaponParameters.explosionRadius,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);
        HashSet<AircraftStatus> damagedTargets = new();
        for (int i = 0; i < colliders.Length; i++)
        {
            AircraftStatus targetStatus = colliders[i].GetComponentInParent<AircraftStatus>();
            if (targetStatus == null || !damagedTargets.Add(targetStatus)) continue;
            AircraftFlightAI targetAircraft = colliders[i].GetComponentInParent<AircraftFlightAI>();
            if (targetAircraft == owner) continue;
            ApplyDamage(targetStatus, targetAircraft, position);
        }
    }

    void ApplyDamage(
        AircraftStatus targetStatus,
        AircraftFlightAI targetAircraft,
        Vector3 position)
    {
        targetStatus.ApplyDamage(damage);
        if (targetAircraft != null)
            DamagePopupManager.ShowDamage(position, damage, targetAircraft);
    }

    void SpawnSubmunitions(Vector3 position)
    {
        if (submunitionsSpawned || weaponParameters.submunitions == null) return;
        submunitionsSpawned = true;
        float damageMultiplier = weaponParameters.baseDamage > 0f
            ? damage / weaponParameters.baseDamage
            : 1f;
        Vector3 forward = velocity.sqrMagnitude > 0.0001f
            ? velocity.normalized
            : transform.forward;

        for (int entryIndex = 0; entryIndex < weaponParameters.submunitions.Count; entryIndex++)
        {
            WeaponSubmunition entry = weaponParameters.submunitions[entryIndex];
            if (!WeaponParameters.TryCreate(entry.weaponName, out WeaponParameters childParameters))
            {
                Debug.LogWarning($"Unknown submunition '{entry.weaponName}' on {weaponParameters.weaponName}.", this);
                continue;
            }

            int childCount = Mathf.Max(1, entry.number);
            for (int i = 0; i < childCount; i++)
            {
                Vector3 childDirection = GetDispersedDirection(
                    forward,
                    childParameters.dispersionAngle);
                WeaponProjectile.Spawn(
                    childParameters,
                    owner,
                    currentTarget,
                    position + childDirection * Mathf.Max(radius, 0.01f),
                    childDirection * childParameters.muzzleVelocity,
                    childParameters.baseDamage * damageMultiplier,
                    childParameters.maximumFlightTime,
                    radius);
            }
        }
    }

    static Vector3 GetDispersedDirection(Vector3 forward, float dispersionAngle)
    {
        Vector3 normalizedForward = forward.sqrMagnitude > 0.0001f
            ? forward.normalized
            : Vector3.forward;
        float maximumAngle = Mathf.Clamp(dispersionAngle, 0f, 180f) * Mathf.Deg2Rad;
        if (maximumAngle <= 0f) return normalizedForward;
        return Vector3.RotateTowards(
            normalizedForward,
            Random.onUnitSphere,
            Random.Range(0f, maximumAngle),
            0f).normalized;
    }

    static void SpawnExplosionVisual(Vector3 position, float explosionRadius)
    {
        GameObject explosionObject = new("Runtime Weapon Explosion");
        explosionObject.transform.position = position;
        ParticleSystem particles = explosionObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.duration = 0.25f;
        main.startLifetime = 0.7f;
        main.startSpeed = Mathf.Max(1f, explosionRadius * 2f);
        main.startSize = Mathf.Max(0.2f, explosionRadius * 0.25f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.85f, 0.25f, 1f),
            new Color(1f, 0.15f, 0.02f, 0.15f));
        main.maxParticles = 96;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)Mathf.Clamp(
                Mathf.CeilToInt(explosionRadius * 6f),
                12,
                96))
        });
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = Mathf.Max(0.05f, explosionRadius * 0.08f);
        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        Material material = GetDefaultParticleMaterial();
        if (material != null) renderer.sharedMaterial = material;
        particles.Play();
        Destroy(explosionObject, 1.5f);
    }
}
