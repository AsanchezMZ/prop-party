// SpawnPoint.cs
using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawRay(transform.position, transform.forward);
    }
}