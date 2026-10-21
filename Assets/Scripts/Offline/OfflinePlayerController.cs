using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class OfflinePlayerController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] Transform cameraPivot;

    [Header("Movimiento")]
    [SerializeField] float walkSpeed = 5f;
    [SerializeField] float sprintSpeed = 8f;
    [SerializeField] float jumpHeight = 1.2f;
    [SerializeField] float gravity = -20f;

    [Header("Cámara")]
    [SerializeField] float mouseSensitivity = 0.1f;     // ratón (grados por píxel)
    [SerializeField] float gamepadSensitivity = 150f;   // mando (grados por segundo)
    [SerializeField] float maxPitch = 85f;

    InputAction moveAction, lookAction, jumpAction, sprintAction;
    CharacterController controller;
    float pitch;
    float verticalVelocity;

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        moveAction = new InputAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        moveAction.AddBinding("<Gamepad>/leftStick");

        lookAction = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");
        lookAction.AddBinding("<Gamepad>/rightStick");

        jumpAction = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
        jumpAction.AddBinding("<Gamepad>/buttonSouth");

        sprintAction = new InputAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
        sprintAction.AddBinding("<Gamepad>/leftStickPress");
    }

    void OnEnable()
    {
        moveAction.Enable();
        lookAction.Enable();
        jumpAction.Enable();
        sprintAction.Enable();
    }

    void OnDisable()
    {
        moveAction.Disable();
        lookAction.Disable();
        jumpAction.Disable();
        sprintAction.Disable();
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // Escape libera el ratón (útil en el editor); click lo vuelve a capturar
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame
            && Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (Cursor.lockState == CursorLockMode.Locked)
            HandleLook();

        HandleMovement();
    }

    void HandleLook()
    {
        Vector2 look = lookAction.ReadValue<Vector2>();

        // El delta del ratón ya es por frame; el stick del mando hay que multiplicarlo por el tiempo
        bool usingGamepad = lookAction.activeControl?.device is Gamepad;
        Vector2 delta = usingGamepad
            ? look * gamepadSensitivity * Time.deltaTime
            : look * mouseSensitivity;

        transform.Rotate(Vector3.up * delta.x);

        pitch = Mathf.Clamp(pitch - delta.y, -maxPitch, maxPitch);
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void HandleMovement()
    {
        Vector2 move = moveAction.ReadValue<Vector2>();
        Vector3 dir = transform.right * move.x + transform.forward * move.y;
        dir = Vector3.ClampMagnitude(dir, 1f);

        float speed = sprintAction.IsPressed() ? sprintSpeed : walkSpeed;

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;   // pegado al suelo

        if (jumpAction.WasPressedThisFrame() && controller.isGrounded)
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = dir * speed + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }
}