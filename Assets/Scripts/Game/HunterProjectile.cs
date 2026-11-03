using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

[RequireComponent(typeof(NetworkObject), typeof(NetworkTransform))]
public class HunterProjectile : NetworkBehaviour
{
    [SerializeField] ThrowableDefinition definition;
    [Tooltip("Optional mesh/visual child. No asset is required by the gameplay code.")]
    [SerializeField] Transform visual;
    Vector3 velocity;
    PropHuntPlayer shooter;
    float remaining;
    TrailRenderer trail;
    readonly NetworkVariable<Vector3> launchOrigin = new();
    readonly NetworkVariable<Vector3> launchVelocity = new();
    readonly NetworkVariable<double> firedAt = new();

    public void Launch(PropHuntPlayer owner, Vector3 direction, ThrowableDefinition settings)
    {
        shooter = owner;
        definition = settings;
        velocity = direction * settings.speed;
        remaining = settings.lifetime;
    }

    public static Vector3 StraightPosition(Vector3 origin, Vector3 velocity, float elapsed) => origin + velocity * elapsed;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Launch prepares local state; NGO initializes these variables before this callback.
            launchOrigin.Value = transform.position;
            launchVelocity.Value = velocity;
            firedAt.Value = NetworkManager.ServerTime.Time;
        }
        if (!IsClient || Application.isBatchMode) return;
        var go = new GameObject("Egg trajectory");
        go.transform.position = visual != null ? visual.position : transform.position;
        trail = go.AddComponent<TrailRenderer>();
        trail.sharedMaterial = Resources.Load<Material>("EggTrail");
        trail.time = 0.3f; trail.minVertexDistance = 0.08f;
        trail.startWidth = 0.09f; trail.endWidth = 0;
        trail.startColor = new Color(1, 0.85f, 0.35f, 0.85f);
        trail.endColor = new Color(1, 0.98f, 0.85f, 0);
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
    }

    public override void OnNetworkDespawn()
    {
        if (trail == null) return;
        trail.emitting = false;
        Destroy(trail.gameObject, trail.time);
        trail = null;
    }

    void LateUpdate()
    {
        if (!IsSpawned) return;
        // Render straight shots at server time, without an extra NetworkTransform interpolation buffer.
        if (!IsServer && visual != null && definition != null && definition.gravity == 0)
        {
            float elapsed = Mathf.Clamp((float)(NetworkManager.ServerTime.Time - firedAt.Value), 0, definition.lifetime);
            visual.position = StraightPosition(launchOrigin.Value, launchVelocity.Value, elapsed);
        }
        if (trail != null) trail.transform.position = visual != null ? visual.position : transform.position;
    }

    void FixedUpdate()
    {
        if (!IsServer || definition == null) return;
        remaining -= Time.fixedDeltaTime;
        if (shooter == null || !shooter.CanHunt) { NetworkObject.Despawn(); return; }
        if (remaining <= 0)
        {
            ImpactClientRpc(transform.position, Vector3.up, true, false);
            NetworkObject.Despawn(); return;
        }
        velocity += Vector3.down * definition.gravity * Time.fixedDeltaTime;
        Vector3 delta = velocity * Time.fixedDeltaTime;
        // Ignore the shooter without allowing a nearer wall to be skipped.
        var hits = Physics.SphereCastAll(transform.position, definition.radius, delta.normalized,
            delta.magnitude, definition.hitMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            var target = hit.collider.GetComponentInParent<PropHuntPlayer>();
            if (target == shooter || (target != null && target.Role.Value == PlayerRole.Spectator)) continue;
            if (target != null && target.IgnoreMovementHitbox(hit.collider)) continue;
            bool hitProp = target != null && target.Role.Value == PlayerRole.Prop;
            if (hitProp) target.Hit(definition.damage, shooter);
            else if (target == null && hit.collider.GetComponentInParent<Prop>() != null) shooter.HitWrongProp();
            ImpactClientRpc(hit.point, hit.normal, !hitProp, true);
            NetworkObject.Despawn();
            return;
        }
        transform.position += delta;
        if (velocity.sqrMagnitude > 0) transform.rotation = Quaternion.LookRotation(velocity);
    }

    [ClientRpc]
    void ImpactClientRpc(Vector3 position, Vector3 normal, bool missed, bool collided)
    {
        if (definition == null) return;
        if (collided) PartyVfx.EggImpact(position, normal, !missed);
        var miss = definition.missSound != null ? definition.missSound : PartyAudio.Settings?.shotMiss;
        var impact = definition.impactSound != null ? definition.impactSound : PartyAudio.Settings?.impactSound;
        var clip = missed && miss != null ? miss : collided ? impact : null;
        if (clip != null) AudioSource.PlayClipAtPoint(clip, position, PartyAudio.Settings != null ? PartyAudio.Settings.EffectsVolume : GamePreferences.EffectsVolume);
        if (collided && definition.impactEffect != null) Destroy(Instantiate(definition.impactEffect, position, Quaternion.identity), 3);
    }
}
