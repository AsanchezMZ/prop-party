using System.Xml.Serialization;
using NUnit.Framework;
using UnityEngine;

public class Prop : MonoBehaviour
{

    enum ColliderMode
    {
        Box,            
        ConvexMesh,     
        ExactMesh 
    }

    [SerializeField] string propName = "prop";
    [SerializeField] bool canBeDisguise = true;
    
    [SerializeField] ColliderMode colliderMode = ColliderMode.ConvexMesh;

    [SerializeField, UnityEngine.Range(0f, 1f)] float spawnProbability = 1.0f;

    [SerializeField] Vector3 randomRotationThreshold;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if(Random.value > spawnProbability)
        {
            Destroy(gameObject);
        }
        float m = Random.value < 0.5f ? -1.0f : 1.0f;
        float x = m * (Random.value * randomRotationThreshold.x);
        m = Random.value < 0.5f ? -1.0f : 1.0f;
        float y = m * (Random.value * randomRotationThreshold.y);
        m = Random.value < 0.5f ? -1.0f : 1.0f;
        float z = m * (Random.value * randomRotationThreshold.z);
        transform.Rotate(new Vector3(x, y, z), Space.Self);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    [ContextMenu("Generate collider")]
    void GenerateCollider()
    {
        var filters = GetComponentsInChildren<MeshFilter>();
        if (filters.Length == 0)
        {
            Debug.LogWarning("No hay ningún mesh dentro de Visual.", this);
            return;
        }

        ClearColliders();

        switch (colliderMode)
        {
            case ColliderMode.Box:
                FitBox();
                break;

            case ColliderMode.ConvexMesh:
            case ColliderMode.ExactMesh:
                foreach (var f in filters)
                {
                    var mc = f.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = f.sharedMesh;
                    mc.convex = colliderMode == ColliderMode.ConvexMesh;
                }
                break;
        }
    }

    void ClearColliders()
    {
        foreach (var c in GetComponentsInChildren<Collider>())
            DestroyImmediate(c);
    }

    void FitBox()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);

        var box = gameObject.AddComponent<BoxCollider>();
        box.center = transform.InverseTransformPoint(b.center);
        box.size = new Vector3(
            b.size.x / transform.lossyScale.x,
            b.size.y / transform.lossyScale.y,
            b.size.z / transform.lossyScale.z);
    }

    
}
