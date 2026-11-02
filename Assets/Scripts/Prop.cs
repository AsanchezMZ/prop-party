using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class Prop : MonoBehaviour
{
    public int id;
    public int slotId;
    public string propName = "prop";
    public Transform visual;
    public enum CollisionShape { Capsule, Box, Model }
    public CollisionShape collisionShape = CollisionShape.Capsule;
    public Vector3 disguiseBoxSize = new Vector3(0.8f, 1, 0.8f);
    [Min(0.1f)] public float disguiseRadius = 0.4f;
    [Min(0.2f)] public float disguiseHeight = 1;
    [SerializeField, Range(0f, 1f)] float spawnProbability = 1.0f;
    [Tooltip("Disable to keep this prop present in every round.")]
    [SerializeField] bool canDisappear = true;
    [SerializeField] Vector3 randomRotationThreshold; // grados máx. por eje (+/-)

    Quaternion baseLocalRot;
    bool cached;

    static readonly List<Prop> active = new List<Prop>();
    void OnEnable()  => active.Add(this);
    void OnDisable() => active.Remove(this);

    void OnValidate()
{
    if (visual == null)
    {
        var t = transform.Find("Visual");
        if (t != null) visual = t;
    }
}

    public static bool ExistsNear(int id, Vector3 pos, float maxDist)
    {
        foreach (Prop p in active)
            if (p.id == id && (p.transform.position - pos).sqrMagnitude <= maxDist * maxDist)
                return true;
        return false;
    }

    public void ApplySeed(int seed)
    {
        if (!cached && visual != null) { baseLocalRot = visual.localRotation; cached = true; }

        bool appears = !canDisappear || Rand01(seed, slotId, 0) < spawnProbability;
        gameObject.SetActive(appears);
        if (!appears || visual == null) return;

        Vector3 offset = new Vector3(
            (Rand01(seed, slotId, 1) * 2f - 1f) * randomRotationThreshold.x,
            (Rand01(seed, slotId, 2) * 2f - 1f) * randomRotationThreshold.y,
            (Rand01(seed, slotId, 3) * 2f - 1f) * randomRotationThreshold.z);
        visual.localRotation = baseLocalRot * Quaternion.Euler(offset);
    }

    public Vector3 BoxSize => new Vector3(Mathf.Max(0.1f, disguiseBoxSize.x),
        Mathf.Max(0.1f, disguiseBoxSize.y), Mathf.Max(0.1f, disguiseBoxSize.z));

    public bool ConfigureDisguiseColliders(GameObject model)
    {
        foreach (var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        if (collisionShape == CollisionShape.Capsule) return false;
        if (collisionShape == CollisionShape.Box)
        {
            var box = model.AddComponent<BoxCollider>();
            box.size = BoxSize;
            box.center = Vector3.up * box.size.y * 0.5f;
            return true;
        }
        bool hasMesh = false;
        foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || !filter.gameObject.activeInHierarchy) continue;
            var collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            hasMesh = true;
        }
        if (!hasMesh) Debug.LogWarning($"{name}: Model collision requires an active MeshFilter; using the capsule instead.", this);
        return hasMesh;
    }

    static float Rand01(int seed, int slot, int channel)
    {
        uint h = Hash((uint)seed ^ Hash((uint)slot * 31u + (uint)channel * 7919u + 17u));
        return (h & 0xFFFFFFu) / (float)0x1000000;
    }

    static uint Hash(uint x)
    {
        x ^= x >> 16; x *= 0x7feb352du;
        x ^= x >> 15; x *= 0x846ca68bu;
        x ^= x >> 16;
        return x;
    }
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (collisionShape == CollisionShape.Box)
        {
            using (new Handles.DrawingScope(Color.cyan, transform.localToWorldMatrix *
                Matrix4x4.TRS(Vector3.zero, visual != null ? visual.localRotation : Quaternion.identity,
                    visual != null ? visual.localScale : Vector3.one)))
                Handles.DrawWireCube(Vector3.up * BoxSize.y * 0.5f, BoxSize);
            return;
        }
        if (collisionShape == CollisionShape.Model && visual != null)
        {
            bool hasMesh = false;
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            foreach (var filter in visual.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                Gizmos.color = Color.cyan;
                Gizmos.matrix = filter.transform.localToWorldMatrix;
                Gizmos.DrawWireMesh(filter.sharedMesh);
                hasMesh = true;
            }
            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
            if (hasMesh) return;
        }
        float radius = Mathf.Max(0.1f, disguiseRadius);
        float height = Mathf.Max(radius * 2, disguiseHeight);
        Vector3 bottom = transform.position + Vector3.up * radius;
        Vector3 top = transform.position + Vector3.up * (height - radius);
        Vector3 right = Vector3.right * radius;
        Vector3 forward = Vector3.forward * radius;

        using (new Handles.DrawingScope(Color.cyan))
        {
            Handles.DrawWireDisc(bottom, Vector3.up, radius);
            Handles.DrawWireDisc(top, Vector3.up, radius);
            Handles.DrawWireArc(top, Vector3.forward, Vector3.right, 180, radius);
            Handles.DrawWireArc(top, Vector3.right, Vector3.back, 180, radius);
            Handles.DrawWireArc(bottom, Vector3.forward, Vector3.left, 180, radius);
            Handles.DrawWireArc(bottom, Vector3.right, Vector3.forward, 180, radius);
            Handles.DrawLine(bottom + right, top + right);
            Handles.DrawLine(bottom - right, top - right);
            Handles.DrawLine(bottom + forward, top + forward);
            Handles.DrawLine(bottom - forward, top - forward);
        }
    }
#endif
}
