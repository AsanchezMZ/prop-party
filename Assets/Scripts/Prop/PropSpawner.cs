using Unity.Netcode;
using UnityEngine;

public class MapPropSpawner : NetworkBehaviour
{
    [SerializeField] private Prop[] props;

    private NetworkVariable<int> seed = new NetworkVariable<int>(
        0,
        NetworkVariableBase.DefaultReadPerm,
        NetworkVariableWritePermission.Server);

    void Awake()
    {
        foreach (var p in props) p.gameObject.SetActive(false);
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
        seed.Value = Random.Range(1, int.MaxValue);
    }

    void ApplyAll(int s)
    {
        if (s == 0) return; // aún sin tirada
        foreach (var p in props) p.ApplySeed(s);
    }

    [ContextMenu("Test local")]
    void TestLocal()
    {
        int s = Random.Range(1, int.MaxValue);
        foreach (var p in props) p.ApplySeed(s);
        Debug.Log($"Semilla {s}");
    }

#if UNITY_EDITOR
    [ContextMenu("Collect and assign props IDs")]
    void CollectProps()
    {
        props = FindObjectsByType<Prop>(FindObjectsInactive.Include);
        System.Array.Sort(props, (a, b) => string.CompareOrdinal(a.name, b.name));
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

