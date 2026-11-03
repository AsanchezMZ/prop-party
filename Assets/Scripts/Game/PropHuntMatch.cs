using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum PlayerRole : byte { Spectator, Prop, Hunter }
public enum MatchPhase : byte { Waiting, Hiding, Hunting, Finished }

public class PropHuntMatch : NetworkBehaviour
{
    [SerializeField] MatchRules rules;
    [SerializeField] MapSetup map;
    [SerializeField] string lobbyScene;
    public MatchRules Rules => rules;
    public MapSetup Map => map;
    public readonly NetworkVariable<MatchPhase> Phase = new(MatchPhase.Waiting);
    public readonly NetworkVariable<double> Deadline = new();
    public readonly NetworkVariable<PlayerRole> Winner = new(PlayerRole.Spectator);
    public readonly NetworkVariable<MatchSettings> Settings = new();
    public readonly NetworkVariable<bool> LastHiderRemaining = new();
    NetworkList<bool> doorStates;
    double nextAutomaticWhistle;
    int initialHiders;

    void Awake() => doorStates = new NetworkList<bool>();
    public override void OnNetworkSpawn()
    {
        doorStates.OnListChanged += DoorChanged;
        if (IsServer && map != null)
        {
            doorStates.Clear();
            foreach (var door in map.doors) doorStates.Add(false);
        }
        ApplyDoors();
        if (!IsServer || rules == null) return;
        var bootstrap = NetworkManager.GetComponent<GameBootstrap>();
        Settings.Value = (bootstrap != null && bootstrap.SessionSettings.HasValue ? bootstrap.SessionSettings.Value : MatchSettings.From(rules)).Validated();
    }
    readonly List<PropHuntPlayer> players = new();
    public IReadOnlyList<PropHuntPlayer> Players => players;
    public double SecondsLeft => System.Math.Max(0, Deadline.Value - NetworkManager.ServerTime.Time);

    public void Register(PropHuntPlayer player) { if (!players.Contains(player)) players.Add(player); }
    public void Unregister(PropHuntPlayer player) { players.Remove(player); }

    public bool BeginRound()
    {
        if (!IsServer || rules == null || map == null || players.Count < Mathf.Max(2, rules.minimumPlayers)) return false;
        if (Phase.Value != MatchPhase.Waiting && Phase.Value != MatchPhase.Finished) return false;
        foreach (var player in players) if (!player.AssetsReady.Value) return false;
        if (!map.TrySpawn(PlayerRole.Hunter, 0, out _) || !map.TrySpawn(PlayerRole.Prop, 0, out _)) return false;
        for (int i = players.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (players[i], players[j]) = (players[j], players[i]);
        }
        foreach (var spawner in FindObjectsByType<MapPropSpawner>(FindObjectsSortMode.None))
            if (spawner.gameObject.scene == gameObject.scene) spawner.Roll();
        int hunters = Mathf.Clamp(Settings.Value.Hunters, 1, players.Count - 1);
        initialHiders = players.Count - hunters;
        LastHiderRemaining.Value = false;
        for (int i = 0; i < doorStates.Count; i++) doorStates[i] = false;
        ApplyDoors();
        var audio = PartyAudio.Settings;
        float intro = audio != null && audio.roundStart != null ? audio.roundStart.length : 0;
        float huntCue = audio != null && audio.huntStart != null ? audio.huntStart.length : 0;
        nextAutomaticWhistle = NetworkManager.ServerTime.Time + Mathf.Max(Settings.Value.HidingSeconds, intro + 2.5f) + Mathf.Max(40, huntCue + 7);
        for (int i = 0; i < players.Count; i++)
        {
            var role = i < hunters ? PlayerRole.Hunter : PlayerRole.Prop;
            map.TrySpawn(role, i, out var spawn);
            players[i].ResetForRound(role, spawn);
        }
        Winner.Value = PlayerRole.Spectator;
        Phase.Value = MatchPhase.Hiding;
        Deadline.Value = NetworkManager.ServerTime.Time + Settings.Value.HidingSeconds;
        return true;
    }

    void Update()
    {
        if (!IsServer || Phase.Value == MatchPhase.Waiting || Phase.Value == MatchPhase.Finished) return;
        int props = 0, hunters = 0;
        foreach (var player in players)
        {
            if (player.Role.Value == PlayerRole.Prop) props++;
            if (player.Role.Value == PlayerRole.Hunter) hunters++;
        }
        if (props == 0 || hunters == 0) { Finish(props == 0 ? PlayerRole.Hunter : PlayerRole.Prop); return; }
        LastHiderRemaining.Value = initialHiders > 1 && props == 1;
        if (SecondsLeft > 0) return;
        if (Phase.Value == MatchPhase.Hiding)
        {
            Phase.Value = MatchPhase.Hunting;
            Deadline.Value = NetworkManager.ServerTime.Time + Settings.Value.HuntingSeconds;
        }
        else Finish(PlayerRole.Prop);
    }

    void Finish(PlayerRole winner) { Winner.Value = winner; Phase.Value = MatchPhase.Finished; }

    public bool ReserveAutomaticWhistle(float duration)
    {
        double now = NetworkManager.ServerTime.Time;
        if (!IsServer || Phase.Value != MatchPhase.Hunting || now < nextAutomaticWhistle) return false;
        nextAutomaticWhistle = now + Mathf.Max(0, duration) + 8;
        return true;
    }

    public void TryToggleDoor(int index, PropHuntPlayer player, Vector3 eye)
    {
        if (!IsServer || map == null || player == null || player.Match != this || !player.CanUseDoor ||
            !float.IsFinite(eye.x) || !float.IsFinite(eye.y) || !float.IsFinite(eye.z) ||
            index < 0 || index >= map.doors.Length || index >= doorStates.Count) return;
        var door = map.doors[index];
        double now = NetworkManager.ServerTime.Time;
        if (door == null || !door.CanUse(player, eye, now)) return;
        door.Used(now);
        doorStates[index] = !doorStates[index];
        DoorCueClientRpc(index);
    }

    [ClientRpc]
    void DoorCueClientRpc(int index)
    {
        if (map != null && index >= 0 && index < map.doors.Length && map.doors[index] != null)
            DoorAudio.PlayAt(map.doors[index].transform.position);
    }

    void DoorChanged(NetworkListEvent<bool> change)
    {
        if (map == null) return;
        if (change.Type == NetworkListEvent<bool>.EventType.Value && change.Index >= 0 && change.Index < map.doors.Length)
            map.doors[change.Index]?.Apply(change.Value);
        else ApplyDoors();
    }

    void ApplyDoors()
    {
        if (map == null) return;
        for (int i = 0; i < map.doors.Length && i < doorStates.Count; i++) map.doors[i]?.Apply(doorStates[i], true);
    }

    public override void OnNetworkDespawn() => doorStates.OnListChanged -= DoorChanged;

    public void ReturnToLobby()
    {
        if (IsServer && Application.CanStreamedLevelBeLoaded(lobbyScene))
        {
            PartyLoadingScreen.Show();
            if (NetworkManager.SceneManager.LoadScene(lobbyScene, LoadSceneMode.Single) != SceneEventProgressStatus.Started)
                PartyLoadingScreen.Cancel();
        }
    }
}
