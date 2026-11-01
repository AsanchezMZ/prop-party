using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [SerializeField] private Transform pivot;          // CameraPivot
    [SerializeField] private float distance = 4f;
    [SerializeField] private float minDistance = 1.8f;
    [SerializeField] private float radius = 0.25f;
    [SerializeField] private float returnSpeed = 8f;
    [SerializeField] private LayerMask obstacles;      // solo geometría del mapa

    private float current;
    Transform spectatorTarget;
    public void SetTarget(Transform target) => spectatorTarget = target;

    void Start() => current = distance;

    void LateUpdate()
    {
        distance = Mathf.Clamp(distance - Input.GetAxis("Mouse ScrollWheel") * 10f, minDistance, 8f);
        Vector3 origin = spectatorTarget != null ? spectatorTarget.position + Vector3.up * 1.5f : pivot.position;
        Vector3 dir = spectatorTarget != null ? -spectatorTarget.forward : -pivot.forward;

        float target = distance;
        if (Physics.SphereCast(origin, radius, dir, out var hit, distance,
                               obstacles, QueryTriggerInteraction.Ignore))
            target = Mathf.Max(0.35f, hit.distance - 0.1f);

        current = target < current ? target : Mathf.Lerp(current, target, returnSpeed * Time.deltaTime);

        transform.position = origin + dir * current;
        transform.rotation = Quaternion.LookRotation(-dir);
    }
}
