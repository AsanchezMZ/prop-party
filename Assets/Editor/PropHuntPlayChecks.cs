using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Batch host integration check; a second real process is still required to verify remote replication.
public static class PropHuntPlayChecks
{
    const string Key = "PropHunt.PlayChecks";
    static int stage;
    static double deadline;
    static PropHuntPlayer bot;
    static string lobbyScene;
    static int exitCode;
    static double shutdownDeadline;

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
                var settings = lobby.Settings.Value;
                settings.Health = 3;
                Require(lobby.ApplySettings(settings), "Host can change settings");
                Require(!lobby.Players[0].Ready, "Changing settings clears ready state");
                Require(lobby.Settings.Value.Health == 3, "Session settings change without mutating rules");
                network.GetComponent<GameBootstrap>().SessionSettings = lobby.Settings.Value;
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
                var waitingHunter = match.Players.First(p => p.Role.Value == PlayerRole.Hunter);
                Require(!waitingHunter.CanHunt, "Hunter shooting is disabled during the waiting screen.");
                int shotsBefore = UnityEngine.Object.FindObjectsByType<HunterProjectile>(FindObjectsSortMode.None).Length;
                waitingHunter.ThrowServerRpc(Vector3.forward);
                Require(UnityEngine.Object.FindObjectsByType<HunterProjectile>(FindObjectsSortMode.None).Length == shotsBefore,
                    "Even a direct server RPC cannot fire during hiding.");
                var prop = match.Players.First(p => p.Role.Value == PlayerRole.Prop);
                Require(prop.Health.Value == 3, "Lobby health settings reach the map");
                CheckCameras(match);
                var root = UnityEngine.Object.FindAnyObjectByType<MatchHUD>().GetComponent<UIDocument>().rootVisualElement;
                Require(root.Q("crosshair") != null && root.Q<Button>("whistle") != null && root.Q("toasts") != null, "HUD UXML contains crosshair, whistle button and toasts");
                prop.FreezeServerRpc(); Require(prop.Frozen.Value, "Prop can suspend");
                match.Deadline.Value = network.ServerTime.Time;
                stage++; return;
            }
            if (stage == 3)
            {
                if (match.Phase.Value != MatchPhase.Hunting) return;
                var prop = match.Players.First(p => p.Role.Value == PlayerRole.Prop);
                CheckRiskAndSteps(match, prop);
                int oldScore = prop.Score.Value;
                prop.ManualWhistleServerRpc();
                Require(prop.Score.Value == oldScore + match.Rules.manualWhistlePoints, "Manual whistle awards points");
                prop.ManualWhistleServerRpc();
                Require(prop.Score.Value == oldScore + match.Rules.manualWhistlePoints, "Whistle spam cannot award points during cooldown");
                Require(prop.NextManualWhistle.Value > network.ServerTime.Time, "Whistle cooldown uses server time");
                var hunter = match.Players.First(p => p.Role.Value == PlayerRole.Hunter);
                int hunterScore = hunter.Score.Value;
                CheckDoorSound(hunter);
                prop.Hit(999, hunter);
                Require(hunter.Score.Value == hunterScore + match.Rules.capturePoints, "Capture credits the hunter");
                Require(prop.Health.Value == 0, "Capture removes health");
                Require(prop.Role.Value == (match.Settings.Value.ConvertCaptured ? PlayerRole.Hunter : PlayerRole.Spectator), "Capture follows configured rule");
                stage++; return;
            }
            if (stage == 4)
            {
                if (match.Phase.Value != MatchPhase.Finished) return;
                Require(match.Winner.Value == PlayerRole.Hunter, "Hunters win when no props remain");
                Require(match.BeginRound(), "Round can restart");
                Require(match.Players.Count(p => p.Role.Value == PlayerRole.Prop) == 1, "Restart restores roles");
                Require(match.Players.All(p => p.Score.Value == 0), "Restart resets round scores");
                match.ReturnToLobby(); stage++; return;
            }
            if (stage == 5 && SceneManager.GetActiveScene().name == lobbyScene)
            {
                var lobby = UnityEngine.Object.FindAnyObjectByType<LobbyManager>();
                if (lobby == null || !lobby.IsSpawned || lobby.Players.Count == 0) return;
                Require(network.IsListening, "Host survives lobby return");
                var resultsAudio = PartyAudio.Instance.GetComponentsInChildren<AudioSource>().First(source => source.name == "Results");
                Require(!resultsAudio.isPlaying, "Result audio must stop when returning to the lobby.");
                Require(lobby.Settings.Value.Health == 3, "Session settings survive lobby return");
                End(true, "Host integration checks passed: lobby settings, cameras, HUD, proximity/LOS scoring, walls, automatic/manual whistles, cooldown, hunter-only footsteps, capture, victory, score reset and lobby return.");
            }
        }
        catch (Exception exception) { End(false, exception.ToString()); }
    }

    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

    static void CheckDoorSound(PropHuntPlayer hunter)
    {
        var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            door.transform.position = hunter.transform.position + Vector3.right;
            door.AddComponent<Rigidbody>().useGravity = false;
            door.AddComponent<HingeJoint>();
            Physics.SyncTransforms();
            hunter.DoorKnockServerRpc(door.transform.position);
            double acceptedUntil = (double)typeof(PropHuntPlayer).GetField("nextDoorSound", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(hunter);
            Require(acceptedUntil > NetworkManager.Singleton.ServerTime.Time, "A nearby hinged door accepts the validated sound request.");
            hunter.DoorKnockServerRpc(door.transform.position);
            Require((double)typeof(PropHuntPlayer).GetField("nextDoorSound", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(hunter) == acceptedUntil,
                "Repeated contact cannot spam the door sound.");
        }
        finally { UnityEngine.Object.Destroy(door); }
    }


    static void CheckCameras(PropHuntMatch match)
    {
        var prop = match.Players.First(p => p.Role.Value == PlayerRole.Prop);
        var hunter = match.Players.First(p => p.Role.Value == PlayerRole.Hunter);
        var movement = prop.GetComponent<PlayerController>();
        var camera = prop.GetComponentInChildren<Camera>();
        var listener = prop.GetComponentInChildren<AudioListener>();
        bool enabled = camera.enabled, audio = listener.enabled;
        bool initial = movement.FirstPerson;
        for (int i = 0; i < 100; i++)
        {
            movement.TogglePerspective();
            Require(movement.FirstPerson == (i % 2 == 0 ? !initial : initial), "Prop can toggle both perspectives");
            Require(camera.enabled == enabled && listener.enabled == audio, "Changing perspective does not toggle camera/listener activation");
        }
        hunter.GetComponent<PlayerController>().TogglePerspective();
        Require(hunter.GetComponent<PlayerController>().FirstPerson, "Hunter cannot switch out of FPV");
        // The extra local fixture shares host ownership; mute its camera to model a remote player.
        bot.GetComponentInChildren<Camera>().enabled = false;
        bot.GetComponentInChildren<AudioListener>().enabled = false;
    }

    static void Invoke(PropHuntPlayer player, string method, params object[] args) =>
        typeof(PropHuntPlayer).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(player, args);

    static void CheckRiskAndSteps(PropHuntMatch match, PropHuntPlayer prop)
    {
        var hunter = match.Players.First(p => p.Role.Value == PlayerRole.Hunter);
        var origin = new Vector3(1000, 1000, 1000);
        hunter.GetComponent<PlayerController>().Teleport(origin, Quaternion.identity);
        prop.GetComponent<PlayerController>().Teleport(origin + Vector3.forward * 4, Quaternion.identity);
        hunter.AimDirection.Value = (prop.GetComponent<CharacterController>().bounds.center - hunter.GetComponent<PlayerController>().EyePosition).normalized;
        Physics.SyncTransforms();
        prop.HunterNearby.Value = prop.InHunterSight.Value = false;
        double now = NetworkManager.Singleton.ServerTime.Time;
        int score = prop.Score.Value;
        Invoke(prop, "UpdateRisk", now);
        Invoke(prop, "UpdateRisk", now + 0.9);
        Require(prop.HunterNearby.Value && prop.InHunterSight.Value && prop.Score.Value == score, "No risk points before a complete interval");
        Invoke(prop, "UpdateRisk", now + 1.01);
        Require(prop.Score.Value == score + match.Rules.visiblePoints, "Visibility awards after one second");
        Invoke(prop, "UpdateRisk", now + 3.02);
        Require(prop.Score.Value == score + 2 * match.Rules.visiblePoints + match.Rules.proximityPoints, "Proximity awards after three seconds and stacks with sight");
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            wall.transform.position = origin + new Vector3(0, 1, 2);
            wall.transform.localScale = new Vector3(3, 4, 0.4f);
            Physics.SyncTransforms();
            int beforeWall = prop.Score.Value;
            Invoke(prop, "UpdateRisk", now + 4.03);
            Require(prop.HunterNearby.Value && !prop.InHunterSight.Value && prop.Score.Value == beforeWall, "Wall blocks visibility points but not proximity status");
            hunter.AimDirection.Value = Vector3.back;
            wall.SetActive(false); Physics.SyncTransforms();
            Invoke(prop, "UpdateRisk", now + 4.1);
            Require(!prop.InHunterSight.Value, "Targets behind the hunter are not visible");
            typeof(PropHuntPlayer).GetField("nextWhistle", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(prop, now - 1);
            typeof(PropHuntPlayer).GetField("nextSense", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(prop, now + 100);
            int automaticScore = prop.Score.Value;
            Invoke(prop, "Update");
            Require(prop.Score.Value == automaticScore, "Automatic whistles award no points");
            wall.SetActive(true); wall.transform.position = origin + Vector3.down * 0.5f;
            wall.transform.localScale = new Vector3(20, 1, 20); Physics.SyncTransforms();
            int hunterSteps = 0, propSteps = 0;
            Action<int> hunterCue = cue => { if (cue == 4) hunterSteps++; };
            Action<int> propCue = cue => { if (cue == 4) propSteps++; };
            hunter.Cue += hunterCue; prop.Cue += propCue;
            try
            {
                Invoke(hunter, "UpdateSteps"); Invoke(prop, "UpdateSteps");
                for (int i = 0; i < 4; i++)
                {
                    hunter.transform.position += Vector3.right * 0.6f;
                    prop.transform.position += Vector3.right * 0.6f;
                    Physics.SyncTransforms(); Invoke(hunter, "UpdateSteps"); Invoke(prop, "UpdateSteps");
                }
                Require(hunterSteps > 0 && propSteps == 0, "Only moving grounded hunters emit footsteps");
            }
            finally { hunter.Cue -= hunterCue; prop.Cue -= propCue; }
        }
        finally { UnityEngine.Object.DestroyImmediate(wall); }
    }
    static void End(bool success, string message)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= Log;
        SessionState.SetBool(Key, false);
        Debug.Log(message);
        exitCode = success ? 0 : 1;
        shutdownDeadline = EditorApplication.timeSinceStartup + 5;
        if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
        EditorApplication.update += FinishShutdown;
    }

    static void FinishShutdown()
    {
        var network = NetworkManager.Singleton;
        if (network != null && network.ShutdownInProgress && EditorApplication.timeSinceStartup < shutdownDeadline) return;
        EditorApplication.update -= FinishShutdown;
        EditorApplication.Exit(exitCode);
    }
}
