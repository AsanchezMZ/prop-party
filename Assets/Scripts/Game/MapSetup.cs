using UnityEngine;

public class MapSetup : MonoBehaviour
{
    public MapDefinition definition;
    public SpawnPoint[] spawns;
    public ApartmentDoor[] doors = System.Array.Empty<ApartmentDoor>();

    public bool TrySpawn(PlayerRole role, int index, out Transform point)
    {
        point = null;
        int count = 0;
        foreach (var spawn in spawns)
            if (spawn != null && spawn.Supports(role)) count++;
        if (count == 0) return false;
        int selected = index % count;
        foreach (var spawn in spawns)
            if (spawn != null && spawn.Supports(role) && selected-- == 0)
            { point = spawn.transform; return true; }
        return false;
    }

    [ContextMenu("Collect scene spawn points")]
    public void Collect()
    {
        var found = new System.Collections.Generic.List<SpawnPoint>();
        foreach (var root in gameObject.scene.GetRootGameObjects())
            found.AddRange(root.GetComponentsInChildren<SpawnPoint>(true));
        spawns = found.ToArray();
    }
}
