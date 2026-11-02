// PlayerController.cs
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
{
    // shortcut: owner-authoritative movement for friend lobbies; use server simulation/prediction for competitive play.
    [Header("Referencias")]
    [SerializeField] Transform cameraPivot;
    [SerializeField] Camera playerCamera;
    [SerializeField] AudioListener listener;
    [SerializeField] Animator animator;

    [Header("Movimiento")]
    [SerializeField] float slowWalkSpeed = 2f;
    [SerializeField] float walkSpeed = 4f;
    [SerializeField] float runSpeed = 7f;
    [SerializeField] float jumpHeight = 1.2f;
    [SerializeField] float gravity = -20f;

    [Header("Cámara")]
    [SerializeField] float mouseSensitivity = 2f;
    [SerializeField] float minPitch = -30f;
    [SerializeField] float maxPitch = 60f;

    CharacterController cc;
    float pitch;
    float verticalVelocity;
    PropHuntPlayer gameplay;
    Transform spectated;
    int spectatorIndex;
    ThirdPersonCamera cameraRig;
    bool ownsCamera, propFirstPerson;
    PlayerRole lastRole = PlayerRole.Spectator;
    MatchPhase lastPhase = MatchPhase.Waiting;
    float nextAim;
    public bool SpectatorMenu { get; private set; }
    public Prop AimedProp { get; private set; }
    public Camera ViewCamera => playerCamera;
    public bool FirstPerson => gameplay != null && (gameplay.Role.Value == PlayerRole.Hunter ||
        (gameplay.Role.Value == PlayerRole.Prop && propFirstPerson));
    public Vector3 EyePosition => transform.TransformPoint(cc.center + Vector3.up * cc.height * 0.4f);

    void Awake()
    {
        cc = GetComponent<CharacterController>(); gameplay = GetComponent<PropHuntPlayer>();
        cameraRig = playerCamera != null ? playerCamera.GetComponent<ThirdPersonCamera>() : null;
        if (playerCamera != null) { playerCamera.enabled = false; playerCamera.nearClipPlane = 0.03f; }
        if (cameraRig != null) cameraRig.enabled = false;
        if (listener != null) listener.enabled = false;
    }

    public override void OnNetworkSpawn()
    {
        bool mine = IsOwner;
        ownsCamera = mine;
        enabled = mine;                 // Update solo en el jugador local
        cc.enabled = true; // Remote colliders are needed by server projectile queries.
        playerCamera.enabled = mine;
        if (cameraRig != null) cameraRig.enabled = mine;
        listener.enabled = mine;
        if (mine) { cameraRig?.ResetPosition(); ApplyCamera(); }
    }

    public override void OnNetworkDespawn()
    {
        if (playerCamera != null) playerCamera.enabled = false;
        if (cameraRig != null) cameraRig.enabled = false;
        if (listener != null) listener.enabled = false;
        if (ownsCamera) SetCursorLocked(false);
        ownsCamera = false;
    }

    void Update()
    {
        if (!IsSpawned || !ownsCamera) return;
        if (gameplay != null && gameplay.Match != null)
        {
            var role = gameplay.Role.Value; var phase = gameplay.Match.Phase.Value;
            if (role != lastRole)
            {
                SpectatorMenu = false;
                spectated = null; cameraRig?.SetTarget(null);
                ApplyCamera();
                SetCursorLocked(role != PlayerRole.Spectator && (phase == MatchPhase.Hiding || phase == MatchPhase.Hunting));
            }
            if (phase != lastPhase)
            {
                if (phase == MatchPhase.Waiting || phase == MatchPhase.Finished) SetCursorLocked(false);
                else if (lastPhase == MatchPhase.Waiting || lastPhase == MatchPhase.Finished)
                    SetCursorLocked(role != PlayerRole.Spectator);
            }
            lastRole = role; lastPhase = phase;
        }
        if (gameplay != null && gameplay.Role.Value == PlayerRole.Spectator)
        {
            cc.enabled = false;
            Spectate();
            return;
        }
        cc.enabled = true;
        if (Input.GetKeyDown(KeyCode.Escape)) SetCursorLocked(false);
        if (Input.GetKeyDown(KeyCode.Tab) && Cursor.lockState != CursorLockMode.Locked && gameplay != null && gameplay.Match != null &&
            (gameplay.Match.Phase.Value == MatchPhase.Hiding || gameplay.Match.Phase.Value == MatchPhase.Hunting))
            SetCursorLocked(true);

        bool locked = Cursor.lockState == CursorLockMode.Locked;
        AimedProp = null; AimedDoor = null;
        if (locked && gameplay != null && gameplay.CanUseDoor) FindAimedTargets();
        if (locked)
        {
            float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
            float my = Input.GetAxis("Mouse Y") * mouseSensitivity;

            transform.Rotate(0f, mx, 0f);
            pitch = Mathf.Clamp(pitch - my, minPitch, maxPitch);
        }
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        cameraPivot.localPosition = cc.center + Vector3.up * cc.height * 0.4f;

        if (gameplay != null && locked)
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                if (AimedDoor != null) gameplay.UseDoorServerRpc(System.Array.IndexOf(gameplay.Match.Map.doors, AimedDoor));
                else if (AimedProp != null) gameplay.DisguiseServerRpc(AimedProp.id);
            }
            if (gameplay.Role.Value == PlayerRole.Prop)
            {
                if (Input.GetKeyDown(KeyCode.V)) TogglePerspective();
                if (Input.GetKeyDown(KeyCode.G)) gameplay.ManualWhistleServerRpc();
                if (Input.GetKeyDown(KeyCode.F)) gameplay.FreezeServerRpc();
                if (Input.GetKeyDown(KeyCode.Q)) gameplay.RotateServerRpc(false);
                if (Input.GetKeyDown(KeyCode.R)) gameplay.RotateServerRpc(true);
            }
            else
            {
                if (gameplay.CanHunt && Input.GetMouseButtonDown(0)) gameplay.ThrowServerRpc(cameraPivot.forward);
                if (gameplay.CanHunt && Time.unscaledTime >= nextAim)
                { nextAim = Time.unscaledTime + 0.1f; gameplay.AimServerRpc(cameraPivot.forward); }
            }
        }

        if (gameplay != null && !gameplay.CanMove)
        {
            verticalVelocity = 0;
            if (animator != null) animator.SetFloat("Speed", 0);
            return;
        }

        Vector3 input = locked
            ? new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"))
            : Vector3.zero;
        input = Vector3.ClampMagnitude(input, 1f);

        bool slow = Input.GetKey(KeyCode.LeftControl);
        bool running = Input.GetKey(KeyCode.LeftShift);
        float speed = walkSpeed;
        float animVal = 0;
        if(input == Vector3.zero)
        {
            
        }
        else if (slow)
        {
            speed = slowWalkSpeed;
            animVal = 1;
        }else if (!running)
        {
            speed = walkSpeed;
            animVal = 2;
        }
        else
        {
            speed = runSpeed;
            animVal = 3;
        }
        Vector3 direction = transform.TransformDirection(input);
        Vector3 move = direction * speed;

        // Gravedad y salto
        bool grounded = cc.isGrounded;
        if (grounded && verticalVelocity < 0f) verticalVelocity = -2f;
        if (grounded && locked && Input.GetButtonDown("Jump"))
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            if (gameplay != null) gameplay.JumpSound();
        }
        verticalVelocity += gravity * Time.deltaTime;

        move.y = verticalVelocity;
        cc.Move(move * Time.deltaTime);

        // Animator (NetworkAnimator replica estos parámetros al resto)
        if (animator != null)
            animator.SetFloat("Speed", animVal, 0.1f, Time.deltaTime);
    }

    public ApartmentDoor AimedDoor { get; private set; }

    void FindAimedTargets()
    {
        var hits = Physics.RaycastAll(playerCamera.transform.position, playerCamera.transform.forward, 10,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (hit.collider.GetComponentInParent<PropHuntPlayer>() == gameplay) continue;
            var door = hit.collider.GetComponentInParent<ApartmentDoor>();
            if (door != null && gameplay.Match.Map != null &&
                System.Array.IndexOf(gameplay.Match.Map.doors, door) >= 0 &&
                Vector3.Distance(hit.point, EyePosition) <= 3)
            { AimedDoor = door; return; }
            var prop = hit.collider.GetComponentInParent<Prop>();
            if (gameplay.Role.Value == PlayerRole.Prop && prop != null && prop.id != 0 && gameplay.Match.Map != null && gameplay.Match.Map.definition != null &&
                gameplay.Match.Map.definition.catalog != null && gameplay.Match.Map.definition.catalog.TryGet(prop.id, out _) &&
                Vector3.Distance(prop.transform.position, transform.position) <= gameplay.Match.Rules.disguiseDistance) AimedProp = prop;
            return;
        }
    }

    public void Teleport(Vector3 position, Quaternion rotation)
    {
        cc.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        cc.enabled = true;
        verticalVelocity = 0;
        if (IsOwner && IsSpawned && TryGetComponent<NetworkTransform>(out var networkTransform))
            networkTransform.Teleport(position, rotation, transform.localScale);
        spectated = null;
        if (ownsCamera) { cameraRig?.SetTarget(null); cameraRig?.ResetPosition(); ApplyCamera(); }
    }

    void ApplyCamera()
    {
        cameraRig?.SetFirstPerson(FirstPerson);
        if (gameplay != null) gameplay.SetFirstPersonVisual(FirstPerson);
    }

    public void TogglePerspective()
    {
        if (!ownsCamera || gameplay == null || gameplay.Role.Value != PlayerRole.Prop) return;
        propFirstPerson = !propFirstPerson;
        ApplyCamera();
    }

    void Spectate()
    {
        if (Cursor.lockState != CursorLockMode.None) SetCursorLocked(false);
        if (Input.GetKeyDown(KeyCode.Escape)) SpectatorMenu = !SpectatorMenu;
        if (Input.GetKeyDown(KeyCode.Tab)) SpectatorMenu = false;
        if (gameplay.Match == null) return;
        var players = gameplay.Match.Players;
        if (Input.GetKeyDown(KeyCode.RightArrow)) { spectatorIndex++; spectated = null; }
        if (spectated != null)
        {
            var target = spectated.GetComponent<PropHuntPlayer>();
            if (target == null || target.Role.Value == PlayerRole.Spectator) spectated = null;
        }
        if (spectated == null && players.Count > 0)
            for (int i = 0; i < players.Count; i++)
            {
                var candidate = players[(spectatorIndex + i) % players.Count];
                if (candidate == this.gameplay || candidate.Role.Value == PlayerRole.Spectator) continue;
                spectated = candidate.transform;
                spectatorIndex = (spectatorIndex + i) % players.Count;
                break;
            }
        cameraRig?.SetTarget(spectated);
    }

    static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
