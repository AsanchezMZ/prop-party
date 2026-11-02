using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

public static class ApartmentPropChecks
{
    const string Folder = "Assets/Brick Project Studio/Apartment Kit/_Prefabs";
    const string ScenePath = "Assets/Scenes/maps/Map_Apartment.unity";

    [MenuItem("PropHunt/Validate apartment props and map")]
    public static void Run()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var types = AssetDatabase.FindAssets("t:Prefab", new[] { Folder }).Select(AssetDatabase.GUIDToAssetPath)
                .Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p).GetComponent<Prop>()).Where(p => p != null).ToArray();
            Require(types.Length == 56, "Expected 56 apartment prop types.");
            Require(types.All(p => p.id > 0 && p.slotId > 0) && types.Select(p => p.id).Distinct().Count() == types.Length &&
                types.Select(p => p.slotId).Distinct().Count() == types.Length, "Prefab IDs and slots must be positive and unique.");
            foreach (var prop in types)
            {
                Require(prop.visual != null && prop.visual != prop.transform && prop.visual.GetComponentsInChildren<Prop>(true).Length == 0,
                    prop.name + ": visual must include the whole model without any Prop components.");
                CheckSettings(prop);
                var clone = UnityEngine.Object.Instantiate(prop.visual.gameObject);
                try { Require(prop.ConfigureDisguiseColliders(clone), prop.name + ": model colliders could not be generated."); }
                finally { UnityEngine.Object.DestroyImmediate(clone); }
            }
            var sample = UnityEngine.Object.Instantiate(types[0]);
            try
            {
                int appeared = 0;
                for (int seed = 1; seed <= 1000; seed++)
                {
                    sample.ApplySeed(seed); bool active = sample.gameObject.activeSelf;
                    sample.ApplySeed(seed); Require(active == sample.gameObject.activeSelf, "Appearance must be deterministic.");
                    if (active) appeared++;
                }
                Require(appeared > 650 && appeared < 850, "Appearance distribution must approximate 75%.");
            }
            finally { UnityEngine.Object.DestroyImmediate(sample.gameObject); }

            var scene = EditorSceneManager.OpenScene(ScenePath);
            var setup = UnityEngine.Object.FindFirstObjectByType<MapSetup>();
            var match = UnityEngine.Object.FindFirstObjectByType<PropHuntMatch>();
            var spawner = UnityEngine.Object.FindFirstObjectByType<MapPropSpawner>();
            Require(setup != null && match != null && match.Map == setup && spawner != null, "Apartment gameplay setup is incomplete.");
            Require(setup.TrySpawn(PlayerRole.Prop, 0, out _) && setup.TrySpawn(PlayerRole.Hunter, 0, out _), "Both teams need spawn points.");
            var props = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Prop>(true)).ToArray();
            Require(props.Length > 0 && props.All(p => p.slotId > 0) && props.Select(p => p.slotId).Distinct().Count() == props.Length,
                "Scene slot IDs must be positive and unique.");
            Require(new SerializedObject(spawner).FindProperty("props").arraySize == props.Length, "Spawner registration incomplete.");
            foreach (var prop in props)
            {
                CheckSettings(prop);
                Require(setup.definition.catalog.TryGet(prop.id, out var type) && type != null, "Missing scene type: " + prop.name);
            }
            foreach (var type in types) Require(setup.definition.catalog.TryGet(type.id, out var found) && found == type, "Catalog must reference the canonical prefab: " + type.name);
            var document = match.GetComponent<UIDocument>();
            Require(match.GetComponent<MatchHUD>() != null && document.panelSettings != null && document.visualTreeAsset != null, "HUD missing.");
            Require(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).All(c => !c.enabled), "Demo cameras must be disabled.");
            Require(EditorBuildSettings.scenes.Any(s => s.path == ScenePath && s.enabled), "Scene_01 missing from build.");
            EditorSceneManager.OpenScene("Assets/Scenes/ui/MainMenu.unity");
            Require(UnityEngine.Object.FindFirstObjectByType<LobbyManager>().Maps.Any(m => m != null && m.sceneName == ScenePath), "Apartment missing from lobby.");
            Debug.Log($"Apartment checks passed: {types.Length} prefab types, {props.Length} scene instances, model colliders, 75% appearance, unique IDs, catalog, HUD, build and lobby.");
        }
        finally { if (!Application.isBatchMode) EditorSceneManager.RestoreSceneManagerSetup(previous); }
    }

    static void CheckSettings(Prop prop)
    {
        var data = new SerializedObject(prop);
        Require(prop.collisionShape == Prop.CollisionShape.Model && data.FindProperty("spawnProbability").floatValue == 0.75f &&
            data.FindProperty("canDisappear").boolValue, "Incorrect appearance/collision settings: " + prop.name);
    }

    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
