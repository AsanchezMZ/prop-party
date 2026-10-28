using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [SerializeField] private Transform pivot;          // CameraPivot
    [SerializeField] private float distance = 4f;
    [SerializeField] private float minDistance = 0.6f;
    [SerializeField] private float radius = 0.25f;
    [SerializeField] private float returnSpeed = 8f;
    [SerializeField] private LayerMask obstacles;      // solo geometría del mapa

    private float current;

    void Start() => current = distance;

    void LateUpdate()
    {
        distance = Mathf.Clamp(distance - Input.GetAxis("Mouse ScrollWheel") * 2f, minDistance, 8f);
        Vector3 origin = pivot.position;
        Vector3 dir = -pivot.forward;

        float target = distance;
        if (Physics.SphereCast(origin, radius, dir, out var hit, distance,
                               obstacles, QueryTriggerInteraction.Ignore))
            target = Mathf.Max(hit.distance, minDistance);

        current = target < current ? target : Mathf.Lerp(current, target, returnSpeed * Time.deltaTime);

        transform.position = origin + dir * current;
        transform.rotation = pivot.rotation;
        Debug.Assert(Vector3.Dot(transform.forward, pivot.position - transform.position) > 0f);
    }
}
