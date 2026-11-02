using UnityEngine;
using UnityEngine.Rendering;

// Short-lived geometric particles: no textures, colliders or external assets.
public class PartyVfx : MonoBehaviour
{
    Transform[] pieces;
    Vector3[] velocities;
    float age;
    bool ring;

    public static void Burst(Vector3 position, Color color, bool ring = false, Material material = null)
    {
        var effect = new GameObject("Party burst").AddComponent<PartyVfx>();
        effect.transform.position = position;
        effect.ring = ring;
        effect.pieces = new Transform[ring ? 16 : 22];
        effect.velocities = new Vector3[effect.pieces.Length];
        var properties = new MaterialPropertyBlock();
        properties.SetColor("_BaseColor", color); properties.SetColor("_Color", color);
        for (int i = 0; i < effect.pieces.Length; i++)
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var collider = piece.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            piece.transform.SetParent(effect.transform, false);
            piece.transform.localScale = ring ? new Vector3(0.06f, 0.06f, 0.18f) : new Vector3(0.06f, 0.12f, 0.03f);
            var renderer = piece.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            if (material != null) renderer.sharedMaterial = material;
            renderer.SetPropertyBlock(properties);
            effect.pieces[i] = piece.transform;
            float angle = i * Mathf.PI * 2 / effect.pieces.Length;
            effect.velocities[i] = ring ? new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 2 : Random.onUnitSphere * 2 + Vector3.up * 2;
        }
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= 0.8f) { Destroy(gameObject); return; }
        for (int i = 0; i < pieces.Length; i++)
        {
            if (!ring) velocities[i] += Vector3.down * 6 * Time.deltaTime;
            pieces[i].localPosition += velocities[i] * Time.deltaTime;
            pieces[i].Rotate(new Vector3(70, 110, 40) * Time.deltaTime);
            pieces[i].localScale *= Mathf.Max(0, 1 - Time.deltaTime * 2);
        }
    }
}
