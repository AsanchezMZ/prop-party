// SpawnPoint.cs
using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    public enum Team { Any, Props, Hunters }
    public Team team;
    public bool Supports(PlayerRole role) => team == Team.Any ||
        (team == Team.Props && role == PlayerRole.Prop) || (team == Team.Hunters && role == PlayerRole.Hunter);
    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawRay(transform.position, transform.forward);
    }
}
