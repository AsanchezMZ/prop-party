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
    float yaw;
    float pitch;
    float verticalVelocity;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        yaw = transform.eulerAngles.y;
    }

    public override void OnNetworkSpawn()
    {
        bool mine = IsOwner;
        enabled = mine;                 // Update solo en el jugador local
        cc.enabled = mine;              // en los remotos mueve el NetworkTransform
        playerCamera.enabled = mine;
        playerCamera.GetComponent<ThirdPersonCamera>().enabled = mine;
        listener.enabled = mine;
    }

    void Update()
    {
        if (Input.GetMouseButton(1))
        {
            float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
            float my = Input.GetAxis("Mouse Y") * mouseSensitivity;

            yaw += mx;
            pitch = Mathf.Clamp(pitch - my, minPitch, maxPitch);
        }

        Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
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
        Vector3 direction = Quaternion.Euler(0f, yaw, 0f) * input;
        if (direction.sqrMagnitude > 0f)
            transform.rotation = Quaternion.LookRotation(direction);
        cameraPivot.rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 move = direction * speed;

        // Gravedad y salto
        bool grounded = cc.isGrounded;
        if (grounded && verticalVelocity < 0f) verticalVelocity = -2f;
        if (grounded && Input.GetButtonDown("Jump"))
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        verticalVelocity += gravity * Time.deltaTime;

        move.y = verticalVelocity;
        cc.Move(move * Time.deltaTime);

        // Animator (NetworkAnimator replica estos parámetros al resto)
        if (animator != null)
            animator.SetFloat("Speed", animVal, 0.1f, Time.deltaTime);
    }
}
