using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Unity.Netcode;

public static class PropHuntSetup
{
    const string Folder = "Assets/PropHuntConfig";

    [MenuItem("PropHunt/Configure enabled build scenes")]
    public static void ConfigureBuildScenes()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "PropHuntConfig");
        var previous = EditorSceneManager.GetSceneManagerSetup();
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).ToArray();
        string lobbyPath = null;
        GameObject playerPrefab = null;
        var rules = Asset<MatchRules>("Rules");
        var weapon = Asset<ThrowableDefinition>("Throwable");
        var propPrefabs = AssetDatabase.FindAssets("t:Prefab").Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(p => p != null).Select(p => p.GetComponent<Prop>()).Where(p => p != null).ToList();
        PropCatalogBuilder.AssignMissingIds(propPrefabs);
        AssetDatabase.SaveAssets();
        var theme = AssetDatabase.FindAssets("PartyMenu t:StyleSheet").Select(g => AssetDatabase.LoadAssetAtPath<StyleSheet>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault();
        try
        {
            foreach (var entry in scenes)
            {
                EditorSceneManager.OpenScene(entry.path);
                var bootstrap = UnityEngine.Object.FindFirstObjectByType<GameBootstrap>();
                if (bootstrap == null || UnityEngine.Object.FindFirstObjectByType<LobbyManager>() == null) continue;
                lobbyPath = entry.path;
                var serialized = new SerializedObject(bootstrap);
                var prefab = serialized.FindProperty("playerPrefab").objectReferenceValue as NetworkObject;
                if (prefab != null) playerPrefab = prefab.gameObject;
                break;
            }
            if (lobbyPath == null || playerPrefab == null) throw new InvalidOperationException("An enabled lobby scene needs GameBootstrap, LobbyManager and a player prefab.");
            rules = Asset<MatchRules>("Rules");
            weapon = Asset<ThrowableDefinition>("Throwable");
            string lobbyName = System.IO.Path.GetFileNameWithoutExtension(lobbyPath);
            if (weapon.projectilePrefab == null)
            {
                var projectile = new GameObject("Throwable");
                var component = projectile.AddComponent<HunterProjectile>();
                Set(component, "definition", weapon);
                var visual = new GameObject("Visual - assign your mesh"); visual.transform.SetParent(projectile.transform, false);
                Set(component, "visual", visual.transform);
                var prefab = PrefabUtility.SaveAsPrefabAsset(projectile, Folder + "/Throwable.prefab");
                UnityEngine.Object.DestroyImmediate(projectile);
                weapon.projectilePrefab = prefab.GetComponent<HunterProjectile>();
                EditorUtility.SetDirty(weapon);
                AssetDatabase.SaveAssets();
            }
            var player = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(playerPrefab));
            try
            {
                var gameplay = player.GetComponent<PropHuntPlayer>();
                if (gameplay == null) gameplay = player.AddComponent<PropHuntPlayer>();
                var visual = player.GetComponentInChildren<Animator>(true);
                if (visual != null) Set(gameplay, "normalVisual", visual.gameObject);
                var disguise = player.transform.Find("Disguise");
                if (disguise == null) { disguise = new GameObject("Disguise").transform; disguise.SetParent(player.transform, false); }
                Set(gameplay, "disguiseRoot", disguise);
                Set(gameplay, "weapon", weapon);
                var audio = player.GetComponent<AudioSource>();
                if (audio == null) audio = player.AddComponent<AudioSource>();
                audio.playOnAwake = false; audio.spatialBlend = 1; audio.maxDistance = 35;
                Set(gameplay, "audioSource", audio);
                PrefabUtility.SaveAsPrefabAsset(player, AssetDatabase.GetAssetPath(playerPrefab));
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }

            var definitions = new System.Collections.Generic.List<string>();
            foreach (var entry in scenes.Where(s => s.path != lobbyPath))
            {
                var scene = EditorSceneManager.OpenScene(entry.path);
                rules = Asset<MatchRules>("Rules");
                theme = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI/PartyMenu.uss");
                var setup = UnityEngine.Object.FindFirstObjectByType<MapSetup>();
                if (setup == null) setup = new GameObject("Prop Hunt Map").AddComponent<MapSetup>();
                setup.Collect();
                if (setup.spawns.Length == 0) { Debug.LogWarning($"Skipping {scene.name}: add SpawnPoints first."); continue; }
                var definition = setup.definition != null ? setup.definition : Asset<MapDefinition>(scene.name);
                if (string.IsNullOrEmpty(definition.displayName)) definition.displayName = scene.name.Replace("_", " ");
                definition.sceneName = scene.path;
                if (definition.catalog == null) definition.catalog = Asset<PropCatalog>(scene.name + " Props");
                setup.definition = definition;
                var spawner = UnityEngine.Object.FindFirstObjectByType<MapPropSpawner>();
                if (spawner == null)
                {
                    var go = new GameObject("Map Props"); go.AddComponent<NetworkObject>(); spawner = go.AddComponent<MapPropSpawner>();
                }
                spawner.CollectProps();
                var props = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Prop>(true)).ToArray();
                var catalog = new SerializedObject(definition.catalog);
                var entries = catalog.FindProperty("props");
                var prefabs = props.Select(p => PrefabUtility.GetCorrespondingObjectFromSource(p)).Where(p => p != null).Distinct().ToArray();
                entries.arraySize = prefabs.Length;
                for (int i = 0; i < prefabs.Length; i++) entries.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
                catalog.ApplyModifiedPropertiesWithoutUndo();
                var match = setup.GetComponent<PropHuntMatch>();
                if (match == null) { setup.gameObject.AddComponent<NetworkObject>(); match = setup.gameObject.AddComponent<PropHuntMatch>(); }
                Set(match, "rules", rules); Set(match, "map", setup); SetString(match, "lobbyScene", lobbyName);
                var hud = setup.GetComponent<MatchHUD>();
                if (hud == null) hud = setup.gameObject.AddComponent<MatchHUD>();
                ConfigurePanel(hud.GetComponent<UIDocument>());
                hud.GetComponent<UIDocument>().visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/MatchHUD.uxml");
                Set(hud, "match", match); SetString(hud, "lobbyScene", lobbyName);
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                definitions.Add(AssetDatabase.GetAssetPath(definition));
            }
            var lobbyScene = EditorSceneManager.OpenScene(lobbyPath);
            rules = Asset<MatchRules>("Rules");
            weapon = Asset<ThrowableDefinition>("Throwable");
            theme = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI/PartyMenu.uss");
            var manager = UnityEngine.Object.FindFirstObjectByType<LobbyManager>();
            Set(manager, "rules", rules);
            var managerData = new SerializedObject(manager);
            var maps = managerData.FindProperty("mapDefinitions"); maps.arraySize = definitions.Count;
            for (int i = 0; i < definitions.Count; i++) maps.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<MapDefinition>(definitions[i]);
            managerData.ApplyModifiedPropertiesWithoutUndo();
            var ui = UnityEngine.Object.FindFirstObjectByType<LobbyUI>();
            if (ui != null)
            {
                if (ui.GetComponent<UIDocument>() == null) ui.gameObject.AddComponent<UIDocument>();
                ConfigurePanel(ui.GetComponent<UIDocument>());
                ui.GetComponent<UIDocument>().visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/MainMenu.uxml");
                var face = ui.GetComponent<FaceCapture>();
                if (face != null) Set(ui, "faceCapture", face);
                EditorUtility.SetDirty(ui.GetComponent<UIDocument>());
                var audio = ui.GetComponent<AudioSource>();
                if (audio == null) audio = ui.gameObject.AddComponent<AudioSource>();
                audio.playOnAwake = false; Set(ui, "audioSource", audio);
            }
            var network = UnityEngine.Object.FindFirstObjectByType<NetworkManager>();
            network.NetworkConfig.ConnectionApproval = true;
            // Append to existing lists, preserving any manually registered prefabs.
            var list = network.NetworkConfig.Prefabs.NetworkPrefabsLists.FirstOrDefault();
            if (list == null) throw new InvalidOperationException("Assign a NetworkPrefabsList to NetworkManager.");
            if (!list.PrefabList.Any(p => p.Prefab == weapon.projectilePrefab.gameObject))
            { list.Add(new NetworkPrefab { Prefab = weapon.projectilePrefab.gameObject }); EditorUtility.SetDirty(list); }
            SetString(UnityEngine.Object.FindFirstObjectByType<GameBootstrap>(), "lobbyScene", lobbyName);
            EditorSceneManager.MarkSceneDirty(lobbyScene); EditorSceneManager.SaveScene(lobbyScene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Prop Hunt configured: {definitions.Count} maps. Assign meshes, audio and map previews in {Folder}.");
            PropHuntChecks.Run();
        }
        finally { if (!Application.isBatchMode) EditorSceneManager.RestoreSceneManagerSetup(previous); }
    }

    static T Asset<T>(string name) where T : ScriptableObject
    {
        string path = Folder + "/" + name + ".asset";
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) { asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); }
        return asset;
    }

    static void ConfigurePanel(UIDocument document)
    {
        var panel = Asset<PanelSettings>("PartyPanel");
        panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/UI/PartyTheme.tss");
        panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        panel.referenceResolution = new Vector2Int(1280, 720);
        document.panelSettings = panel;
        EditorUtility.SetDirty(panel);
        EditorUtility.SetDirty(document);
    }

    static void Set(UnityEngine.Object target, string property, UnityEngine.Object value)
    {
        var data = new SerializedObject(target); data.FindProperty(property).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo();
    }
    static void SetString(UnityEngine.Object target, string property, string value)
    {
        var data = new SerializedObject(target); data.FindProperty(property).stringValue = value; data.ApplyModifiedPropertiesWithoutUndo();
    }
}
