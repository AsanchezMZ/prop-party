using System;
using UnityEditor;
using UnityEngine;

public static class PropHuntChecks
{
    [MenuItem("PropHunt/Run deterministic prop checks")]
    public static void Run()
    {
        CheckMusicSelection();
        CheckPropCollisions();
        CheckHudAndPortraits();
        CheckPresentationAssets();
        CheckBuildCollisionAssets();
        CheckHudAndVfxDesign();
        CheckGameSettings();
        Require(FaceCapture.Crop(1920, 1080, false) == new Rect(0.21875f, 0, 0.5625f, 1), "Landscape webcam must crop centrally.");
        Require(FaceCapture.Crop(1080, 1920, false) == new Rect(0, 0.21875f, 1, 0.5625f), "Portrait webcam must crop centrally.");
        Require(FaceCapture.Crop(512, 512, true) == new Rect(0, 1, 1, -1), "Mirrored camera must flip vertically.");
        var settings = new MatchSettings { Hunters = -4, HidingSeconds = 999, HuntingSeconds = 0, WhistleSeconds = -1, Health = 90 }.Validated();
        Require(settings.Hunters == 1 && settings.HidingSeconds == 120 && settings.HuntingSeconds == 30 && settings.WhistleSeconds == 5 && settings.Health == 10,
            "Session settings must respect server bounds.");
        Require(new MatchSettings { HunterMissLimit = 100 }.Validated().HunterMissLimit == 50 &&
            new MatchSettings { HunterMissLimit = -1 }.Validated().HunterMissLimit == 1 &&
            new MatchSettings { HunterMissLimit = 0 }.Validated().HunterMissLimit == 0,
            "Hunter miss limits must be 1–50, with zero reserved for unlimited.");
        var newRules = ScriptableObject.CreateInstance<MatchRules>();
        try
        {
            var defaults = MatchSettings.From(newRules);
            Require(defaults.HunterMissLimit == 10 && defaults.IdentifyHiders, "New rounds default to ten misses and identification enabled.");
            var changed = defaults; changed.IdentifyHiders = false;
            Require(!defaults.Equals(changed), "Identification changes must replicate as new settings.");
            changed = defaults; changed.HunterMissLimit++;
            Require(!defaults.Equals(changed), "Hunter life changes must replicate as new settings.");
        }
        finally { UnityEngine.Object.DestroyImmediate(newRules); }
        Require(PropHuntPlayer.InView(Vector3.forward, Vector3.forward * 5, 75), "A hunter sees the center of their view.");
        Require(!PropHuntPlayer.InView(Vector3.forward, Vector3.back, 75), "A hunter cannot see behind them.");
        Require(!PropHuntPlayer.InView(Vector3.forward, Vector3.right, 75), "Outside-FOV props do not earn visibility points.");
        var go = new GameObject("Prop probability check");
        try
        {
            var prop = go.AddComponent<Prop>();
            prop.slotId = 19;
            var data = new SerializedObject(prop);
            var probability = data.FindProperty("spawnProbability");
            probability.floatValue = 0; data.ApplyModifiedPropertiesWithoutUndo();
            for (int seed = 1; seed <= 1000; seed++) { prop.ApplySeed(seed); Require(!go.activeSelf, "Probability zero must never spawn."); }
            probability.floatValue = 1; data.ApplyModifiedPropertiesWithoutUndo();
            for (int seed = 1; seed <= 1000; seed++) { prop.ApplySeed(seed); Require(go.activeSelf, "Probability one must always spawn."); }
            probability.floatValue = 0.5f; data.ApplyModifiedPropertiesWithoutUndo();
            int appearances = 0;
            for (int seed = 1; seed <= 1000; seed++)
            {
                prop.ApplySeed(seed); bool first = go.activeSelf;
                prop.ApplySeed(seed); Require(first == go.activeSelf, "Same seed must produce the same result.");
                if (first) appearances++;
            }
            Require(appearances > 400 && appearances < 600, "Distribution should approximate the configured probability.");
            probability.floatValue = 0; data.FindProperty("canDisappear").boolValue = false; data.ApplyModifiedPropertiesWithoutUndo();
            prop.ApplySeed(42); Require(go.activeSelf, "Permanent props must remain present.");
            Debug.Log("PropHunt checks passed: probability boundaries, deterministic replay, distribution, permanent props.");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    public static void CheckBuildCollisionAssets()
    {
        string scene = System.IO.File.ReadAllText("Assets/Scenes/maps/Map_Apartment.unity");
        string bathtub = System.IO.File.ReadAllText("Assets/Brick Project Studio/Apartment Kit/_Prefabs/Furniture/Bathroom/BathTub_Base_01.prefab");
        Require(System.Text.RegularExpressions.Regex.IsMatch(scene, @"--- !u!64 &2135681732 stripped\r?\nMeshCollider:") &&
            System.Text.RegularExpressions.Regex.IsMatch(bathtub, @"--- !u!64 &6976062566496198566\r?\nMeshCollider:") &&
            scene.Contains("interactionCollider: {fileID: 2135681732}"),
            "Shower door instance and source must both be MeshColliders, preserving the interaction reference.");
        foreach (string name in new[] { "BlueCar", "Brick", "Bench", "BigCar", "BigAssTree", "BigBlackCar", "Carreta", "Container",
            "FarolaD", "Fence", "Fountain", "Farola", "Phone", "ShittyCAR", "Slide", "SmallTree", "SmallerTree", "Spinner", "TrashCan", "CosaRara", "Map" })
            CheckImport("Assets/Imports/city/" + name + ".fbx.meta", "preBakeTriangleCollisionMesh");
        CheckImport("Assets/Imports/town/Map.fbx.meta", "preBakeTriangleCollisionMesh");
        foreach (string name in new[] { "libraries", "toilet", "tv", "tv_table", "small_table", "small_vase", "double_door" })
            CheckImport("Assets/Imports/haunted_house/" + name + ".fbx.meta", "preBakeTriangleCollisionMesh");
        foreach (string name in new[] { "big_base", "double_door_b" })
            CheckImport("Assets/Imports/haunted_house/" + name + ".fbx.meta", "preBakeConvexCollisionMesh");
        foreach (string name in new[] { "double_door_a", "door" })
            CheckImport("Assets/Imports/haunted_house/" + name + ".fbx.meta", "isReadable");
        string bathroom = "Assets/Brick Project Studio/Apartment Kit/Common/Models/Bathroom_Apt_01.fbx.meta";
        CheckImport(bathroom, "preBakeTriangleCollisionMesh");
        CheckImport(bathroom, "preBakeConvexCollisionMesh");
        Debug.Log("Build collision asset checks passed: shower source/instance types, readable door meshes and pre-baked collisions.");
    }

    static void CheckImport(string path, string setting) => Require(System.Text.RegularExpressions.Regex.IsMatch(
        System.IO.File.ReadAllText(path), @"(?m)^\s+" + setting + @": 1\r?$"), path + ": enable " + setting);

    [MenuItem("PropHunt/Run presentation checks")]
    public static void CheckPresentationAssets()
    {
        var loading = Resources.Load<UnityEngine.UIElements.VisualTreeAsset>("LoadingScreen");
        Require(loading != null, "Loading screen must be included in player builds.");
        var root = loading.CloneTree();
        Require(UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.ProgressBar>(root, "loading-progress") != null &&
            UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Button>(root, "loading-retry") != null &&
            UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Label>(root, "loading-status") != null,
            "Loading screen needs progress, retry and status controls; return is optional.");
        var trail = Resources.Load<Material>("EggTrail");
        Require(trail != null && trail.shader != null && trail.shader.name == "Universal Render Pipeline/Particles/Unlit" &&
            trail.GetFloat("_ZWrite") == 0 && trail.GetFloat("_Surface") == 1 && trail.renderQueue == 3000,
            "Egg trail must use a build-included transparent, non-depth-writing material.");
        var audio = AssetDatabase.LoadAssetAtPath<GameAudioConfig>("Assets/PropHuntConfig/Audio.asset");
        Require(audio != null && audio.uiClick != null && audio.disguise != null && audio.uiClick != audio.disguise &&
            AssetDatabase.GetAssetPath(audio.disguise).EndsWith("disguise.wav", StringComparison.OrdinalIgnoreCase),
            "Map selection must use distinct UI-click and disguise.wav clips.");
        Debug.Log("Presentation checks passed: loading UI, egg trail material and map audio.");
    }

    public static void CheckHudAndVfxDesign()
    {
        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>("Assets/UI/MatchHUD.uxml");
        Require(asset != null, "The redesigned HUD remains an authored UXML asset.");
        var root = asset.CloneTree();
        var whistle = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Button>(root, "whistle");
        Require(UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.VisualElement>(whistle, "whistle-recharge") != null &&
            UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Label>(whistle, "whistle-label") != null &&
            UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.VisualElement>(whistle, "whistle-fill") != null,
            "Manual recharge and the independent automatic countdown both live inside the button.");
        Require(MatchHUD.IdentificationDelay == 1.25f, "Player identification requires 1.25 seconds of continuous aim.");
        Require(MatchHUD.ResultStage(0) == 0 && MatchHUD.ResultStage(2.999f) == 0 && MatchHUD.ResultStage(3) == 1 &&
            MatchHUD.ResultStage(5.999f) == 1 && MatchHUD.ResultStage(6) == 2,
            "Results show notification first, the winner at three seconds and the summary at six seconds.");
        Require(MatchHUD.ResultTitleOpacity(3) == 0 && MatchHUD.ResultTitleOpacity(3.25f) > 0 &&
            MatchHUD.ResultTitleOpacity(3.25f) < 1 && MatchHUD.ResultTitleOpacity(4) == 1 &&
            MatchHUD.ResultTitleOpacity(5.7f) > 0 && MatchHUD.ResultTitleOpacity(5.7f) < 1 && MatchHUD.ResultTitleOpacity(6) == 0,
            "Result title fades in, holds, then fades out completely before the paper summary.");
        Require(MatchHUD.CountdownFraction(10, 10, 20) == 1 && MatchHUD.CountdownFraction(15, 10, 20) == 0.5f &&
            MatchHUD.CountdownFraction(20, 10, 20) == 0 && MatchHUD.CountdownFraction(25, 10, 20) == 0 &&
            MatchHUD.CountdownFraction(5, 10, 20) == 1 && MatchHUD.CountdownFraction(10, 10, 10) == 0,
            "Automatic countdown drains from full to zero, clamps during hiding and handles unset intervals.");
        foreach (var name in new[] { "role", "objective", "timer", "phase-label", "health", "score", "risk", "target-prop", "phase-banner", "winner-title", "result" })
            Require(UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Label>(root, name) != null, "Missing HUD label: " + name);
        foreach (var name in new[] { "whistle", "resume", "replay", "lobby", "leave" })
            Require(UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Button>(root, name) != null, "Missing HUD action: " + name);
        foreach (var name in new[] { "risk-halo", "hit-flash", "hit-marker", "health-pips", "clock-fill", "crosshair" })
        {
            var element = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.VisualElement>(root, name);
            Require(element != null && element.pickingMode == UnityEngine.UIElements.PickingMode.Ignore,
                "Decorative HUD layers must not intercept clicks: " + name);
        }
        Require(MatchHUD.FilledSegments(3, 5, 5) == 3 && MatchHUD.FilledSegments(25, 50, 10) == 5 &&
            MatchHUD.FilledSegments(1, 50, 10) == 1 && MatchHUD.FilledSegments(0, 10, 10) == 0 &&
            MatchHUD.FilledSegments(-4, 10, 10) == 0 && MatchHUD.FilledSegments(99, 10, 10) == 10 &&
            MatchHUD.FilledSegments(4, 0, 10) == 0,
            "Vital segments handle empty, unlimited and large hunter miss budgets.");
        Require(string.Format(MatchHudStrings.HealthFormat, 3) == "3 ♥" && MatchHudStrings.UnlimitedHealth == "∞ ♥",
            "Vitals show remaining lives followed by a heart, including unlimited hunter lives.");
        Require(UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Label>(root, "health-caption") == null,
            "Vitals no longer require or overwrite a health caption.");
        for (int bars = 1; bars <= 6; bars++)
            Require(MatchHUD.PresenceSegments(8 * (1 - (bars - 0.5f) / 6), 8, 6) == bars,
                "Every presence bar is reachable as the nearest hunter approaches.");
        Require(MatchHUD.PresenceSegments(8, 8, 6) == 0 &&
            MatchHUD.PresenceSegments(float.PositiveInfinity, 8, 6) == 0 &&
            MatchHUD.PresenceSegments(4, 8, 6) == 3 && MatchHUD.PresenceSegments(0, 8, 6) == 6 &&
            MatchHUD.PresenceSegments(50, 8, 6) == 0 && MatchHUD.PresenceSegments(4, 8, 10) == 5 &&
            MatchHUD.PresenceSegments(0, 8, 0) == 0,
            "Presence bar count depends only on distance, never on visual confirmation.");
        Require(!MatchHUD.IsPresenceAlert(0, false) && !MatchHUD.IsPresenceAlert(2, false) &&
            MatchHUD.IsPresenceAlert(3, false) && MatchHUD.IsPresenceAlert(6, false) && MatchHUD.IsPresenceAlert(0, true),
            "Heartbeat is active only for yellow or red presence, even when red has no filled bars.");
        foreach (var name in new[] { "score-heading", "hit-marker-label", "blackout-kicker", "blackout-title", "blackout-copy", "pause-heading", "ranking-heading" })
            Require(!string.IsNullOrWhiteSpace(UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Label>(root, name)?.text),
                "Static HUD labels are authored directly in UXML: " + name);
        // Detecta errores en los marcadores al editar los formatos, sin tocar preferencias ni escenas.
        foreach (var field in typeof(MatchHudStrings).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            if (field.Name.EndsWith("Format", StringComparison.Ordinal))
                string.Format((string)field.GetRawConstantValue(), 1, 2, 3);
        var shader = Resources.Load<Shader>("PartyParticles");
        Require(shader != null && shader.name == "PropParty/Layered Particles", "Particle shader must be included in builds, not found by an editor-only name lookup.");
        Require(PartyVfx.MaxActiveBursts <= 24, "Rapid-fire effects keep a bounded lifetime and active budget.");
        Debug.Log("HUD/VFX design checks passed: bindings, non-blocking overlays, vitals and build-included particle shader.");
    }

    public static void CheckGameSettings()
    {
        var menu = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>("Assets/UI/MainMenu.uxml");
        Require(menu != null, "Game settings live in the existing menu asset.");
        var root = menu.CloneTree();
        foreach (var name in new[] { "game-settings-button", "game-video-tab", "game-audio-tab", "game-settings-back", "game-settings-apply" })
            Require(UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Button>(root, name) != null, "Missing settings button: " + name);
        foreach (var name in new[] { "game-resolution", "game-fps" })
            Require(UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.DropdownField>(root, name) != null, "Missing video setting: " + name);
        foreach (var name in new[] { "game-master", "game-music", "game-effects" })
        {
            var slider = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.SliderInt>(root, name);
            Require(slider != null && slider.lowValue == 0 && slider.highValue == 100, "Audio sliders use percentage bounds.");
        }
        Require(UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.TextField>(root, "player-name") != null &&
            UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Button>(root, "face-button") != null,
            "Name and webcam keep their existing entry points.");
        var dialog = new GameSettingsDialog(root, _ => { });
        typeof(GameSettingsDialog).GetMethod("Open", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(dialog, null);
        Require(dialog.IsOpen, "Settings open with the current menu layout, without a quality dropdown.");
        dialog.Close();
        Require(!dialog.IsOpen, "Settings close without applying or changing saved preferences.");
        Require(GamePreferences.ValidFrameRate(-20) == -1 && GamePreferences.ValidFrameRate(0) == -1 &&
            GamePreferences.ValidFrameRate(5) == 30 && GamePreferences.ValidFrameRate(999) == 240 && GamePreferences.ValidFrameRate(144) == 144,
            "Saved frame limits remain valid, including unlimited.");
        var audio = ScriptableObject.CreateInstance<GameAudioConfig>();
        try
        {
            Require(Mathf.Approximately(audio.MusicVolume, audio.musicVolume * GamePreferences.MusicVolume) &&
                Mathf.Approximately(audio.EffectsVolume, audio.effectsVolume * GamePreferences.EffectsVolume) &&
                Mathf.Approximately(audio.DangerVolume, audio.dangerVolume * GamePreferences.EffectsVolume) &&
                Mathf.Approximately(audio.AmbienceVolume, audio.ambienceVolume * GamePreferences.EffectsVolume),
                "Local audio settings multiply authored levels without changing the config asset.");
        }
        finally { UnityEngine.Object.DestroyImmediate(audio); }
        Debug.Log("Game settings checks passed: menu controls, profile entry points, frame limits and audio multipliers.");
    }

    public static void CheckPropCollisions()
    {
        var go = new GameObject("Prop collision check");
        GameObject model = null;
        try
        {
            var prop = go.AddComponent<Prop>();
            model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var originalCollider = model.GetComponent<Collider>();
            Require(!prop.ConfigureDisguiseColliders(model), "Existing props default to the controller capsule.");
            Require(!originalCollider.enabled, "Cloned visual colliders must be disabled.");
            prop.visual = model.transform;
            model.transform.localScale = new Vector3(2, 4, 3);
            prop.disguiseRadius = float.NaN; prop.disguiseHeight = float.PositiveInfinity;
            prop.disguiseBoxSize = new Vector3(float.NaN, float.NegativeInfinity, float.NaN);
            prop.ValidateDisguiseDimensions();
            Require(prop.disguiseRadius == 1.5f && prop.disguiseHeight == 4 && prop.BoxSize == new Vector3(2, 4, 3),
                "Invalid saved dimensions recover from mesh bounds, including model scale.");
            prop.visual = null;
            prop.disguiseRadius = prop.disguiseHeight = float.NaN;
            prop.disguiseBoxSize = Vector3.one * float.NaN;
            prop.ValidateDisguiseDimensions();
            Require(prop.disguiseRadius == 0.4f && prop.disguiseHeight == 1 && prop.BoxSize == new Vector3(0.8f, 1, 0.8f),
                "Props without a mesh still recover finite playable dimensions.");
            model.transform.localScale = Vector3.one;

            prop.collisionShape = Prop.CollisionShape.Box;
            prop.disguiseBoxSize = new Vector3(-1, 2, 3);
            Require(prop.ConfigureDisguiseColliders(model), "Box mode creates a custom hitbox.");
            var boxes = model.GetComponents<BoxCollider>();
            var box = boxes[boxes.Length - 1];
            Require(box.enabled && box.size == new Vector3(0.1f, 2, 3) && box.center == Vector3.up,
                "Box dimensions are clamped and the base is at the origin.");
            Require(box.Raycast(new Ray(new Vector3(0, 1, -5), Vector3.forward), out _, 10),
                "The configured box receives hits inside its silhouette.");
            Require(!box.Raycast(new Ray(new Vector3(1, 1, -5), Vector3.forward), out _, 10),
                "The configured box rejects hits outside its silhouette.");

            prop.collisionShape = Prop.CollisionShape.Model;
            var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
            child.transform.SetParent(model.transform);
            child.transform.localPosition = Vector3.right * 2;
            Require(prop.ConfigureDisguiseColliders(model), "Model mode creates mesh hitboxes.");
            Require(!box.enabled && !child.GetComponent<BoxCollider>().enabled, "Model mode disables primitive hitboxes.");
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
                Require(filter.GetComponent<MeshCollider>().sharedMesh == filter.sharedMesh,
                    "Every model part uses its own mesh, including transformed children.");
            Physics.SyncTransforms();
            Require(child.GetComponent<MeshCollider>().Raycast(new Ray(new Vector3(2, 0, -5), Vector3.forward), out _, 10),
                "Model hits respect child transforms.");
            prop.collisionShape = Prop.CollisionShape.Capsule;
            Require(!prop.ConfigureDisguiseColliders(model), "Switching back restores capsule hit detection.");
            foreach (var collider in model.GetComponentsInChildren<Collider>())
                Require(!collider.enabled, "Switching back disables custom hitboxes.");
            Debug.Log("Prop collision checks passed: capsule, box, model and switching.");
        }
        finally
        {
            if (model != null) UnityEngine.Object.DestroyImmediate(model);
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    static void CheckHudAndPortraits()
    {
        Require(MatchHUD.CanSeeName(PlayerRole.Prop, PlayerRole.Prop, true), "Props see fellow props through walls.");
        Require(!MatchHUD.CanSeeName(PlayerRole.Hunter, PlayerRole.Prop, false), "Hunters never see prop name tags.");
        Require(MatchHUD.CanSeeName(PlayerRole.Hunter, PlayerRole.Hunter, true), "Hunters see fellow hunters through walls.");
        Require(MatchHUD.CanSeeName(PlayerRole.Prop, PlayerRole.Hunter, false), "Props can identify visible hunters.");
        Require(!MatchHUD.CanSeeName(PlayerRole.Prop, PlayerRole.Hunter, true), "Walls hide hunter names from props.");
        Require(!MatchHUD.CanSeeName(PlayerRole.Prop, PlayerRole.Spectator, false), "Spectators have no tag.");
        Require(HunterProjectile.StraightPosition(Vector3.one, Vector3.forward * 45, 0.5f) == Vector3.one + Vector3.forward * 22.5f,
            "Straight egg rendering does not add gravity or interpolation delay.");
        Require(!LobbyManager.ValidPortrait(null) && !LobbyManager.ValidPortrait(new byte[20_001]), "Reject missing and oversized thumbnails.");
        var texture = new Texture2D(64, 64, TextureFormat.RGB24, false);
        try
        {
            var png = texture.EncodeToPNG();
            Require(LobbyManager.ValidPortrait(png), "Accept a 64x64 PNG thumbnail.");
            png[19] = 65;
            Require(!LobbyManager.ValidPortrait(png), "Reject remote image dimensions before decoding.");
            png[19] = 64; png[0] = 0;
            Require(!LobbyManager.ValidPortrait(png), "Reject non-PNG payloads.");
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
        var egg = AssetDatabase.LoadAssetAtPath<ThrowableDefinition>("Assets/PropHuntConfig/Throwable.asset");
        Require(egg != null && egg.gravity == 0 && egg.cooldown == 0.25f && egg.speed == 45, "Egg tuning is straight and rapid-fire.");
        var eggMesh = egg.projectilePrefab.GetComponentInChildren<MeshFilter>();
        Require(eggMesh.sharedMesh != null && AssetDatabase.GetAssetPath(eggMesh.sharedMesh) == "Assets/PropHuntConfig/Egg.obj" &&
            eggMesh.GetComponent<MeshRenderer>().sharedMaterial == AssetDatabase.LoadAssetAtPath<Material>("Assets/PropHuntConfig/Egg.mat") &&
            new SerializedObject(egg.projectilePrefab.GetComponent<HunterProjectile>()).FindProperty("visual").objectReferenceValue == eggMesh.transform,
            "The fired prefab links the Egg model, material and projectile visual transform.");
    }

    static void CheckMusicSelection()
    {
        var config = ScriptableObject.CreateInstance<GameAudioConfig>();
        var music = AudioClip.Create("Audio selection check", 32, 1, 8000, false);
        var finale = AudioClip.Create("Final stretch check", 32, 1, 8000, false);
        try
        {
            Require(config.PickWhistleIndex() == -1 && config.WhistleAt(0) == null, "An empty whistle collection stays silent.");
            var randomState = UnityEngine.Random.state;
            try
            {
                config.whistles = new[] { null, music, null, finale };
                for (int i = 0; i < 100; i++)
                {
                    int index = config.PickWhistleIndex();
                    Require(index == 1 || index == 3, "Only assigned whistle clips can be selected.");
                    Require(config.WhistleAt(index) != null, "A selected whistle index resolves to the shared clip.");
                }
                config.whistles = new AudioClip[] { null };
                Require(config.PickWhistleIndex() == -1, "A collection containing only empty slots stays silent.");
                Require(config.WhistleAt(-1) == null && config.WhistleAt(99) == null, "Invalid whistle indices stay silent.");
            }
            finally { UnityEngine.Random.state = randomState; }
            Require(config.SelectMusic(false, false, MatchPhase.Waiting, 0) == null, "Empty audio slots stay silent.");
            config.menuMusic = music; config.gameplayMusic = music;
            Require(config.SelectMusic(false, false, MatchPhase.Waiting, 0) == music, "Menu selects its track.");
            Require(config.SelectMusic(true, true, MatchPhase.Hunting, 20) == music, "Missing final track keeps gameplay music.");
            config.finalStretchMusic = finale;
            Require(config.SelectMusic(true, true, MatchPhase.Hunting, 20) == finale, "Final stretch selects its optional track.");
            Require(config.SelectMusic(true, true, MatchPhase.Hunting, 90, true) == finale &&
                config.SelectMusic(true, true, MatchPhase.Hunting, 90, false) == music,
                "Last-hider tension uses the same track, only when the local listener is eligible.");
            config.victory = music; config.defeat = finale;
            Require(config.SelectMusic(true, true, MatchPhase.Finished, 0) == null, "Result tracks use their own looping channel, not gameplay music.");
            Require(config.victory != null && config.defeat != null, "Victory and defeat own the result audio slots.");
            config.spectatorEnter = finale;
            Require(config.SelectMusic(true, true, MatchPhase.Hunting, 90, spectator: true) == finale,
                "Spectator music replaces gameplay music rather than playing as an effect.");
            Require(config.SelectMusic(true, true, MatchPhase.Finished, 0, spectator: true) == null,
                "Round results always silence spectator music, including a one-versus-one elimination.");
            config.spectatorEnter = null;
            Require(config.SelectMusic(true, true, MatchPhase.Hunting, 90, spectator: true) == music,
                "A missing spectator track falls back to gameplay music.");
            var door = new GameObject("Unspawned door audio check").AddComponent<DoorAudio>();
            try
            {
                var data = new SerializedObject(door);
                data.FindProperty("sound").objectReferenceValue = music;
                data.ApplyModifiedPropertiesWithoutUndo();
                door.Opened(); door.Closed(); door.HandleUsed(); door.Knocked();
                Require(!door.GetComponent<AudioSource>().isPlaying, "Unspawned doors must not emit audio or send gameplay events.");
            }
            finally { UnityEngine.Object.DestroyImmediate(door.gameObject); }
        }
        finally { UnityEngine.Object.DestroyImmediate(config); UnityEngine.Object.DestroyImmediate(music); UnityEngine.Object.DestroyImmediate(finale); }
    }
}
