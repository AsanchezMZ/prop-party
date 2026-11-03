using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Local, bounded bursts. Particle simulation never adds colliders or network objects.
public class PartyVfx : MonoBehaviour
{
    public const int MaxActiveBursts = 18;
    static readonly Queue<PartyVfx> active = new();
    static readonly Material[] materials = new Material[4];
    static Mesh shell;
    static uint nextSeed = 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetResources()
    {
        active.Clear(); nextSeed = 1;
        foreach (var material in materials) if (material != null) Destroy(material);
        for (int i = 0; i < materials.Length; i++) materials[i] = null;
        if (shell != null) Destroy(shell);
        shell = null;
    }

    static PartyVfx Create(Vector3 position, Vector3 normal, string label)
    {
        while (active.Count >= MaxActiveBursts)
        {
            var oldest = active.Dequeue();
            if (oldest != null) { oldest.gameObject.SetActive(false); Destroy(oldest.gameObject); }
        }
        var effect = new GameObject(label).AddComponent<PartyVfx>();
        effect.transform.SetPositionAndRotation(position, Quaternion.LookRotation(normal.sqrMagnitude > 0.001f ? normal : Vector3.up));
        active.Enqueue(effect);
        Destroy(effect.gameObject, 1.6f);
        return effect;
    }

    static Material SharedMaterial(int shape)
    {
        if (materials[shape] != null) return materials[shape];
        var shader = Resources.Load<Shader>("PartyParticles");
        if (shader == null) return Resources.Load<Material>("EggTrail");
        materials[shape] = new Material(shader) { name = "Party particles " + shape, hideFlags = HideFlags.HideAndDontSave };
        materials[shape].SetFloat("_Shape", shape);
        return materials[shape];
    }

    static Mesh ShellMesh()
    {
        if (shell != null) return shell;
        shell = new Mesh { name = "Curved eggshell fragment", hideFlags = HideFlags.HideAndDontSave };
        shell.vertices = new[] { new Vector3(-0.6f, -0.4f, 0), new Vector3(0.45f, -0.45f, 0.03f),
            new Vector3(0.7f, 0.3f, 0), new Vector3(-0.15f, 0.65f, 0.08f), new Vector3(-0.6f, 0.2f, 0), new Vector3(0, 0, 0.22f) };
        shell.triangles = new[] { 5, 0, 1, 5, 1, 2, 5, 2, 3, 5, 3, 4, 5, 4, 0 };
        shell.RecalculateNormals(); shell.RecalculateBounds();
        return shell;
    }

