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
    public override void OnNetworkSpawn()
    {
        if (!IsServer || rules == null) return;
        var bootstrap = NetworkManager.GetComponent<GameBootstrap>();
        Settings.Value = (bootstrap != null && bootstrap.SessionSettings.HasValue ? bootstrap.SessionSettings.Value : MatchSettings.From(rules)).Validated();
    }
    readonly List<PropHuntPlayer> players = new();
    bool lastHiderAnnounced;
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
        lastHiderAnnounced = players.Count - hunters <= 1;
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
        if (props == 1 && !lastHiderAnnounced)
        { lastHiderAnnounced = true; LastHiderClientRpc(); }
        if (SecondsLeft > 0) return;
        if (Phase.Value == MatchPhase.Hiding)
        {
            Phase.Value = MatchPhase.Hunting;
            Deadline.Value = NetworkManager.ServerTime.Time + Settings.Value.HuntingSeconds;
        }
        else Finish(PlayerRole.Prop);
    }

    void Finish(PlayerRole winner) { Winner.Value = winner; Phase.Value = MatchPhase.Finished; }

    [ClientRpc]
    void LastHiderClientRpc() => PartyAudio.Instance?.Play(PartyAudio.Settings?.lastHider);

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
