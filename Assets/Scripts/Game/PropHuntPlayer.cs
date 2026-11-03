using Unity.Netcode;
using Unity.Collections;
using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class PropHuntPlayer : NetworkBehaviour
{
    [SerializeField] Transform disguiseRoot;
    [SerializeField] GameObject normalVisual;
    [SerializeField] ThrowableDefinition weapon;
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip disguiseSound;
    [SerializeField] AudioClip capturedSound;
    [SerializeField] AudioClip jumpSound;
    [SerializeField] AudioClip footstepSound;
    [SerializeField] AudioClip[] footstepClips;
    [SerializeField, Min(0.5f)] float stepDistance = 1.8f;
    [SerializeField] Material effectMaterial;
    public const float ManualWhistleCooldown = 5;
    public readonly NetworkVariable<int> Score = new();
    public readonly NetworkVariable<FixedString32Bytes> DisplayName = new();
    public readonly NetworkVariable<bool> HunterNearby = new();
    public readonly NetworkVariable<bool> InHunterSight = new();
    public readonly NetworkVariable<double> NextManualWhistle = new();
    public readonly NetworkVariable<double> NextAutomaticWhistle = new();
    public readonly NetworkVariable<double> AutomaticWhistleStartedAt = new();
    public readonly NetworkVariable<Vector3> AimDirection = new(Vector3.forward);
    public event System.Action<int> Cue;
    public readonly NetworkVariable<PlayerRole> Role = new(PlayerRole.Spectator);
    public readonly NetworkVariable<int> Disguise = new();
    public readonly NetworkVariable<bool> Frozen = new();
    public readonly NetworkVariable<float> Rotation = new();
    public readonly NetworkVariable<int> Health = new();
    public readonly NetworkVariable<int> HunterMissesRemaining = new();
    public readonly NetworkVariable<bool> AssetsReady = new();
    public PropHuntMatch Match { get; private set; }
    public bool CanHunt => Match != null && Match.Phase.Value == MatchPhase.Hunting && Role.Value == PlayerRole.Hunter;
    public bool CanUseDoor => Match != null &&
        ((Match.Phase.Value == MatchPhase.Hunting && (Role.Value == PlayerRole.Hunter || Role.Value == PlayerRole.Prop)) ||
         (Match.Phase.Value == MatchPhase.Hiding && Role.Value == PlayerRole.Prop));
    public bool CanMove => Match != null && (Match.Phase.Value == MatchPhase.Hunting ||
        (Match.Phase.Value == MatchPhase.Hiding && Role.Value == PlayerRole.Prop)) && !Frozen.Value;
    public bool CanRotateDisguise => Match != null && Role.Value == PlayerRole.Prop && Disguise.Value != 0 &&
        (Match.Phase.Value == MatchPhase.Hiding || Match.Phase.Value == MatchPhase.Hunting);
    GameObject disguiseVisual;
    bool hasDisguiseColliders;
    public bool IgnoreMovementHitbox(Collider collider) => hasDisguiseColliders && collider is CharacterController;
    double nextThrow, nextAction;
    double nextDoorSound;
    double nextFineRotation;
    double nearSince, seenSince, nextSense, nextAim;
    Vector3 lastStepPosition;
    float travelled;
    bool firstPersonVisual;
    PlayerController movement;
    AudioSource whistleSource;
    float originalHeight, originalRadius;
    Vector3 originalCenter;

    void Awake()
    {
        var controller = GetComponent<CharacterController>();
        movement = GetComponent<PlayerController>();
        whistleSource = gameObject.AddComponent<AudioSource>();
        whistleSource.playOnAwake = false; whistleSource.spatialBlend = 1;
        if (audioSource != null)
        {
            whistleSource.minDistance = audioSource.minDistance;
            whistleSource.maxDistance = audioSource.maxDistance;
            whistleSource.rolloffMode = audioSource.rolloffMode;
        }
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
        lastStepPosition = transform.position;
        if (IsOwner) NameServerRpc(new FixedString32Bytes(LocalProfile.PlayerName));
        if (IsOwner) StartCoroutine(ReportAssetsReady());
    }

    System.Collections.IEnumerator ReportAssetsReady()
    {
        while (IsSpawned && !PartyLoadingScreen.Ready) yield return null;
        if (IsSpawned) AssetsReadyServerRpc();
    }

    [ServerRpc]
    void AssetsReadyServerRpc() => AssetsReady.Value = true;

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
    }
    void DisguiseChanged(int previous, int current) => RefreshVisual();

    void RefreshVisual()
    {
        if (disguiseVisual != null) { disguiseVisual.SetActive(false); Destroy(disguiseVisual); }
        hasDisguiseColliders = false;
        var controller = GetComponent<CharacterController>();
        controller.radius = originalRadius; controller.height = originalHeight; controller.center = originalCenter;
        bool disguised = Role.Value == PlayerRole.Prop && Disguise.Value != 0;
        if (normalVisual != null)
            foreach (var renderer in normalVisual.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = Role.Value != PlayerRole.Spectator && !disguised;
                renderer.forceRenderingOff = IsOwner && firstPersonVisual;
            }
        if (!disguised || disguiseRoot == null || Match == null) return;
        var catalog = Match.Map != null && Match.Map.definition != null ? Match.Map.definition.catalog : null;
        if (catalog != null && catalog.TryGet(Disguise.Value, out var prop) && prop.visual != null)
        {
            // shortcut: locomotion stays capsule-shaped; replace CharacterController for shape-accurate movement.
            prop.ValidateDisguiseDimensions();
            controller.radius = prop.disguiseRadius;
            controller.height = prop.disguiseHeight;
            controller.center = Vector3.up * controller.height * 0.5f;
            disguiseVisual = Instantiate(prop.visual.gameObject, disguiseRoot);
            disguiseVisual.transform.localPosition = Vector3.zero;
            hasDisguiseColliders = prop.ConfigureDisguiseColliders(disguiseVisual);
            foreach (var collider in disguiseVisual.GetComponentsInChildren<Collider>())
                if (collider.enabled) Physics.IgnoreCollision(controller, collider);
            foreach (var renderer in disguiseVisual.GetComponentsInChildren<Renderer>()) renderer.forceRenderingOff = IsOwner && firstPersonVisual;
        }
    }

    void Update()
    {
        if (!IsSpawned || Match == null) return;
        if (disguiseRoot != null) disguiseRoot.rotation = Quaternion.Euler(0, Rotation.Value, 0);
        if (!IsServer) return;
        double now = NetworkManager.ServerTime.Time;
        if (Role.Value == PlayerRole.Prop && Match.Phase.Value == MatchPhase.Hunting && now >= NextAutomaticWhistle.Value)
        {
            var settings = PartyAudio.Settings;
            int index = settings != null ? settings.PickWhistleIndex() : -1;
            var clip = settings != null ? settings.WhistleAt(index) : null;
            if (Match.ReserveAutomaticWhistle(clip != null ? clip.length : 0))
            {
                AutomaticWhistleStartedAt.Value = now;
                NextAutomaticWhistle.Value = now + Mathf.Max(35, Match.Settings.Value.WhistleSeconds) + Random.Range(0, 12);
                CueClientRpc(0, index);
            }
            else NextAutomaticWhistle.Value = now + 2;
        }
        if (now >= nextSense)
        {
            nextSense = now + 0.1;
            UpdateRisk(now);
            UpdateSteps();
        }
    }

    void UpdateRisk(double now)
    {
        bool nearby = false, visible = false;
        if (Role.Value == PlayerRole.Prop && Match.Phase.Value == MatchPhase.Hunting)
            foreach (var hunter in Match.Players)
            {
                if (hunter.Role.Value != PlayerRole.Hunter) continue;
                float distance = Vector3.Distance(transform.position, hunter.transform.position);
                nearby |= distance <= Mathf.Max(1, Match.Rules.proximityRadius);
                if (distance > Mathf.Max(1, Match.Rules.visibilityDistance)) continue;
                var origin = hunter.movement.EyePosition;
                var target = GetComponent<CharacterController>().bounds.center;
                var delta = target - origin;
                if (!InView(hunter.AimDirection.Value, delta, Match.Rules.hunterFieldOfView)) continue;
                var hits = Physics.RaycastAll(origin, delta.normalized, delta.magnitude + 0.05f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                bool blocked = false;
                foreach (var hit in hits)
                {
                    var player = hit.collider.GetComponentInParent<PropHuntPlayer>();
                    if (player == hunter || (player != null && player.Role.Value == PlayerRole.Spectator)) continue;
                    blocked = player != this;
                    break;
                }
                if (!blocked) visible = true;
            }
        if (!nearby) nearSince = now;
        if (!visible) seenSince = now;
        if (nearby && !HunterNearby.Value) nearSince = now;
        if (visible && !InHunterSight.Value) seenSince = now;
        HunterNearby.Value = nearby; InHunterSight.Value = visible;
        // Each continuous exposure has its own clock; multiple hunters never multiply the award.
        if (nearby && now - nearSince >= 3) { AddScore(Match.Rules.proximityPoints); nearSince = now; }
        if (visible && now - seenSince >= 1) { AddScore(Match.Rules.visiblePoints); seenSince = now; }
    }

    public static bool InView(Vector3 aim, Vector3 delta, float fov) => delta.sqrMagnitude < 0.0001f ||
        Vector3.Dot(aim.normalized, delta.normalized) >= Mathf.Cos(Mathf.Clamp(fov, 20, 120) * Mathf.Deg2Rad * 0.5f);

    public void AddScore(int points)
    {
        if (IsServer && points > 0) Score.Value = (int)System.Math.Min(int.MaxValue, (long)Score.Value + points);
    }

    [ServerRpc]
    void NameServerRpc(FixedString32Bytes value) { DisplayName.Value = value; }

    [ServerRpc]
    public void AimServerRpc(Vector3 direction)
    {
        if (!CanHunt || NetworkManager.ServerTime.Time < nextAim || !ValidDirection(direction)) return;
        nextAim = NetworkManager.ServerTime.Time + 0.08;
        AimDirection.Value = direction.normalized;
    }

    static bool ValidDirection(Vector3 direction) => float.IsFinite(direction.x) && float.IsFinite(direction.y) &&
        float.IsFinite(direction.z) && direction.sqrMagnitude > 0.01f && direction.sqrMagnitude < 4;

    [ServerRpc]
    public void ManualWhistleServerRpc()
    {
        double now = NetworkManager.ServerTime.Time;
        if (Role.Value != PlayerRole.Prop || Match == null || Match.Phase.Value != MatchPhase.Hunting || now < NextManualWhistle.Value) return;
        NextManualWhistle.Value = now + ManualWhistleCooldown;
        AddScore(Match.Rules.manualWhistlePoints);
        PlayWhistle(true);
    }

    void PlayWhistle(bool manual)
    {
        // The server picks once so every listener hears the same clip.
        var settings = PartyAudio.Settings;
        CueClientRpc(manual ? 5 : 0, settings != null ? settings.PickWhistleIndex() : -1);
    }

    public void SetFirstPersonVisual(bool value)
    {
        if (!IsOwner || firstPersonVisual == value) return;
        firstPersonVisual = value;
        if (normalVisual != null)
            foreach (var renderer in normalVisual.GetComponentsInChildren<Renderer>(true)) renderer.forceRenderingOff = value;
        if (disguiseVisual != null)
            foreach (var renderer in disguiseVisual.GetComponentsInChildren<Renderer>(true)) renderer.forceRenderingOff = value;
    }

    public void ResetForRound(PlayerRole role, Transform spawn)
    {
        if (!IsServer) return;
        Role.Value = role;
        Health.Value = Match.Settings.Value.Health;
        HunterMissesRemaining.Value = Match.Settings.Value.HunterMissLimit;
        Score.Value = 0; HunterNearby.Value = InHunterSight.Value = false;
        NextManualWhistle.Value = 0;
        nearSince = seenSince = NetworkManager.ServerTime.Time;
        AimDirection.Value = spawn.forward;
        Disguise.Value = 0;
        Frozen.Value = false;
        Rotation.Value = spawn.eulerAngles.y;
        nextThrow = nextAction = 0;
        nextFineRotation = 0;
        AutomaticWhistleStartedAt.Value = NetworkManager.ServerTime.Time + Match.Settings.Value.HidingSeconds;
        NextAutomaticWhistle.Value = AutomaticWhistleStartedAt.Value + Mathf.Max(40, Match.Settings.Value.WhistleSeconds) + Random.Range(0, 12);
        travelled = 0; lastStepPosition = spawn.position;
        GetComponent<PlayerController>().Teleport(spawn.position, spawn.rotation);
        TeleportClientRpc(spawn.position, spawn.rotation);
    }

    [ClientRpc]
    void TeleportClientRpc(Vector3 position, Quaternion rotation)
    {
        if (!IsServer) GetComponent<PlayerController>().Teleport(position, rotation);
    }

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
    public void FreezeServerRpc()
    {
        if (!CanActAsProp()) return;
        Frozen.Value = !Frozen.Value;
        CueClientRpc(Frozen.Value ? 7 : 8);
    }

    [ServerRpc]
    public void RotateServerRpc(bool clockwise)
    {
        if (!CanActAsProp()) return;
        Rotation.Value = Mathf.Repeat(Rotation.Value + (clockwise ? 15 : -15), 360);
        CueClientRpc(9);
    }

    [ServerRpc]
    public void FineRotateServerRpc(float delta)
    {
        if (!CanRotateDisguise || !float.IsFinite(delta) || Mathf.Abs(delta) > 15 || NetworkManager.ServerTime.Time < nextFineRotation) return;
        nextFineRotation = NetworkManager.ServerTime.Time + 0.04;
        Rotation.Value = Mathf.Repeat(Rotation.Value + delta, 360);
    }

    [ServerRpc]
    public void ThrowServerRpc(Vector3 direction)
    {
        if (!CanHunt || weapon == null || weapon.projectilePrefab == null || NetworkManager.ServerTime.Time < nextThrow || !ValidDirection(direction)) return;
        nextThrow = NetworkManager.ServerTime.Time + Mathf.Max(0.1f, weapon.cooldown);
        var projectile = Instantiate(weapon.projectilePrefab, movement.EyePosition, Quaternion.LookRotation(direction));
        projectile.Launch(this, direction.normalized, weapon);
        projectile.NetworkObject.Spawn();
        CueClientRpc(3);
    }

    [ServerRpc]
    public void UseDoorServerRpc(int index)
    {
        if (CanUseDoor) Match.TryToggleDoor(index, this, movement.EyePosition);
    }

    [ServerRpc]
    public void DoorKnockServerRpc(Vector3 point)
    {
        if (!CanMove || !float.IsFinite(point.x) || !float.IsFinite(point.y) || !float.IsFinite(point.z) ||
            Vector3.Distance(point, transform.position) > 4 || NetworkManager.ServerTime.Time < nextDoorSound) return;
        foreach (var collider in Physics.OverlapSphere(point, 0.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            var body = collider.attachedRigidbody;
            if (body == null || body.isKinematic || body.GetComponent<HingeJoint>() == null) continue;
            nextDoorSound = NetworkManager.ServerTime.Time + 0.6;
            DoorKnockClientRpc(point); break;
        }
    }

    [ClientRpc]
    void DoorKnockClientRpc(Vector3 point) => DoorAudio.PlayAt(point);

    public void HitWrongProp()
    {
        if (!IsServer || !CanHunt || Match.Settings.Value.HunterMissLimit == 0 || HunterMissesRemaining.Value <= 0) return;
        HunterMissesRemaining.Value--;
        CueClientRpc(HunterMissesRemaining.Value == 0 ? 11 : 10);
        if (HunterMissesRemaining.Value > 0) return;
        Role.Value = PlayerRole.Spectator;
        Frozen.Value = false;
    }

    public void Hit(int damage, PropHuntPlayer hunter = null)
    {
        if (!IsServer || Role.Value != PlayerRole.Prop || Match.Phase.Value != MatchPhase.Hunting) return;
        Health.Value = Mathf.Max(0, Health.Value - Mathf.Max(1, damage));
        CueClientRpc(6);
        if (hunter != null && hunter != this && hunter.CanHunt) hunter.ConfirmHitClientRpc();
        if (Health.Value != 0) return;
        if (hunter != null && hunter != this && hunter.CanHunt) hunter.AddScore(Match.Rules.capturePoints);
        Role.Value = Match.Settings.Value.ConvertCaptured ? PlayerRole.Hunter : PlayerRole.Spectator;
        HunterMissesRemaining.Value = Match.Settings.Value.HunterMissLimit;
        HunterNearby.Value = InHunterSight.Value = false;
        Disguise.Value = 0;
        Frozen.Value = false;
        CueClientRpc(2);
    }

    void UpdateSteps()
    {
        var delta = transform.position - lastStepPosition; delta.y = 0;
        lastStepPosition = transform.position;
        if (!CanHunt || delta.magnitude > 3) { travelled = 0; return; }
        var hits = Physics.RaycastAll(GetComponent<CharacterController>().bounds.center, Vector3.down,
            GetComponent<CharacterController>().height * 0.5f + 0.15f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        bool grounded = false;
        foreach (var hit in hits) if (hit.collider.GetComponentInParent<PropHuntPlayer>() == null) { grounded = true; break; }
        if (!grounded) { travelled = 0; return; }
        travelled += delta.magnitude;
        if (travelled >= Mathf.Max(0.5f, stepDistance)) { travelled = 0; CueClientRpc(4); }
    }
    public void JumpSound()
    {
        var clip = jumpSound != null ? jumpSound : PartyAudio.Settings?.jump;
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip, PartyAudio.Settings != null ? PartyAudio.Settings.EffectsVolume : GamePreferences.EffectsVolume);
    }

    [ClientRpc]
    void ConfirmHitClientRpc()
    {
        if (IsOwner) { PartyAudio.Instance?.Play(PartyAudio.Settings?.hitConfirmation); Cue?.Invoke(12); }
    }

    [ClientRpc]
    void CueClientRpc(int cue, int whistleIndex = -1)
    {
        var settings = PartyAudio.Settings;
        var steps = footstepClips != null && footstepClips.Length > 0 ? footstepClips : footstepSound != null ? null : settings?.footsteps;
        var clip = cue == 0 || cue == 5 ? settings?.WhistleAt(whistleIndex) : cue == 1 ? disguiseSound : cue == 2 ? capturedSound :
            cue == 4 ? (steps != null && steps.Length > 0 ? steps[Random.Range(0, steps.Length)] : footstepSound) :
            cue == 3 && weapon != null ? weapon.throwSound : null;
        if (clip == null && settings != null)
            clip = cue switch
            {
                1 => settings.disguise, 2 => settings.captured,
                3 => settings.throwSound, 6 => settings.hurt,
                7 => settings.freeze, 8 => settings.freeze, 9 => settings.rotate, _ => null
            };
        if ((cue == 0 || cue == 5) && clip != null)
        {
            whistleSource.clip = clip;
            whistleSource.volume = settings != null ? settings.EffectsVolume : GamePreferences.EffectsVolume;
            whistleSource.Play();
        }
        else if (audioSource != null && clip != null) audioSource.PlayOneShot(clip, settings != null ? settings.EffectsVolume : GamePreferences.EffectsVolume);
        Vector3 effectPosition = transform.position + Vector3.up * 0.5f;
        if (cue == 1) PartyVfx.Burst(effectPosition, new Color(0.2f, 0.9f, 0.75f), false, effectMaterial);
        else if (cue == 2) PartyVfx.Capture(effectPosition, effectMaterial);
        else if (cue == 6) PartyVfx.Hurt(effectPosition, effectMaterial);
        if (IsOwner) Cue?.Invoke(cue);
    }
}
