using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] private float speed = 5f;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private GameObject cameraObject;  // PlayerCamera
    [SerializeField] private Transform cameraPivot;

    private CharacterController controller;
    private float pitch;
    private float verticalVelocity;

    void Awake() => controller = GetComponent<CharacterController>();

    public override void OnNetworkSpawn()
    {
        cameraObject.SetActive(IsOwner);
        controller.enabled = IsOwner;

        if (IsOwner)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Update()
    {
        if (!IsOwner) return;

        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity;
        transform.Rotate(0f, mx, 0f);
        pitch = Mathf.Clamp(pitch - my, -40f, 70f);
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        Vector3 input = new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical"));
        Vector3 move = transform.TransformDirection(Vector3.ClampMagnitude(input, 1f)) * speed;

        verticalVelocity = controller.isGrounded ? -1f : verticalVelocity + gravity * Time.deltaTime;
        move.y = verticalVelocity;

        controller.Move(move * Time.deltaTime);
    }
}