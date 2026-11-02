using UnityEngine;

public class PushObjects : MonoBehaviour
{
    public float fuerzaDeEmpuje = 2.0f;
    float nextDoorSound;

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;
        var player = GetComponent<PropHuntPlayer>();
        if (player != null && (!player.IsOwner || !player.CanMove)) return;

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
        if (body.GetComponent<HingeJoint>() != null && Time.unscaledTime >= nextDoorSound)
        {
            nextDoorSound = Time.unscaledTime + 0.6f;
            if (player != null && player.IsSpawned) player.DoorKnockServerRpc(hit.point);
            else DoorAudio.PlayAt(hit.point);
        }
    }
}
