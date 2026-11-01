using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// Batch host integration check; a second real process is still required to verify remote replication.
public static class PropHuntPlayChecks
{
    const string Key = "PropHunt.PlayChecks";
    static int stage;
    static double deadline;
    static PropHuntPlayer bot;
    static string lobbyScene;

    public static void Run()
    {
        foreach (var entry in EditorBuildSettings.scenes.Where(s => s.enabled))
        {
            EditorSceneManager.OpenScene(entry.path);
            if (UnityEngine.Object.FindAnyObjectByType<LobbyManager>() == null) continue;
            SessionState.SetBool(Key, true);
            Resume();
            EditorApplication.EnterPlaymode();
            return;
        }
        throw new InvalidOperationException("No enabled lobby scene.");
    }

    [InitializeOnLoadMethod]
    static void Resume()
    {
        if (!SessionState.GetBool(Key, false)) return;
        stage = 0;
        deadline = EditorApplication.timeSinceStartup + 90;
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= Log;
        EditorApplication.update += Tick;
        Application.logMessageReceived += Log;
    }

    static void Log(string message, string stack, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) End(false, message + "\n" + stack);
    }

    static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Host integration check timed out at stage " + stage);
            if (!EditorApplication.isPlaying || Time.frameCount < 5) return;
            var network = NetworkManager.Singleton;
            if (stage == 0)
            {
                lobbyScene = SceneManager.GetActiveScene().name;
                Require(network.StartHost(), "Host starts"); stage++; return;
            }
            if (stage == 1)
            {
                var lobby = UnityEngine.Object.FindAnyObjectByType<LobbyManager>();
                if (!network.IsConnectedClient || lobby == null || !lobby.IsSpawned || lobby.Players.Count == 0) return;
                Require(!lobby.CanStart, "Single player cannot start");
                lobby.ToggleReadyServerRpc();
                Require(lobby.Players[0].Ready, "Ready request reaches server");
                Require(lobby.Maps.Length > 0, "Configured maps exist");
                Require(network.SceneManager.LoadScene(lobby.Maps[0].sceneName, LoadSceneMode.Single) == SceneEventProgressStatus.Started, "Network map load starts");
                stage++; return;
            }
            var match = UnityEngine.Object.FindAnyObjectByType<PropHuntMatch>();
            if (stage == 2)
            {
                if (match == null || !match.IsSpawned || network.LocalClient.PlayerObject == null) return;
                var host = network.LocalClient.PlayerObject.GetComponent<PropHuntPlayer>();
                Require(host != null, "Player has gameplay component");
                var prefab = network.NetworkConfig.Prefabs.NetworkPrefabsLists.SelectMany(list => list.PrefabList)
                    .Select(entry => entry.Prefab).First(p => p != null && p.GetComponent<PropHuntPlayer>() != null);
                bot = UnityEngine.Object.Instantiate(prefab, host.transform.position + Vector3.right * 3, Quaternion.identity).GetComponent<PropHuntPlayer>();
                bot.NetworkObject.Spawn();
                Require(match.BeginRound(), "Two players start a round");
                Require(match.Phase.Value == MatchPhase.Hiding, "Hiding phase starts");
                var prop = match.Players.First(p => p.Role.Value == PlayerRole.Prop);
                prop.FreezeServerRpc(); Require(prop.Frozen.Value, "Prop can suspend");
                match.Deadline.Value = network.ServerTime.Time;
                stage++; return;
            }
            if (stage == 3)
            {
                if (match.Phase.Value != MatchPhase.Hunting) return;
                var prop = match.Players.First(p => p.Role.Value == PlayerRole.Prop);
                prop.Hit(999);
                Require(prop.Health.Value == 0, "Capture removes health");
                Require(prop.Role.Value == (match.Rules.capturedBecomeHunters ? PlayerRole.Hunter : PlayerRole.Spectator), "Capture follows configured rule");
                stage++; return;
            }
            if (stage == 4)
            {
                if (match.Phase.Value != MatchPhase.Finished) return;
                Require(match.Winner.Value == PlayerRole.Hunter, "Hunters win when no props remain");
                Require(match.BeginRound(), "Round can restart");
                Require(match.Players.Count(p => p.Role.Value == PlayerRole.Prop) == 1, "Restart restores roles");
                match.ReturnToLobby(); stage++; return;
            }
            if (stage == 5 && SceneManager.GetActiveScene().name == lobbyScene)
            {
                var lobby = UnityEngine.Object.FindAnyObjectByType<LobbyManager>();
                if (lobby == null || !lobby.IsSpawned || lobby.Players.Count == 0) return;
                Require(network.IsListening, "Host survives lobby return");
                End(true, "Host integration checks passed: lobby, ready, map load, roles, freeze, capture, victory, restart, lobby return.");
            }
        }
        catch (Exception exception) { End(false, exception.ToString()); }
    }

    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void End(bool success, string message)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= Log;
        SessionState.SetBool(Key, false);
        Debug.Log(message);
        EditorApplication.Exit(success ? 0 : 1);
    }
}
