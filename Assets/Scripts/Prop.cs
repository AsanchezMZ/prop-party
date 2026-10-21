using System.Collections.Generic;
using UnityEngine;

public class Prop : MonoBehaviour
{
    enum ColliderMode { Box, ConvexMesh, ExactMesh }

    public int id;
    public int slotId;
    public string propName = "prop";
    public Transform visual;
    [SerializeField, Range(0f, 1f)] float spawnProbability = 1.0f;
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

        bool appears = Rand01(seed, slotId, 0) <= spawnProbability;
        gameObject.SetActive(appears);
        if (!appears || visual == null) return;

        Vector3 offset = new Vector3(
            (Rand01(seed, slotId, 1) * 2f - 1f) * randomRotationThreshold.x,
            (Rand01(seed, slotId, 2) * 2f - 1f) * randomRotationThreshold.y,
            (Rand01(seed, slotId, 3) * 2f - 1f) * randomRotationThreshold.z);
        visual.localRotation = baseLocalRot * Quaternion.Euler(offset);
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
}