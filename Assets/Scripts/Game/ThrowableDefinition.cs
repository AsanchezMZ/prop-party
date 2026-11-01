using UnityEngine;

[CreateAssetMenu(menuName = "PropHunt/Throwable")]
public class ThrowableDefinition : ScriptableObject
{
    public HunterProjectile projectilePrefab;
    [Min(1)] public float speed = 18;
    [Min(0)] public float gravity = 15;
    [Min(0.05f)] public float radius = 0.15f;
    [Min(0.1f)] public float lifetime = 5;
    [Min(0.1f)] public float cooldown = 0.8f;
    [Min(1)] public int damage = 1;
    public LayerMask hitMask = ~0;
    public AudioClip throwSound;
    public AudioClip impactSound;
    public GameObject impactEffect;
}
