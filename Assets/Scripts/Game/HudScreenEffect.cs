using UnityEngine;
using UnityEngine.UIElements;

// A feathered peripheral wash and ink cuts; the aiming area stays transparent.
public sealed class HudScreenEffect : VisualElement
{
    readonly bool damage;
    float strength, clock;
    Color tint;

    public HudScreenEffect(bool damage)
    {
        this.damage = damage;
        pickingMode = PickingMode.Ignore;
        style.position = Position.Absolute;
        style.left = style.right = style.top = style.bottom = 0;
        generateVisualContent += Draw;
    }

    public void SetState(float value, Color color, float time)
    {
        value = Mathf.Clamp01(value);
        if (value == 0 && strength == 0) return;
        strength = value; tint = color; clock = time;
        MarkDirtyRepaint();
    }

    void Draw(MeshGenerationContext context)
    {
        if (strength <= 0 || contentRect.width < 1 || contentRect.height < 1) return;
        const int segments = 64, bands = 4;
        var mesh = context.Allocate(segments * bands * 4, segments * bands * 6);
        Vector2 center = contentRect.center;
        float breath = damage ? 0 : Mathf.Sin(clock * 3.5f) * 0.015f;
        for (int band = 0; band < bands; band++)
        {
            float inner = Mathf.Lerp(0.64f + breath, 1.52f, band / (float)bands);
            float outer = Mathf.Lerp(0.64f + breath, 1.52f, (band + 1f) / bands);
            Color a = tint, b = tint;
            a.a = strength * Mathf.Pow(band / (float)bands, 1.7f) * (damage ? 0.72f : 0.45f);
            b.a = strength * Mathf.Pow((band + 1f) / bands, 1.7f) * (damage ? 0.72f : 0.45f);
            for (int i = 0; i < segments; i++)
            {
                float start = i * Mathf.PI * 2 / segments, end = (i + 1) * Mathf.PI * 2 / segments;
                Vector2 first = new Vector2(Mathf.Cos(start) * center.x, Mathf.Sin(start) * center.y);
                Vector2 last = new Vector2(Mathf.Cos(end) * center.x, Mathf.Sin(end) * center.y);
                mesh.SetNextVertex(VertexAt(center + first * inner, a));
                mesh.SetNextVertex(VertexAt(center + first * outer, b));
                mesh.SetNextVertex(VertexAt(center + last * outer, b));
                mesh.SetNextVertex(VertexAt(center + last * inner, a));
                ushort vertex = (ushort)((band * segments + i) * 4);
                mesh.SetNextIndex(vertex); mesh.SetNextIndex((ushort)(vertex + 1)); mesh.SetNextIndex((ushort)(vertex + 2));
                mesh.SetNextIndex((ushort)(vertex + 2)); mesh.SetNextIndex((ushort)(vertex + 3)); mesh.SetNextIndex(vertex);
            }
        }
        var painter = context.painter2D;
        Color ink = tint; ink.a = strength * (damage ? 0.85f : 0.5f);
        painter.strokeColor = ink;
        painter.lineWidth = damage ? 2.5f : 1.5f;
        for (int corner = 0; corner < 4; corner++)
        {
            Vector2 origin = new Vector2(corner % 2 == 0 ? 0 : contentRect.width, corner < 2 ? 0 : contentRect.height);
            Vector2 sign = new Vector2(corner % 2 == 0 ? 1 : -1, corner < 2 ? 1 : -1);
            for (int cut = 0; cut < (damage ? 5 : 3); cut++)
            {
                float inset = 12 + cut * 10;
                float reach = (damage ? 82 : 48) + Mathf.Sin(clock * 2 + cut) * 6;
                painter.BeginPath();
                painter.MoveTo(origin + Vector2.Scale(sign, new Vector2(inset, reach)));
                painter.LineTo(origin + Vector2.Scale(sign, new Vector2(inset + 8, inset + 14)));
                painter.LineTo(origin + Vector2.Scale(sign, new Vector2(reach + 14, inset)));
                painter.Stroke();
            }
        }
    }

    static Vertex VertexAt(Vector2 position, Color color) => new Vertex
    {
        position = new Vector3(position.x, position.y, Vertex.nearZ), tint = color, uv = Vector2.zero
    };
}
