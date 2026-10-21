using Unity.Netcode;
using UnityEngine;

public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] private float speed = 5f;
    [SerializeField] private GameObject cameraObject; // arrastra aquí PlayerCamera

    public override void OnNetworkSpawn()
    {
        // La cámara solo existe para el jugador local
        cameraObject.SetActive(IsOwner);

        if (IsOwner)
        {
            // Evita que todos aparezcan en el mismo punto
            transform.position = new Vector3(Random.Range(-3f, 3f), 1f, Random.Range(-3f, 3f));
        }
    }

    void Update()
    {
        if (!IsOwner) return; // solo mi jugador lee mi teclado

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 move = new Vector3(h, 0f, v) * speed * Time.deltaTime;
        transform.Translate(move, Space.Self);
    }
}