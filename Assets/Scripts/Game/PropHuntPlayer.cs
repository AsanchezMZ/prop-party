using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class PropHuntPlayer : NetworkBehaviour
{
    [SerializeField] Transform disguiseRoot;
    [SerializeField] GameObject normalVisual;
    [SerializeField] ThrowableDefinition weapon;
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip whistleSound;
    [SerializeField] AudioClip disguiseSound;
    [SerializeField] AudioClip capturedSound;
    [SerializeField] AudioClip jumpSound;
    [SerializeField] AudioClip footstepSound;
    public readonly NetworkVariable<PlayerRole> Role = new(PlayerRole.Spectator);
    public readonly NetworkVariable<int> Disguise = new();
    public readonly NetworkVariable<bool> Frozen = new();
    public readonly NetworkVariable<float> Rotation = new();
    public readonly NetworkVariable<int> Health = new();
    public PropHuntMatch Match { get; private set; }
    public bool CanHunt => Match != null && Match.Phase.Value == MatchPhase.Hunting && Role.Value == PlayerRole.Hunter;
    public bool CanMove => Match != null && (Match.Phase.Value == MatchPhase.Hunting ||
        (Match.Phase.Value == MatchPhase.Hiding && Role.Value == PlayerRole.Prop)) && !Frozen.Value;
    GameObject disguiseVisual;
    double nextThrow, nextWhistle, nextAction;
    float originalHeight, originalRadius;
    Vector3 originalCenter;

    void Awake()
    {
        var controller = GetComponent<CharacterController>();
        originalHeight = controller.height; originalRadius = controller.radius; originalCenter = controller.center;
    }

    public override void OnNetworkSpawn()
    {
        Match = FindFirstObjectByType<PropHuntMatch>();
        if (Match == null) { Debug.LogError("Player requires a PropHuntMatch in the map.", this); return; }
        Match.Register(this);
        Role.OnValueChanged += RoleChanged;
        Disguise.OnValueChanged += DisguiseChanged;
        RefreshVisual();
    }

    public override void OnNetworkDespawn()
    {
        if (Match != null) Match.Unregister(this);
        Role.OnValueChanged -= RoleChanged;
        Disguise.OnValueChanged -= DisguiseChanged;
        if (disguiseVisual != null) Destroy(disguiseVisual);
    }

    void RoleChanged(PlayerRole previous, PlayerRole current)
    {
        RefreshVisual();
        if (IsOwner && current != PlayerRole.Spectator)
        { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
    }
    void DisguiseChanged(int previous, int current) => RefreshVisual();

    void RefreshVisual()
    {
        if (disguiseVisual != null) Destroy(disguiseVisual);
        var controller = GetComponent<CharacterController>();
        controller.height = originalHeight; controller.radius = originalRadius; controller.center = originalCenter;
        bool disguised = Role.Value == PlayerRole.Prop && Disguise.Value != 0;
        if (normalVisual != null)
            foreach (var renderer in normalVisual.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = Role.Value != PlayerRole.Spectator && !disguised;
        if (!disguised || disguiseRoot == null || Match == null) return;
        var catalog = Match.Map != null && Match.Map.definition != null ? Match.Map.definition.catalog : null;
        if (catalog != null && catalog.TryGet(Disguise.Value, out var prop) && prop.visual != null)
        {
            // shortcut: capsule hitbox per prop; use authored compound hitboxes for irregular silhouettes.
            controller.radius = Mathf.Max(0.1f, prop.disguiseRadius);
            controller.height = Mathf.Max(controller.radius * 2, prop.disguiseHeight);
            controller.center = Vector3.up * controller.height * 0.5f;
            disguiseVisual = Instantiate(prop.visual.gameObject, disguiseRoot);
            disguiseVisual.transform.localPosition = Vector3.zero;
            foreach (var collider in disguiseVisual.GetComponentsInChildren<Collider>()) collider.enabled = false;
        }
    }

    void Update()
    {
        if (!IsSpawned || Match == null) return;
        if (disguiseRoot != null) disguiseRoot.rotation = Quaternion.Euler(0, Rotation.Value, 0);
        if (IsServer && Role.Value == PlayerRole.Prop && Match.Phase.Value == MatchPhase.Hunting &&
            NetworkManager.ServerTime.Time >= nextWhistle)
        {
            nextWhistle = NetworkManager.ServerTime.Time + Mathf.Max(1, Match.Rules.whistleSeconds);
            CueClientRpc(0);
        }
    }

    public void ResetForRound(PlayerRole role, Transform spawn)
    {
        if (!IsServer) return;
        Role.Value = role;
        Health.Value = Mathf.Max(1, Match.Rules.propHealth);
        Disguise.Value = 0;
        Frozen.Value = false;
        Rotation.Value = spawn.eulerAngles.y;
        nextThrow = nextAction = 0;
        nextWhistle = NetworkManager.ServerTime.Time + Match.Rules.hidingSeconds + Mathf.Max(1, Match.Rules.whistleSeconds);
        GetComponent<PlayerController>().Teleport(spawn.position, spawn.rotation);
        TeleportClientRpc(spawn.position, spawn.rotation);
    }

    [ClientRpc]
    void TeleportClientRpc(Vector3 position, Quaternion rotation) => GetComponent<PlayerController>().Teleport(position, rotation);

    [ServerRpc]
    public void DisguiseServerRpc(int id)
    {
        if (!CanActAsProp() || id == 0 || Match.Map == null || Match.Map.definition == null || Match.Map.definition.catalog == null ||
            !Match.Map.definition.catalog.TryGet(id, out _) ||
            !Prop.ExistsNear(id, transform.position, Match.Rules.disguiseDistance)) return;
        Disguise.Value = id;
        CueClientRpc(1);
    }

    bool CanActAsProp()
    {
        if (Match == null || Role.Value != PlayerRole.Prop ||
            (Match.Phase.Value != MatchPhase.Hiding && Match.Phase.Value != MatchPhase.Hunting) ||
            NetworkManager.ServerTime.Time < nextAction) return false;
        nextAction = NetworkManager.ServerTime.Time + 0.1;
        return true;
    }

    [ServerRpc]
    public void FreezeServerRpc() { if (CanActAsProp()) Frozen.Value = !Frozen.Value; }

    [ServerRpc]
    public void RotateServerRpc(bool clockwise)
    {
        if (CanActAsProp()) Rotation.Value = Mathf.Repeat(Rotation.Value + (clockwise ? 15 : -15), 360);
    }

    [ServerRpc]
    public void ThrowServerRpc(Vector3 direction)
    {
        if (!CanHunt || weapon == null || weapon.projectilePrefab == null || NetworkManager.ServerTime.Time < nextThrow ||
            !float.IsFinite(direction.x) || !float.IsFinite(direction.y) || !float.IsFinite(direction.z) || direction.sqrMagnitude < 0.01f) return;
        nextThrow = NetworkManager.ServerTime.Time + Mathf.Max(0.1f, weapon.cooldown);
        var projectile = Instantiate(weapon.projectilePrefab, transform.position + Vector3.up, Quaternion.LookRotation(direction));
        projectile.Launch(this, direction.normalized, weapon);
        projectile.NetworkObject.Spawn();
        CueClientRpc(3);
    }

    public void Hit(int damage)
    {
        if (!IsServer || Role.Value != PlayerRole.Prop || Match.Phase.Value != MatchPhase.Hunting) return;
        Health.Value = Mathf.Max(0, Health.Value - Mathf.Max(1, damage));
        if (Health.Value != 0) return;
        Role.Value = Match.Rules.capturedBecomeHunters ? PlayerRole.Hunter : PlayerRole.Spectator;
        Disguise.Value = 0;
        Frozen.Value = false;
        CueClientRpc(2);
    }

    public void Footstep() { if (audioSource != null && footstepSound != null) audioSource.PlayOneShot(footstepSound); }
    public void JumpSound() { if (audioSource != null && jumpSound != null) audioSource.PlayOneShot(jumpSound); }

    [ClientRpc]
    void CueClientRpc(int cue)
    {
        var clip = cue == 0 ? whistleSound : cue == 1 ? disguiseSound : cue == 2 ? capturedSound : weapon != null ? weapon.throwSound : null;
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }
}
