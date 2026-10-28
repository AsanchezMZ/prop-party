// PlayerController.cs
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
{
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

    void Awake() => cc = GetComponent<CharacterController>();

    public override void OnNetworkSpawn()
    {
        bool mine = IsOwner;
        enabled = mine;                 // Update solo en el jugador local
        cc.enabled = mine;              // en los remotos mueve el NetworkTransform
        playerCamera.enabled = mine;
        listener.enabled = mine;

        if (mine) SetCursorLocked(true);
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner) SetCursorLocked(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            SetCursorLocked(Cursor.lockState != CursorLockMode.Locked);

        bool locked = Cursor.lockState == CursorLockMode.Locked;

        // Ratón: giro horizontal en el cuerpo, vertical en el pivote
        if (locked)
        {
            float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
            float my = Input.GetAxis("Mouse Y") * mouseSensitivity;

            transform.Rotate(0f, mx, 0f);
            pitch = Mathf.Clamp(pitch - my, minPitch, maxPitch);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        // Movimiento
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
        Vector3 move = transform.TransformDirection(input) * speed;

        // Gravedad y salto
        bool grounded = cc.isGrounded;
        if (grounded && verticalVelocity < 0f) verticalVelocity = -2f;
        if (grounded && locked && Input.GetButtonDown("Jump"))
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        verticalVelocity += gravity * Time.deltaTime;

        move.y = verticalVelocity;
        cc.Move(move * Time.deltaTime);

        // Animator (NetworkAnimator replica estos parámetros al resto)
        if (animator != null)
            animator.SetFloat("Speed", animVal, 0.1f, Time.deltaTime);
    }

    static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}