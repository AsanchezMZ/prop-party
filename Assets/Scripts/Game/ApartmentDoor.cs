using UnityEngine;

// State is owned by PropHuntMatch; the existing animator moves the door and its colliders.
public class ApartmentDoor : MonoBehaviour
{
    public Animator animator;
    public Collider interactionCollider;
    public string openingState = "Opening";
    public string closingState = "Closing";
    public bool IsOpen { get; private set; }
    double nextUse;

    void Awake()
    {
        if (animator != null) animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }

    public void Apply(bool open, bool snap = false)
    {
        IsOpen = open;
        if (animator != null) animator.Play(open ? openingState : closingState, 0, snap ? 1 : 0);
    }

    public bool CanUse(PropHuntPlayer player, Vector3 eye, double now)
    {
        if (!isActiveAndEnabled || animator == null || interactionCollider == null || !interactionCollider.enabled ||
            !interactionCollider.gameObject.activeInHierarchy || now < nextUse) return false;
        Vector3 point = interactionCollider.ClosestPoint(eye);
        Vector3 delta = point - eye;
        if (delta.sqrMagnitude > 9) return false;
        var hits = Physics.RaycastAll(eye, delta.normalized, delta.magnitude + 0.1f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (hit.collider.GetComponentInParent<PropHuntPlayer>() == player) continue;
            return hit.collider.GetComponentInParent<ApartmentDoor>() == this;
        }
        return delta.sqrMagnitude < 0.01f;
    }

    public void Used(double now) => nextUse = now + 0.8;
}
