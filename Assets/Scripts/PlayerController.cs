using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Cinemachine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
{
    [Header("Ajustes de Movimiento")]
    public float velocidadCaminar = 5f;
    public float velocidadRotacion = 10f;
    
    private CharacterController controller;
    private Animator animator;
    private Transform camaraPrincipal;

    public override void OnNetworkSpawn()
    {
        // Si este clon no es el nuestro, ignoramos la configuración de cámara
        if (!IsOwner) return;

        // Solo el jugador local busca y se adueña de la cámara en la escena
        CinemachineFreeLook camara = FindObjectOfType<CinemachineFreeLook>();
        if (camara != null)
        {
            camara.Follow = this.transform;
            camara.LookAt = this.transform;
        }
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        // Busca el Animator en el FBX que pusiste como hijo
        animator = GetComponentInChildren<Animator>();
        
        if (Camera.main != null)
        {
            camaraPrincipal = Camera.main.transform;
        }
    }

    void Update()
    {
        // Bloqueo de red: Solo el dueño de este objeto puede controlarlo
        if (!IsOwner) return;

        MoverJugador();
    }

    void MoverJugador()
    {
        // 1. Capturar los inputs (W, A, S, D o Joysticks)
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Vector3 direccion = new Vector3(horizontal, 0f, vertical).normalized;

        if (direccion.magnitude >= 0.1f)
        {
            // 2. Calcular el ángulo hacia donde mirar basado en la cámara
            float anguloObjetivo = Mathf.Atan2(direccion.x, direccion.z) * Mathf.Rad2Deg + camaraPrincipal.eulerAngles.y;
            
            // 3. Suavizar la rotación
            float angulo = Mathf.LerpAngle(transform.eulerAngles.y, anguloObjetivo, Time.deltaTime * velocidadRotacion);
            transform.rotation = Quaternion.Euler(0f, angulo, 0f);

            // 4. Mover al personaje
            Vector3 direccionMovimiento = Quaternion.Euler(0f, anguloObjetivo, 0f) * Vector3.forward;
            controller.Move(direccionMovimiento.normalized * velocidadCaminar * Time.deltaTime);
        }

        // 5. Aplicar gravedad básica para que no flote en pendientes
        controller.Move(Physics.gravity * Time.deltaTime);

        // 6. Actualizar el parámetro de animación (Asegúrate de que tu parámetro en el Animator se llame exactamente "Velocidad")
        if (animator != null)
        {
            animator.SetFloat("Velocidad", direccion.magnitude);
        }
    }
}