using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [SerializeField] Transform pivot;
    [SerializeField] float distance = 4;
    [SerializeField] float minDistance = 1.8f;
    [SerializeField] float radius = 0.2f;
    [SerializeField] float returnSpeed = 8;
    [SerializeField] LayerMask obstacles = ~0;
    Transform spectatorTarget;
    Transform playerRoot;
    bool firstPerson;
    float current;

    void Awake() { playerRoot = GetComponentInParent<PlayerController>()?.transform; current = distance; }

    public void SetTarget(Transform target)
    {
        if (spectatorTarget == target) return;
        spectatorTarget = target;
        current = distance;
    }

    public void SetFirstPerson(bool value)
    {
        if (firstPerson == value) return;
        firstPerson = value;
        current = distance;
    }

    public void ResetPosition() => current = distance;

    void LateUpdate()
    {
        if (pivot == null) return;
        bool spectating = spectatorTarget != null;
        Vector3 origin = spectating ? spectatorTarget.position + Vector3.up * 1.5f : pivot.position;
        Vector3 forward = spectating ? spectatorTarget.forward : pivot.forward;
        if (firstPerson && !spectating)
        {
            transform.SetPositionAndRotation(origin, pivot.rotation);
            return;
        }
        if (Cursor.lockState == CursorLockMode.Locked)
            distance = Mathf.Clamp(distance - Input.GetAxis("Mouse ScrollWheel") * 10, minDistance, 8);
        float target = distance;
        var hits = Physics.SphereCastAll(origin, radius, -forward, distance, obstacles, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            if (playerRoot != null && hit.transform.IsChildOf(playerRoot)) continue;
            if (spectating && hit.transform.IsChildOf(spectatorTarget)) continue;
            target = Mathf.Min(target, Mathf.Max(0.05f, hit.distance - 0.05f));
        }
        current = target < current ? target : Mathf.Lerp(current, target, 1 - Mathf.Exp(-returnSpeed * Time.deltaTime));
        transform.SetPositionAndRotation(origin - forward * current, Quaternion.LookRotation(forward));
    }
}