    ParticleSystem Layer(string label, int count, int shape, Color first, Color last, float size,
        float speed, float lifetime, float gravity = 0, Material material = null)
    {
        var go = new GameObject(label); go.transform.SetParent(transform, false);
        var particles = go.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particles.useAutoRandomSeed = false; particles.randomSeed = nextSeed++;
        var main = particles.main;
        main.playOnAwake = false; main.loop = false; main.duration = 0.1f;
        main.maxParticles = count; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.65f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.45f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.55f, size);
        main.startColor = new ParticleSystem.MinMaxGradient(first, last);
        main.gravityModifier = gravity;
        main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
        var emission = particles.emission; emission.enabled = false;
        var cone = particles.shape; cone.enabled = true; cone.shapeType = ParticleSystemShapeType.Cone;
        cone.angle = 68; cone.radius = 0.035f;
        var fade = particles.colorOverLifetime; fade.enabled = true;
        fade.color = new Gradient
        {
            colorKeys = new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            alphaKeys = new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0.85f, 0.55f), new GradientAlphaKey(0, 1) }
        };
        var scale = particles.sizeOverLifetime; scale.enabled = true;
        scale.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 0.65f), new Keyframe(0.12f, 1), new Keyframe(1, 0.4f)));
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material != null ? material : SharedMaterial(shape);
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        renderer.sortingFudge = 0.1f;
        if (shape == 3)
        {
            renderer.renderMode = ParticleSystemRenderMode.Mesh; renderer.mesh = ShellMesh();
            main.startRotation3D = true;
            main.startRotationX = main.startRotationY = main.startRotationZ = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            var spin = particles.rotationOverLifetime; spin.enabled = true; spin.separateAxes = true;
            spin.x = new ParticleSystem.MinMaxCurve(-6, 6); spin.y = new ParticleSystem.MinMaxCurve(-5, 5); spin.z = new ParticleSystem.MinMaxCurve(-8, 8);
        }
        particles.Play(); particles.Emit(count);
        return particles;
    }

    void Ring(Color color, float size)
    {
        var particles = Layer("Expanding impact ring", 1, 1, color, color, size, 0, 0.4f);
        var main = particles.main; main.startSize = size;
        var scale = particles.sizeOverLifetime;
        scale.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 0.25f, 1, 2.6f));
        particles.GetComponent<ParticleSystemRenderer>().alignment = ParticleSystemRenderSpace.Local;
    }

    public static void EggImpact(Vector3 position, Vector3 normal, bool hitPlayer)
    {
        var effect = Create(position + normal * 0.02f, normal, "Egg / shell, yolk and splash");
        effect.Layer("Flying shell fragments", 18, 3, new Color(1, 0.97f, 0.83f), new Color(0.82f, 0.73f, 0.55f), 0.19f, 3.5f, 1.15f, 0.9f);
        effect.Layer("Heavy yolk droplets", 24, 2, new Color(1, 0.64f, 0.06f), new Color(1, 0.9f, 0.32f), 0.13f, 3, 0.8f, 0.65f);
        effect.Layer("Egg white mist", 8, 0, new Color(1, 0.97f, 0.78f, 0.6f), new Color(1, 0.86f, 0.5f, 0.25f), 0.65f, 0.8f, 0.55f);
        var splash = effect.Layer("Surface yolk rosette", 5, 2, new Color(1, 0.67f, 0.08f, 0.9f), new Color(1, 0.85f, 0.28f, 0.7f), 0.45f, 0, hitPlayer ? 0.35f : 1.35f);
        var shape = splash.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 0.19f;
        splash.GetComponent<ParticleSystemRenderer>().alignment = ParticleSystemRenderSpace.Local;
        // Re-emit after configuring the surface spread, not the default ejection cone.
        splash.Clear(); splash.Emit(5);
        effect.Ring(hitPlayer ? new Color(0.34f, 1, 0.76f) : new Color(1, 0.83f, 0.32f), 0.65f);
    }

    public static void Burst(Vector3 position, Color color, bool ring = false, Material material = null)
    {
        var effect = Create(position, Vector3.up, "Disguise / ink bloom");
        effect.Ring(color, ring ? 1.1f : 0.8f);
        effect.Layer("Spinning paper slivers", 24, 3, color, Color.Lerp(color, Color.white, 0.65f), 0.13f, 2.7f, 1.1f, 0.3f, material);
        effect.Layer("Soft color bloom", 7, 0, new Color(color.r, color.g, color.b, 0.4f), new Color(color.r, color.g, color.b, 0.1f), 0.85f, 0.8f, 0.65f);
    }

    public static void Capture(Vector3 position, Material material = null)
    {
        var effect = Create(position, Vector3.up, "Capture / confetti finale");
        effect.Ring(new Color(1, 0.8f, 0.2f), 1.3f);
        effect.Layer("Coral confetti", 28, 3, new Color(1, 0.32f, 0.23f), new Color(1, 0.76f, 0.23f), 0.17f, 4.8f, 1.4f, 0.65f, material);
        effect.Layer("Mint confetti", 22, 3, new Color(0.2f, 0.95f, 0.7f), new Color(1, 0.97f, 0.8f), 0.12f, 3.5f, 1.3f, 0.45f, material);
        effect.Layer("Celebration glow", 6, 0, new Color(1, 0.72f, 0.3f, 0.45f), new Color(1, 0.9f, 0.5f, 0.1f), 1, 1.6f, 0.55f);
    }

    public static void Hurt(Vector3 position, Material material = null)
    {
        var effect = Create(position, Vector3.up, "Hit / coral fragments");
        effect.Layer("Impact flecks", 10, 3, new Color(1, 0.3f, 0.22f), new Color(1, 0.83f, 0.3f), 0.09f, 2, 0.5f, 0.25f, material);
        effect.Ring(new Color(1, 0.52f, 0.28f), 0.5f);
    }
}
