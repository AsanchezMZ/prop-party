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
        RunMap("");
    }

    public static void RunApartment()
    {
        RunMap("Assets/Scenes/maps/Map_Apartment.unity");
    }

    static void RunMap(string scenePath)
    {
        SessionState.SetString(Key + ".Map", scenePath);
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
                var audio = PartyAudio.Instance;
                var music = audio.GetComponentsInChildren<AudioSource>().First(source => source.name == "Music");
                var readySetter = typeof(PartyLoadingScreen).GetProperty("Ready").GetSetMethod(true);
                bool wasReady = PartyLoadingScreen.Ready;
                try
                {
                    readySetter.Invoke(null, new object[] { false });
                    music.volume = 1;
                    typeof(PartyAudio).GetMethod("UpdateMusic", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(audio, null);
                    Require(!music.isPlaying && music.volume == 0, "Loading must immediately stop music, without a fade-out tail");
                }
                finally { readySetter.Invoke(null, new object[] { wasReady }); }
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
                CheckMapSelection(lobby);
                var settings = lobby.Settings.Value;
                settings.Health = 3;
                Require(lobby.ApplySettings(settings), "Host can change settings");
                Require(!lobby.Players[0].Ready, "Changing settings clears ready state");
                Require(lobby.Settings.Value.Health == 3, "Session settings change without mutating rules");
                network.GetComponent<GameBootstrap>().SessionSettings = lobby.Settings.Value;
                Require(lobby.Maps.Length > 0, "Configured maps exist");
                string requestedMap = SessionState.GetString(Key + ".Map", "");
                var map = string.IsNullOrEmpty(requestedMap) ? lobby.Maps[0] : lobby.Maps.Single(m => m != null && m.sceneName == requestedMap);
                Require(network.SceneManager.LoadScene(map.sceneName, LoadSceneMode.Single) == SceneEventProgressStatus.Started, "Network map load starts");
                stage++; return;
            }
            var match = UnityEngine.Object.FindAnyObjectByType<PropHuntMatch>();
            if (stage == 2)
            {
                if (match == null || !match.IsSpawned || network.LocalClient.PlayerObject == null) return;
                var host = network.LocalClient.PlayerObject.GetComponent<PropHuntPlayer>();
                Require(host != null, "Player has gameplay component");
                if (!host.AssetsReady.Value) return;
                var prefab = network.NetworkConfig.Prefabs.NetworkPrefabsLists.SelectMany(list => list.PrefabList)
                    .Select(entry => entry.Prefab).First(p => p != null && p.GetComponent<PropHuntPlayer>() != null);
                bot = UnityEngine.Object.Instantiate(prefab, host.transform.position + Vector3.right * 3, Quaternion.identity).GetComponent<PropHuntPlayer>();
                bot.NetworkObject.Spawn(true);
                bot.AssetsReady.Value = false;
                Require(!match.BeginRound(), "Round cannot start before every player prepares its assets");
                bot.AssetsReady.Value = true; // The synthetic bot has no remote loading screen.
                Require(match.BeginRound(), "Two players start a round");
                Require(match.Phase.Value == MatchPhase.Hiding, "Hiding phase starts");
                CheckBlackout(match);
                CheckProjectileLaunch(network.LocalClient.PlayerObject.GetComponent<PropHuntPlayer>());
                var waitingHunter = match.Players.First(p => p.Role.Value == PlayerRole.Hunter);
                Require(!waitingHunter.CanHunt, "Hunter shooting is disabled during the waiting screen.");
                int shotsBefore = UnityEngine.Object.FindObjectsByType<HunterProjectile>(FindObjectsSortMode.None).Length;
                waitingHunter.ThrowServerRpc(Vector3.forward);
                Require(UnityEngine.Object.FindObjectsByType<HunterProjectile>(FindObjectsSortMode.None).Length == shotsBefore,
                    "Even a direct server RPC cannot fire during hiding.");
                var prop = match.Players.First(p => p.Role.Value == PlayerRole.Prop);
                Require(prop.Health.Value == 3, "Lobby health settings reach the map");
                CheckCameras(match);
                CheckFineRotation(prop);
                CheckVisualEffects();
                var root = UnityEngine.Object.FindAnyObjectByType<MatchHUD>().GetComponent<UIDocument>().rootVisualElement;
                Require(root.Q("crosshair") != null && root.Q<Button>("whistle") != null && root.Q("toasts") != null, "HUD UXML contains crosshair, whistle button and toasts");
                prop.FreezeServerRpc(); Require(prop.Frozen.Value, "Prop can suspend");
                match.Deadline.Value = network.ServerTime.Time;
                stage++; return;
            }
            if (stage == 3)
            {
                if (match.Phase.Value != MatchPhase.Hunting) return;
                CheckSpectatorMusic(match);
                var prop = match.Players.First(p => p.Role.Value == PlayerRole.Prop);
                CheckRiskAndSteps(match, prop);
                int oldScore = prop.Score.Value;
                double automaticDeadline = prop.NextAutomaticWhistle.Value;
                double automaticStart = prop.AutomaticWhistleStartedAt.Value;
                prop.ManualWhistleServerRpc();
                Require(prop.NextAutomaticWhistle.Value == automaticDeadline && prop.AutomaticWhistleStartedAt.Value == automaticStart,
                    "Manual whistle recharge never changes the automatic countdown.");
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
                CheckResultPresentation();
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
                lobby.Players.Add(new LobbyPlayer { ClientId = ulong.MaxValue });
                var ui = UnityEngine.Object.FindAnyObjectByType<LobbyUI>();
                typeof(LobbyUI).GetMethod("Leave", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(ui, null);
                int playersBefore = lobby.Players.Count;
                typeof(LobbyManager).GetMethod("RemovePlayer", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(lobby, new object[] { network.LocalClientId });
                Require(lobby.Players.Count == playersBefore, "Shutdown callbacks do not mutate replicated lobby state");
                typeof(LobbyUI).GetMethod("Leave", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(ui, null);
                typeof(LobbyUI).GetMethod("Disconnected", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(ui, new object[] { network.LocalClientId });
                Require(ui.GetComponent<UIDocument>().rootVisualElement.Q<Label>("status").text.Contains("Has salido"),
                    "Repeated shutdown callbacks preserve the intentional leave message");
                stage++; return;
            }
            if (stage == 6)
            {
                if (network.IsListening || network.ShutdownInProgress) return;
                var ui = UnityEngine.Object.FindAnyObjectByType<LobbyUI>();
                if (!ui.GetComponent<UIDocument>().rootVisualElement.Q<Button>("host").enabledSelf) return;
                typeof(LobbyUI).GetMethod("Connect", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(ui, new object[] { true });
                stage++; return;
            }
            if (stage == 7)
            {
                var lobby = UnityEngine.Object.FindAnyObjectByType<LobbyManager>();
                if (!network.IsConnectedClient || lobby == null || !lobby.IsSpawned || lobby.Players.Count == 0) return;
                Require(lobby.Players.Count == 1, "Reopened lobby contains only the new host");
                var ui = UnityEngine.Object.FindAnyObjectByType<LobbyUI>();
                typeof(LobbyUI).GetMethod("Leave", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(ui, null);
                stage++; return;
            }
            if (stage == 8 && !network.IsListening && !network.ShutdownInProgress)
            {
                End(true, "Host integration checks passed, including lobby close, reopen and close without errors.");
            }
        }
        catch (Exception exception) { End(false, exception.ToString()); }
    }

    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

    static void CheckResultPresentation()
    {
        var hud = UnityEngine.Object.FindAnyObjectByType<MatchHUD>();
        var root = hud.GetComponent<UIDocument>().rootVisualElement;
        var update = typeof(MatchHUD).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
        var started = typeof(MatchHUD).GetField("phaseStartedAt", BindingFlags.NonPublic | BindingFlags.Instance);
        update.Invoke(hud, null);
        foreach (float elapsed in new[] { 0, 2.9f, 3.1f, 5.9f, 6.1f, 7f })
        {
            started.SetValue(hud, Time.unscaledTime - elapsed);
            update.Invoke(hud, null);
            Require((root.Q("winner-splash").style.display.value == DisplayStyle.Flex) == (elapsed >= 3 && elapsed < 6),
                "Only the three-to-six-second interval shows the winner title.");
            Require((root.Q("actions").style.display.value == DisplayStyle.Flex) == (elapsed >= 6),
                "Unlocked cursor and spectator menus cannot skip the delayed summary.");
            if (elapsed == 6.1f)
                Require(root.Q("actions").style.opacity.value > 0 && root.Q("actions").style.opacity.value < 1 &&
                    root.Q("result-paper").style.translate.value.y.value > 0,
                    "The paper summary fades in while sliding into position.");
            if (elapsed == 7)
                Require(root.Q("actions").style.opacity.value == 1 && root.Q("result-paper").style.translate.value.y.value == 0,
                    "The paper settles fully opaque without perpetual motion.");
        }
        var local = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PropHuntPlayer>();
        var role = local.Role.Value;
        var outcome = typeof(MatchHUD).GetField("resultRole", BindingFlags.NonPublic | BindingFlags.Instance);
        var original = outcome.GetValue(hud);
        try
        {
            local.Role.Value = PlayerRole.Spectator;
            outcome.SetValue(hud, PlayerRole.Hunter);
            update.Invoke(hud, null);
            Require(root.Q<Label>("winner-title").text == MatchHudStrings.VictoryResult && root.Q("winner-splash").ClassListContains("is-win"),
                "A spectator retains victory for their last active winning team.");
            outcome.SetValue(hud, PlayerRole.Prop);
            update.Invoke(hud, null);
            Require(root.Q<Label>("winner-title").text == MatchHudStrings.DefeatResult && root.Q("winner-splash").ClassListContains("is-lose"),
                "A losing player receives the defeat title and red theme.");
        }
        finally { local.Role.Value = role; outcome.SetValue(hud, original); }
    }

    static void CheckFineRotation(PropHuntPlayer prop)
    {
        var movement = prop.GetComponent<PlayerController>();
        var look = typeof(PlayerController).GetMethod("UpdateLook", BindingFlags.NonPublic | BindingFlags.Instance);
        var throttle = typeof(PropHuntPlayer).GetField("nextFineRotation", BindingFlags.NonPublic | BindingFlags.Instance);
        var pitch = typeof(PlayerController).GetField("pitch", BindingFlags.NonPublic | BindingFlags.Instance);
        int disguise = prop.Disguise.Value;
        float rotation = prop.Rotation.Value;
        var bodyRotation = prop.transform.rotation;
        float cameraPitch = (float)pitch.GetValue(movement);
        try
        {
            prop.Disguise.Value = 0;
            var controller = prop.GetComponent<CharacterController>();
            float originalSkinWidth = controller.skinWidth;
            prop.FineRotateServerRpc(1);
            Require(prop.Rotation.Value == rotation, "Fine rotation requires a disguise.");
            var entries = new SerializedObject(prop.Match.Map.definition.catalog).FindProperty("props");
            prop.Disguise.Value = ((Prop)entries.GetArrayElementAtIndex(0).objectReferenceValue).id;
            Require(Mathf.Approximately(controller.skinWidth, Mathf.Min(originalSkinWidth, Mathf.Max(0.01f, controller.radius * 0.1f))),
                "Disguise movement uses a size-appropriate positive skin width, never larger than the human's.");
            prop.Rotation.Value = 359.75f;
            prop.FineRotateServerRpc(float.NaN); prop.FineRotateServerRpc(float.PositiveInfinity); prop.FineRotateServerRpc(16);
            Require(prop.Rotation.Value == 359.75f, "Fine rotation rejects nonfinite and oversized network input.");
            look.Invoke(movement, new object[] { 2f, 10f, true });
            Require(Mathf.Abs(Mathf.DeltaAngle(prop.Rotation.Value, 0.25f)) < 0.001f,
                "Mouse rotation supports sub-degree changes across the zero-degree boundary.");
            Require(prop.transform.rotation == bodyRotation && (float)pitch.GetValue(movement) == cameraPitch,
                "Fine prop rotation changes neither body yaw nor camera pitch.");
            prop.FineRotateServerRpc(1);
            Require(Mathf.Abs(Mathf.DeltaAngle(prop.Rotation.Value, 0.25f)) < 0.001f, "Fine rotation requests are rate limited.");
            throttle.SetValue(prop, 0d);
            var phase = prop.Match.Phase.Value;
            try
            {
                prop.Match.Phase.Value = MatchPhase.Waiting;
                prop.FineRotateServerRpc(1);
                Require(Mathf.Abs(Mathf.DeltaAngle(prop.Rotation.Value, 0.25f)) < 0.001f, "Fine rotation is blocked outside active round phases.");
            }
            finally { prop.Match.Phase.Value = phase; }
            prop.Disguise.Value = 0;
            Require(Mathf.Approximately(controller.skinWidth, originalSkinWidth), "Removing a disguise restores the human's skin width.");
        }
        finally
        {
            prop.Rotation.Value = rotation; prop.Disguise.Value = disguise;
            throttle.SetValue(prop, 0d);
        }
    }

    static void CheckBlackout(PropHuntMatch match)
    {
        var hud = UnityEngine.Object.FindAnyObjectByType<MatchHUD>();
        var root = hud.GetComponent<UIDocument>().rootVisualElement.Q("hud-root");
        var local = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PropHuntPlayer>();
        var role = local.Role.Value;
        var update = typeof(MatchHUD).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
        try
        {
            local.Role.Value = PlayerRole.Hunter;
            update.Invoke(hud, null);
            Require(root.Q("identity-card").ClassListContains("is-hunter"), "Hunter identity uses the red theme.");
            Require(root.ClassListContains("is-blackout") && root.Q("blackout").style.display.value == DisplayStyle.Flex,
                "Hiding hunters enable the exclusive blackout stylesheet state.");
            local.Role.Value = PlayerRole.Prop;
            update.Invoke(hud, null);
            Require(root.Q("identity-card").ClassListContains("is-prop") && !root.Q("identity-card").ClassListContains("is-hunter"),
                "Prop identity switches to blue without retaining the hunter theme.");
            Require(!root.ClassListContains("is-blackout"), "Props keep their normal HUD during hiding.");
            local.Role.Value = PlayerRole.Spectator;
            update.Invoke(hud, null);
            Require(root.Q("identity-card").ClassListContains("is-spectator"), "Spectator identity uses the yellow theme.");
            local.Role.Value = PlayerRole.Hunter;
            match.Phase.Value = MatchPhase.Hunting;
            update.Invoke(hud, null);
            Require(!root.ClassListContains("is-blackout") && root.Q("blackout").style.display.value == DisplayStyle.None,
                "Releasing hunters restores the normal HUD.");
        }
        finally
        {
            local.Role.Value = role;
            match.Phase.Value = MatchPhase.Hiding;
            update.Invoke(hud, null);
        }
    }

    static void CheckProjectileLaunch(PropHuntPlayer owner)
    {
        var settings = AssetDatabase.LoadAssetAtPath<ThrowableDefinition>("Assets/PropHuntConfig/Throwable.asset");
        var shot = UnityEngine.Object.Instantiate(settings.projectilePrefab, Vector3.one * 1000, Quaternion.identity).GetComponent<HunterProjectile>();
        int prematureWrites = 0;
        Application.LogCallback capture = (message, stack, type) =>
        {
            if (message.Contains("NetworkVariable is written to")) prematureWrites++;
        };
        Application.logMessageReceived += capture;
        try
        {
            var origin = (NetworkVariable<Vector3>)typeof(HunterProjectile).GetField("launchOrigin", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(shot);
            var velocity = (NetworkVariable<Vector3>)typeof(HunterProjectile).GetField("launchVelocity", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(shot);
            shot.Launch(owner, Vector3.forward, settings);
            Require(origin.Value == Vector3.zero && velocity.Value == Vector3.zero && prematureWrites == 0,
                "Launch before Spawn prepares only local state, without premature network writes.");
            shot.NetworkObject.Spawn(true);
            Require(origin.Value == shot.transform.position && velocity.Value == Vector3.forward * settings.speed && prematureWrites == 0,
                "Spawn initializes replicated launch state after NGO binds the variables.");
        }
        finally
        {
            Application.logMessageReceived -= capture;
            if (shot.IsSpawned) shot.NetworkObject.Despawn();
            else UnityEngine.Object.Destroy(shot.gameObject);
        }
    }

    static void CheckMapSelection(LobbyManager lobby)
    {
        var field = typeof(LobbyManager).GetField("mapDefinitions", BindingFlags.NonPublic | BindingFlags.Instance);
        var original = lobby.Maps;
        var invalid = ScriptableObject.CreateInstance<MapDefinition>();
        var randomState = UnityEngine.Random.state;
        try
        {
            field.SetValue(lobby, new[] { original[0], null, invalid, original[0] });
            lobby.SelectMap(LobbyManager.RandomMap);
            Require(lobby.SelectedMap.Value == LobbyManager.RandomMap && !lobby.Players[0].Ready,
                "Random mode is synchronized and clears ready state");
            var root = UnityEngine.Object.FindAnyObjectByType<LobbyUI>().GetComponent<UIDocument>().rootVisualElement;
            Require(root.Q<DropdownField>("maps").value == "Aleatorio" && root.Q<Label>("map-title").text == "MAPA ALEATORIO",
                "Random mode has a distinct selection and preview");
            var randomPreview = Resources.Load<Texture2D>("RandomMapPreview");
            Require(randomPreview != null && root.Q("map-preview").style.backgroundImage.value.texture == randomPreview &&
                root.Q("map-preview").ClassListContains("is-random"), "Random mode displays the question mark image without cropping");
            bool first = false, last = false;
            UnityEngine.Random.InitState(42);
            for (int i = 0; i < 100; i++)
            {
                int selected = lobby.ResolveMap();
                Require(selected == 0 || selected == 3, "Random selection excludes missing and unavailable maps");
                first |= selected == 0; last |= selected == 3;
            }
            Require(first && last, "All available entries can be drawn");
            field.SetValue(lobby, Array.Empty<MapDefinition>());
            Require(lobby.ResolveMap() == LobbyManager.RandomMap && !lobby.CanStart, "No available map prevents starting");
        }
        finally
        {
            field.SetValue(lobby, original);
            lobby.SelectMap(0);
            UnityEngine.Random.state = randomState;
            UnityEngine.Object.DestroyImmediate(invalid);
        }
    }

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


    static void CheckSpectatorMusic(PropHuntMatch match)
    {
        var audio = PartyAudio.Instance;
        var listener = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PropHuntPlayer>();
        var field = typeof(PartyAudio).GetField("config", BindingFlags.NonPublic | BindingFlags.Instance);
        var update = typeof(PartyAudio).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
        var reset = typeof(PartyAudio).GetMethod("FindMatch", BindingFlags.NonPublic | BindingFlags.Instance);
        var original = audio.Config;
        var role = listener.Role.Value; var phase = match.Phase.Value; var winner = match.Winner.Value;
        var config = ScriptableObject.CreateInstance<GameAudioConfig>();
        var spectator = AudioClip.Create("Spectator transition check", 8000, 1, 8000, false);
        var defeat = AudioClip.Create("Defeat transition check", 8000, 1, 8000, false);
        var music = audio.GetComponentsInChildren<AudioSource>().First(source => source.name == "Music");
        var results = audio.GetComponentsInChildren<AudioSource>().First(source => source.name == "Results");
        try
        {
            field.SetValue(audio, config);
            config.spectatorEnter = spectator; config.defeat = defeat;
            for (int sameFrame = 0; sameFrame < 2; sameFrame++)
            {
                results.clip = null;
                listener.Role.Value = PlayerRole.Hunter; match.Phase.Value = MatchPhase.Hunting;
                reset.Invoke(audio, null); update.Invoke(audio, null);
                listener.Role.Value = PlayerRole.Spectator;
                if (sameFrame == 0)
                {
                    music.volume = 0; update.Invoke(audio, null);
                    Require(music.clip == spectator && music.volume <= config.MusicVolume,
                        "Spectators use the quieter music channel, not effects volume.");
                }
                match.Winner.Value = PlayerRole.Prop; match.Phase.Value = MatchPhase.Finished;
                update.Invoke(audio, null);
                Require(results.clip == defeat && results.loop && music.clip == null && !music.isPlaying,
                    "Defeat replaces spectator music even when elimination and round end arrive together.");
            }
        }
        finally
        {
            music.Stop(); music.clip = null; results.Stop(); results.clip = null;
            listener.Role.Value = role; match.Winner.Value = winner; match.Phase.Value = phase;
            field.SetValue(audio, original); reset.Invoke(audio, null);
            UnityEngine.Object.DestroyImmediate(config);
            UnityEngine.Object.DestroyImmediate(spectator); UnityEngine.Object.DestroyImmediate(defeat);
        }
    }

    public static void CheckVisualEffects()
    {
        Require(Application.isPlaying, "Visual effect checks require Play Mode.");
        var existing = UnityEngine.Object.FindObjectsByType<PartyVfx>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var randomState = UnityEngine.Random.state;
        try
        {
            PartyVfx.EggImpact(new Vector3(1000, 1000, 1000), Vector3.right, false);
            var egg = UnityEngine.Object.FindObjectsByType<PartyVfx>(FindObjectsSortMode.None).First(effect => !existing.Contains(effect));
            Require(Vector3.Dot(egg.transform.forward, Vector3.right) > 0.99f, "Egg splash follows the surface normal.");
            Require(egg.GetComponentsInChildren<ParticleSystem>().Length == 5, "Eggs have separate shell, yolk, white, splash and ring layers.");
            var shell = egg.GetComponentsInChildren<ParticleSystemRenderer>().First(renderer => renderer.renderMode == ParticleSystemRenderMode.Mesh);
            Require(shell.mesh != null && shell.mesh.vertexCount > 3 && shell.sharedMaterial.shader.name == "PropParty/Layered Particles",
                "Shells use curved fragments and a build-included particle material, not primitive cubes.");
            for (int i = 0; i < PartyVfx.MaxActiveBursts + 2; i++) PartyVfx.Burst(Vector3.one * 1000, Color.cyan);
            var active = UnityEngine.Object.FindObjectsByType<PartyVfx>(FindObjectsSortMode.None);
            Require(active.Length <= PartyVfx.MaxActiveBursts, "Rapid-fire bursts retire the oldest effect before exceeding the budget.");
            foreach (var effect in active)
                Require(effect.GetComponentsInChildren<Collider>().Length == 0 && effect.GetComponentsInChildren<Light>().Length == 0,
                    "Visual effects add no physics colliders or realtime lights.");
            Require(randomState.Equals(UnityEngine.Random.state), "Cosmetic randomness must not change the gameplay random stream.");
        }
        finally
        {
            foreach (var effect in UnityEngine.Object.FindObjectsByType<PartyVfx>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!existing.Contains(effect)) UnityEngine.Object.DestroyImmediate(effect.gameObject);
        }
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
            prop.NextAutomaticWhistle.Value = now - 1;
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
