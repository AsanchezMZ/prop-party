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

    public void Launch(PropHuntPlayer owner, Vector3 direction, ThrowableDefinition settings)
    {
        shooter = owner;
        definition = settings;
        velocity = direction * settings.speed;
        remaining = settings.lifetime;
    }

    void FixedUpdate()
    {
        if (!IsServer || definition == null) return;
        remaining -= Time.fixedDeltaTime;
        if (remaining <= 0 || shooter == null || !shooter.CanHunt) { NetworkObject.Despawn(); return; }
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
            if (target != null) target.Hit(definition.damage, shooter);
            ImpactClientRpc(hit.point);
            NetworkObject.Despawn();
            return;
        }
        transform.position += delta;
        if (velocity.sqrMagnitude > 0) transform.rotation = Quaternion.LookRotation(velocity);
    }

    [ClientRpc]
    void ImpactClientRpc(Vector3 position)
    {
        if (definition == null) return;
        PartyVfx.Burst(position, new Color(1, 0.8f, 0.2f));
        if (definition.impactSound != null) AudioSource.PlayClipAtPoint(definition.impactSound, position);
        if (definition.impactEffect != null) Destroy(Instantiate(definition.impactEffect, position, Quaternion.identity), 3);
    }
}
