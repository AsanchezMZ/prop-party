using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyManager : NetworkBehaviour
{
    [SerializeField] MapDefinition[] mapDefinitions = Array.Empty<MapDefinition>();
    [SerializeField] MatchRules rules;
    public MapDefinition[] Maps => mapDefinitions;
    public MatchRules Rules => rules;
    public readonly NetworkVariable<int> SelectedMap = new();
    public readonly NetworkVariable<MatchSettings> Settings = new();
    public NetworkList<LobbyPlayer> Players;
    public event Action Changed;
    bool loading;

    public bool CanStart
    {
        get
        {
            if (!IsServer || loading || rules == null || Players.Count < Mathf.Max(2, rules.minimumPlayers) ||
                SelectedMap.Value < 0 || SelectedMap.Value >= Maps.Length || Maps[SelectedMap.Value] == null ||
                !Application.CanStreamedLevelBeLoaded(Maps[SelectedMap.Value].sceneName)) return false;
            foreach (var player in Players) if (!player.Ready) return false;
            return true;
        }
    }

    void Awake() => Players = new NetworkList<LobbyPlayer>();

    public override void OnNetworkSpawn()
    {
        loading = false;
        if (IsServer && rules != null)
        {
            var bootstrap = NetworkManager.GetComponent<GameBootstrap>();
            Settings.Value = bootstrap != null && bootstrap.SessionSettings.HasValue ? bootstrap.SessionSettings.Value : MatchSettings.From(rules);
        }
        Players.OnListChanged += ListChanged;
        SelectedMap.OnValueChanged += MapChanged;
        Settings.OnValueChanged += SettingsChanged;
        if (IsServer) NetworkManager.OnClientDisconnectCallback += RemovePlayer;
        if (IsClient) RegisterPlayerServerRpc(new FixedString32Bytes(LocalProfile.PlayerName));
        Changed?.Invoke();
    }

    void ListChanged(NetworkListEvent<LobbyPlayer> change) => Changed?.Invoke();
    void MapChanged(int previous, int current) => Changed?.Invoke();
    void SettingsChanged(MatchSettings previous, MatchSettings current) => Changed?.Invoke();

    public override void OnNetworkDespawn()
    {
        Players.OnListChanged -= ListChanged;
        SelectedMap.OnValueChanged -= MapChanged;
        Settings.OnValueChanged -= SettingsChanged;
        if (NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= RemovePlayer;
    }

    [ServerRpc(RequireOwnership = false)]
    void RegisterPlayerServerRpc(FixedString32Bytes name, ServerRpcParams rpc = default)
    {
        ulong id = rpc.Receive.SenderClientId;
        foreach (var player in Players) if (player.ClientId == id) return;
        Players.Add(new LobbyPlayer { ClientId = id, Name = name });
    }

    [ServerRpc(RequireOwnership = false)]
    public void ToggleReadyServerRpc(ServerRpcParams rpc = default)
    {
        if (loading) return;
        for (int i = 0; i < Players.Count; i++)
            if (Players[i].ClientId == rpc.Receive.SenderClientId)
            {
                var player = Players[i]; player.Ready = !player.Ready; Players[i] = player; break;
            }
    }

    public void SelectMap(int index)
    {
        if (!IsServer || loading || index < 0 || index >= Maps.Length || index == SelectedMap.Value) return;
        SelectedMap.Value = index;
        for (int i = 0; i < Players.Count; i++)
        { var player = Players[i]; player.Ready = false; Players[i] = player; }
    }

    void RemovePlayer(ulong id)
    {
        for (int i = 0; i < Players.Count; i++)
            if (Players[i].ClientId == id)
            {
                Players.RemoveAt(i);
                break;
            }
    }


    public void StartGame()
    {
        if (!CanStart) return;
        var bootstrap = NetworkManager.GetComponent<GameBootstrap>();
        if (bootstrap != null) bootstrap.SessionSettings = Settings.Value;
        loading = NetworkManager.SceneManager.LoadScene(Maps[SelectedMap.Value].sceneName, LoadSceneMode.Single) == SceneEventProgressStatus.Started;
        Changed?.Invoke();
    }

    public bool ApplySettings(MatchSettings settings)
    {
        if (!IsServer || !IsSpawned || loading) return false;
        settings = settings.Validated();
        if (settings.Equals(Settings.Value)) return true;
        Settings.Value = settings;
        for (int i = 0; i < Players.Count; i++)
        { var player = Players[i]; player.Ready = false; Players[i] = player; }
        return true;
    }
}
