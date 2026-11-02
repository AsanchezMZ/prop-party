using System;
using System.Collections.Generic;
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
    readonly Dictionary<ulong, byte[]> portraitBytes = new();
    readonly Dictionary<ulong, Texture2D> portraits = new();
    readonly Dictionary<ulong, double> nextPortrait = new();
    public Texture2D Portrait(ulong id) => portraits.TryGetValue(id, out var image) ? image : null;

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
        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback += OtherConnected;
            NetworkManager.OnClientDisconnectCallback += RemovePlayer;
        }
        if (IsClient)
        {
            RegisterPlayerServerRpc(new FixedString32Bytes(LocalProfile.PlayerName));
            SendLocalPortrait();
        }
        Changed?.Invoke();
    }

    void ListChanged(NetworkListEvent<LobbyPlayer> change) => Changed?.Invoke();
    void MapChanged(int previous, int current)
    {
        if (IsClient) PartyAudio.Instance?.Play(PartyAudio.Settings?.disguise);
        Changed?.Invoke();
    }
    void SettingsChanged(MatchSettings previous, MatchSettings current) => Changed?.Invoke();

    public override void OnNetworkDespawn()
    {
        Players.OnListChanged -= ListChanged;
        SelectedMap.OnValueChanged -= MapChanged;
        Settings.OnValueChanged -= SettingsChanged;
        if (NetworkManager != null)
        {
            NetworkManager.OnClientConnectedCallback -= OtherConnected;
            NetworkManager.OnClientDisconnectCallback -= RemovePlayer;
        }
        foreach (var texture in portraits.Values) if (texture != null) Destroy(texture);
        portraits.Clear(); portraitBytes.Clear(); nextPortrait.Clear();
    }

    [ServerRpc(RequireOwnership = false)]
    void RegisterPlayerServerRpc(FixedString32Bytes name, ServerRpcParams rpc = default)
    {
        ulong id = rpc.Receive.SenderClientId;
        foreach (var player in Players) if (player.ClientId == id) return;
        Players.Add(new LobbyPlayer { ClientId = id, Name = name });
        var target = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { id } } };
        foreach (var photo in portraitBytes) PortraitClientRpc(photo.Key, photo.Value, target);
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
                portraitBytes.Remove(id); nextPortrait.Remove(id);
                if (IsSpawned && !NetworkManager.ShutdownInProgress) PresenceClientRpc(id, false);
                break;
            }
    }

    void OtherConnected(ulong id)
    {
        var listeners = new List<ulong>();
        foreach (var player in Players) if (player.ClientId != id) listeners.Add(player.ClientId);
        if (listeners.Count == 0) return;
        PresenceClientRpc(id, true, new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = listeners } });
    }

    [ClientRpc]
    void PresenceClientRpc(ulong id, bool joined, ClientRpcParams rpc = default)
    {
        if (!joined && portraits.Remove(id, out var texture)) Destroy(texture);
        if (id != NetworkManager.LocalClientId)
            PartyAudio.Instance?.Play(joined ? PartyAudio.Settings?.connected : PartyAudio.Settings?.disconnected);
        Changed?.Invoke();
    }

    // Only a saved still photo is shared, never the live webcam feed.
    public void SendLocalPortrait()
    {
        if (!IsSpawned || !IsClient) return;
        LocalProfile.LoadSavedFace();
        var jpg = LocalProfile.FaceJpg;
        if (jpg == null || jpg.Length == 0 || jpg.Length > PlayerFace.MaxPhotoBytes) return;
        var photo = new Texture2D(2, 2, TextureFormat.RGB24, false);
        var thumbnail = new Texture2D(64, 64, TextureFormat.RGB24, false);
        var target = RenderTexture.GetTemporary(64, 64, 0);
        var previous = RenderTexture.active;
        try
        {
            if (!photo.LoadImage(jpg)) return;
            Graphics.Blit(photo, target); RenderTexture.active = target;
            thumbnail.ReadPixels(new Rect(0, 0, 64, 64), 0, 0); thumbnail.Apply();
            SubmitPortraitServerRpc(thumbnail.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target);
            Destroy(photo); Destroy(thumbnail);
        }
    }

    // Check PNG dimensions before decoding untrusted bytes, not after allocating the image.
    public static bool ValidPortrait(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 33 || bytes.Length > 20_000) return false;
        byte[] header = { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82,
            0, 0, 0, 64, 0, 0, 0, 64 };
        for (int i = 0; i < header.Length; i++) if (bytes[i] != header[i]) return false;
        return true;
    }

    [ServerRpc(RequireOwnership = false)]
    void SubmitPortraitServerRpc(byte[] bytes, ServerRpcParams rpc = default)
    {
        ulong id = rpc.Receive.SenderClientId;
        if (loading || !ValidPortrait(bytes)) return;
        bool registered = false;
        foreach (var player in Players) if (player.ClientId == id) { registered = true; break; }
        double now = NetworkManager.ServerTime.Time;
        if (!registered || (nextPortrait.TryGetValue(id, out double next) && now < next)) return;
        nextPortrait[id] = now + 1;
        portraitBytes[id] = bytes;
        PortraitClientRpc(id, bytes);
    }

    [ClientRpc]
    void PortraitClientRpc(ulong id, byte[] bytes, ClientRpcParams rpc = default)
    {
        if (!ValidPortrait(bytes)) return;
        var image = new Texture2D(2, 2, TextureFormat.RGB24, false);
        if (!image.LoadImage(bytes) || image.width != 64 || image.height != 64) { Destroy(image); return; }
        if (portraits.Remove(id, out var old)) Destroy(old);
        portraits[id] = image;
        Changed?.Invoke();
    }


    public void StartGame()
    {
        if (!CanStart) return;
        var bootstrap = NetworkManager.GetComponent<GameBootstrap>();
        if (bootstrap != null) bootstrap.SessionSettings = Settings.Value;
        PartyLoadingScreen.Show();
        loading = NetworkManager.SceneManager.LoadScene(Maps[SelectedMap.Value].sceneName, LoadSceneMode.Single) == SceneEventProgressStatus.Started;
        if (!loading) PartyLoadingScreen.Cancel();
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
