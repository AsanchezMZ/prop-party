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
        CheckGameSettings();
        Require(FaceCapture.Crop(1920, 1080, false) == new Rect(0.21875f, 0, 0.5625f, 1), "Landscape webcam must crop centrally.");
        Require(FaceCapture.Crop(1080, 1920, false) == new Rect(0, 0.21875f, 1, 0.5625f), "Portrait webcam must crop centrally.");
        Require(FaceCapture.Crop(512, 512, true) == new Rect(0, 1, 1, -1), "Mirrored camera must flip vertically.");
        var settings = new MatchSettings { Hunters = -4, HidingSeconds = 999, HuntingSeconds = 0, WhistleSeconds = -1, Health = 90 }.Validated();
        Require(settings.Hunters == 1 && settings.HidingSeconds == 120 && settings.HuntingSeconds == 30 && settings.WhistleSeconds == 5 && settings.Health == 10,
            "Session settings must respect server bounds.");
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

    [MenuItem("PropHunt/Run presentation checks")]
    public static void CheckPresentationAssets()
    {
        var loading = Resources.Load<UnityEngine.UIElements.VisualTreeAsset>("LoadingScreen");
        Require(loading != null, "Loading screen must be included in player builds.");
        var root = loading.CloneTree();
        Require(UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.ProgressBar>(root, "loading-progress") != null &&
            UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Button>(root, "loading-retry") != null &&
            UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Button>(root, "loading-leave") != null,
            "Loading screen needs progress, retry and return controls.");
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

    public static void CheckGameSettings()
    {
        var menu = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>("Assets/UI/MainMenu.uxml");
        Require(menu != null, "Game settings live in the existing menu asset.");
        var root = menu.CloneTree();
        foreach (var name in new[] { "game-settings-button", "game-video-tab", "game-audio-tab", "game-settings-back", "game-settings-apply" })
            Require(UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Button>(root, name) != null, "Missing settings button: " + name);
        foreach (var name in new[] { "game-resolution", "game-quality", "game-fps" })
            Require(UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.DropdownField>(root, name) != null, "Missing video setting: " + name);
        foreach (var name in new[] { "game-master", "game-music", "game-effects" })
        {
            var slider = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.SliderInt>(root, name);
            Require(slider != null && slider.lowValue == 0 && slider.highValue == 100, "Audio sliders use percentage bounds.");
        }
        Require(UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.TextField>(root, "player-name") != null &&
            UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Button>(root, "face-button") != null,
            "Name and webcam keep their existing entry points.");
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
        Require(egg.projectilePrefab.GetComponentInChildren<MeshFilter>().sharedMesh != null, "Egg prefab references the authored mesh asset.");
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
            config.victory = music; config.defeat = finale;
            Require(config.SelectMusic(true, true, MatchPhase.Finished, 0) == null, "Results clips are not treated as looping music.");
            Require(config.victory != null && config.defeat != null, "Victory and defeat own the result audio slots.");
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
