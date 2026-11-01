using Unity.Netcode;
using UnityEngine;

public class MapPropSpawner : NetworkBehaviour
{
    [SerializeField] private Prop[] props = System.Array.Empty<Prop>();

    private NetworkVariable<int> seed = new NetworkVariable<int>(
        0,
        NetworkVariableBase.DefaultReadPerm,
        NetworkVariableWritePermission.Server);

    void Awake()
    {
        foreach (var p in props) if (p != null) p.gameObject.SetActive(false);
    }

    public override void OnNetworkSpawn()
    {
        seed.OnValueChanged += OnSeedChanged;
        if (IsServer) Roll();
        ApplyAll(seed.Value);
    }

    public override void OnNetworkDespawn()
    {
        seed.OnValueChanged -= OnSeedChanged;
    }

    void OnSeedChanged(int oldSeed, int newSeed) => ApplyAll(newSeed);

    public void Roll()
    {
        if (!IsServer) return;
        int next;
        do { next = Random.Range(1, int.MaxValue); } while (next == seed.Value);
        seed.Value = next;
    }

    void ApplyAll(int s)
    {
        if (s == 0) return; // aún sin tirada
        foreach (var p in props) if (p != null) p.ApplySeed(s);
    }

    [ContextMenu("Test local")]
    void TestLocal()
    {
        int s = Random.Range(1, int.MaxValue);
        foreach (var p in props) if (p != null) p.ApplySeed(s);
        Debug.Log($"Semilla {s}");
    }

#if UNITY_EDITOR
    [ContextMenu("Collect and assign props IDs")]
    public void CollectProps()
    {
        var found = new System.Collections.Generic.List<Prop>();
        foreach (var root in gameObject.scene.GetRootGameObjects())
            found.AddRange(root.GetComponentsInChildren<Prop>(true));
        props = found.ToArray();
        for (int i = 0; i < props.Length; i++)
        {
            props[i].slotId = i + 1;
            UnityEditor.EditorUtility.SetDirty(props[i]);
        }
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"{props.Length} props registrados");
    }
#endif
}
