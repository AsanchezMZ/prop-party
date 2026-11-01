// PlayerController.cs
using Unity.Netcode;
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
    float nextStep;

    void Awake() { cc = GetComponent<CharacterController>(); gameplay = GetComponent<PropHuntPlayer>(); }

    public override void OnNetworkSpawn()
    {
        bool mine = IsOwner;
        enabled = mine;                 // Update solo en el jugador local
        cc.enabled = true; // Remote colliders are needed by server projectile queries.
        playerCamera.enabled = mine;
        playerCamera.GetComponent<ThirdPersonCamera>().enabled = mine;
        listener.enabled = mine;
        if (mine) SetCursorLocked(true);
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner) SetCursorLocked(false);
    }

    void Update()
    {
        if (gameplay != null && gameplay.Role.Value == PlayerRole.Spectator)
        {
            cc.enabled = false;
            Spectate();
            return;
        }
        cc.enabled = true;
        if (Input.GetKeyDown(KeyCode.Escape)) SetCursorLocked(false);
        if (Input.GetKeyDown(KeyCode.Tab) && Cursor.lockState != CursorLockMode.Locked)
            SetCursorLocked(true);

        bool locked = Cursor.lockState == CursorLockMode.Locked;
        if (locked)
        {
            float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
            float my = Input.GetAxis("Mouse Y") * mouseSensitivity;

            transform.Rotate(0f, mx, 0f);
            pitch = Mathf.Clamp(pitch - my, minPitch, maxPitch);
        }
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        if (gameplay != null && locked)
        {
            if (gameplay.Role.Value == PlayerRole.Prop)
            {
                if (Input.GetKeyDown(KeyCode.F)) gameplay.FreezeServerRpc();
                if (Input.GetKeyDown(KeyCode.Q)) gameplay.RotateServerRpc(false);
                if (Input.GetKeyDown(KeyCode.R)) gameplay.RotateServerRpc(true);
                if (Input.GetKeyDown(KeyCode.E) && Physics.Raycast(playerCamera.transform.position,
                    playerCamera.transform.forward, out var hit, 10, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    var prop = hit.collider.GetComponentInParent<Prop>();
                    if (prop != null) gameplay.DisguiseServerRpc(prop.id);
                }
            }
            else if (Input.GetMouseButtonDown(0)) gameplay.ThrowServerRpc(playerCamera.transform.forward);
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
        if (grounded && input.sqrMagnitude > 0 && Time.time >= nextStep)
        {
            nextStep = Time.time + (running ? 0.3f : 0.5f);
            if (gameplay != null) gameplay.Footstep();
        }

        // Animator (NetworkAnimator replica estos parámetros al resto)
        if (animator != null)
            animator.SetFloat("Speed", animVal, 0.1f, Time.deltaTime);
    }

    public void Teleport(Vector3 position, Quaternion rotation)
    {
        cc.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        cc.enabled = true;
        verticalVelocity = 0;
        if (IsOwner) SetCursorLocked(true);
        spectated = null;
        if (playerCamera != null) playerCamera.GetComponent<ThirdPersonCamera>().SetTarget(null);
    }

    void Spectate()
    {
        SetCursorLocked(false);
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
        playerCamera.GetComponent<ThirdPersonCamera>().SetTarget(spectated);
    }

    static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
