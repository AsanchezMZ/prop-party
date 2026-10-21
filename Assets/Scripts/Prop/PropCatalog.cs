using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "PropHunt/Prop Catalog")]
public class PropCatalog : ScriptableObject
{
    [SerializeField] private Prop[] props;
    private Dictionary<int, Prop> lookup;

    public bool TryGet(int id, out Prop prop)
    {
        if (lookup == null) Build();
        return lookup.TryGetValue(id, out prop);
    }

    void Build()
    {
        lookup = new Dictionary<int, Prop>();
        foreach (var p in props)
            if (p != null) lookup[p.id] = p;
    }

    void OnValidate()
    {
        lookup = null;
        var seen = new HashSet<int>();
        foreach (var p in props)
        {
            if (p == null) continue;
            if (p.id == 0) Debug.LogError($"[{name}] {p.name}: el ID 0 está reservado", this);
            if (!seen.Add(p.id)) Debug.LogError($"[{name}] ID duplicado: {p.id} ({p.name})", this);
        }
    }
}