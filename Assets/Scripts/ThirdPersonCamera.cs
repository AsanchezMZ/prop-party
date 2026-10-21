using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [SerializeField] private Transform pivot;          // CameraPivot
    [SerializeField] private float distance = 4f;
    [SerializeField] private float minDistance = 0.6f;
    [SerializeField] private float radius = 0.25f;
    [SerializeField] private float shoulderOffset = 0.4f; // 0 = centrada
    [SerializeField] private float returnSpeed = 8f;
    [SerializeField] private LayerMask obstacles;      // solo geometría del mapa

    private float current;

    void Start() => current = distance;

    void LateUpdate()
    {
        Vector3 origin = pivot.position + pivot.right * shoulderOffset;
        Vector3 dir = -pivot.forward;

        float target = distance;
        if (Physics.SphereCast(origin, radius, dir, out var hit, distance,
                               obstacles, QueryTriggerInteraction.Ignore))
            target = Mathf.Max(hit.distance, minDistance);

        // Se acerca de golpe al chocar, y vuelve suave al liberarse
        current = target < current ? target : Mathf.Lerp(current, target, returnSpeed * Time.deltaTime);

        transform.position = origin + dir * current;
        transform.rotation = pivot.rotation;
    }
}