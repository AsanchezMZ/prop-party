// LobbyManager.cs
using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyManager : NetworkBehaviour
{

    [SerializeField] string[] maps = { "Map_HauntedHouse", "Map_Town" };
    static string lastMap;

    public NetworkList<LobbyPlayer> Players;
    public event Action Changed;

    void Awake() => Players = new NetworkList<LobbyPlayer>();

    public override void OnNetworkSpawn()
    {
        Players.OnListChanged += _ => Changed?.Invoke();

        if (IsServer)
            NetworkManager.OnClientDisconnectCallback += RemovePlayer;

        // Cada cliente (host incluido) dice su nombre al servidor
        RegisterPlayerServerRpc(new FixedString32Bytes(LocalProfile.PlayerName));
        Changed?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager != null)
            NetworkManager.OnClientDisconnectCallback -= RemovePlayer;
    }

    [ServerRpc(RequireOwnership = false)]
    void RegisterPlayerServerRpc(FixedString32Bytes name, ServerRpcParams p = default)
    {
        ulong id = p.Receive.SenderClientId;
        foreach (var pl in Players) if (pl.ClientId == id) return;
        Players.Add(new LobbyPlayer { ClientId = id, Name = name });
    }

    void RemovePlayer(ulong id)
    {
        for (int i = 0; i < Players.Count; i++)
            if (Players[i].ClientId == id) { Players.RemoveAt(i); break; }
    }

    public void StartGame()
    {
        if (!IsServer || maps.Length == 0) return;

        string chosen;
        int tries = 0;
        do { chosen = maps[UnityEngine.Random.Range(0, maps.Length)]; }
        while (maps.Length > 1 && chosen == lastMap && ++tries < 10);

        lastMap = chosen;
        NetworkManager.SceneManager.LoadScene(chosen, LoadSceneMode.Single);
    }
}