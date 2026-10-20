using UnityEngine;

public class PushObjects : MonoBehaviour
{
    public float fuerzaDeEmpuje = 2.0f;

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;

        // Si chocamos con algo que no tiene Rigidbody o es Kinematic, no hacemos nada
        if (body == null || body.isKinematic)
        {
            return;
        }

        // Evitamos empujar objetos hacia abajo (si los pisamos)
        if (hit.moveDirection.y < -0.3f)
        {
            return;
        }

        // Calculamos la dirección del empuje (solo en horizontal)
        Vector3 direccionEmpuje = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z);

        // Aplicamos la fuerza a la puerta basándonos en la velocidad y masa
        body.linearVelocity = direccionEmpuje * fuerzaDeEmpuje;
    }
}