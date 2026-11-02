using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PropIdRepair
{
    static readonly string BackupFolder = Path.Combine(Path.GetTempPath(), "opencode", "prop-id-repair-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));

    static void Backup(string path)
    {
        string destination = BackupFolder + "/" + path;
        if (File.Exists(destination)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        File.Copy(path, destination);
    }

    static Prop[] SceneProps(UnityEngine.SceneManagement.Scene scene) => scene.GetRootGameObjects()
        .SelectMany(r => r.GetComponentsInChildren<Prop>(true)).ToArray();

    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    static string[] AssetsOfType(string type) => AssetDatabase.FindAssets("t:" + type, new[] { "Assets" })
        .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal).ToArray();

    static Prop[] Types() => AssetsOfType("Prefab").Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p)?.GetComponent<Prop>())
        .Where(p => p != null).ToArray();

    static Prop TypeOf(Prop prop)
    {
        if (EditorUtility.IsPersistent(prop) && prop.transform.parent == null) return prop;
        string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prop.gameObject);
        if (!string.IsNullOrEmpty(path))
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var source = PrefabUtility.GetCorrespondingObjectFromSource(prop);
            if (root != null && root.GetComponent<Prop>() != null &&
                (prop.transform == PrefabUtility.GetNearestPrefabInstanceRoot(prop.gameObject).transform ||
                 source != null && source.transform.parent == null)) return root.GetComponent<Prop>();
        }
        for (var current = prop; current != null;)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(current);
            if (source == null || source == current) return EditorUtility.IsPersistent(current) && current.transform.parent == null ? current : null;
            if (source.transform.parent == null) return source;
            current = source;
        }
        return null;
    }

    [MenuItem("PropHunt/Repair project prop IDs and map catalogs")]
    public static void Repair()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            CheckIdAssignment();
            var types = Types();
            // Preserve membership by asset path, never by the old (possibly duplicated) ID.
            var members = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (string path in AssetsOfType("PropCatalog"))
            {
                var entries = new SerializedObject(AssetDatabase.LoadAssetAtPath<PropCatalog>(path)).FindProperty("props");
                var paths = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < entries.arraySize; i++)
                {
                    var prop = entries.GetArrayElementAtIndex(i).objectReferenceValue as Prop;
                    Check(prop != null, $"Missing prop in {path}, entry {i}; repair the reference before rebuilding.");
                    var type = TypeOf(prop);
                    Check(type != null, $"Catalog entry has no root Prop prefab: {path}, {prop.name}");
                    paths.Add(AssetDatabase.GetAssetPath(type));
                }
                members.Add(path, paths);
            }
            var scenes = new List<string>();
            foreach (string path in AssetsOfType("Scene"))
            {
                var scene = EditorSceneManager.OpenScene(path);
                var props = SceneProps(scene);
                foreach (var prop in props) Check(TypeOf(prop) != null, $"Scene-only Prop needs a prefab before assigning type IDs: {path}, {prop.name}");
                var setups = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MapSetup>(true)).ToArray();
                if (props.Length > 0 || setups.Length > 0) { scenes.Add(path); Backup(path); }
                foreach (var setup in setups)
                {
                    Check(setup.definition != null && setup.definition.catalog != null, "Missing map definition/catalog: " + path);
                    string catalogPath = AssetDatabase.GetAssetPath(setup.definition.catalog);
                    foreach (var prop in props) members[catalogPath].Add(AssetDatabase.GetAssetPath(TypeOf(prop)));
                }
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            types = Types();
            var oldIds = types.ToDictionary(AssetDatabase.GetAssetPath, p => p.id, StringComparer.Ordinal);
            foreach (var type in types) Backup(AssetDatabase.GetAssetPath(type));
            PropCatalogBuilder.AssignMissingIds(types.ToList());
            AssetDatabase.SaveAssets();
            foreach (var type in types.Where(p => oldIds[AssetDatabase.GetAssetPath(p)] != p.id))
                Debug.Log($"PROP ID REPAIRED {oldIds[AssetDatabase.GetAssetPath(type)]} -> {type.id}: {AssetDatabase.GetAssetPath(type)}");

            int prefabOverrides = 0, sceneOverrides = 0;
            foreach (string path in AssetsOfType("Prefab"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!asset.GetComponentsInChildren<Prop>(true).Any(p => p.transform.parent != null && TypeOf(p) != null && p.id != TypeOf(p).id)) continue;
                Backup(path);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var prop in root.GetComponentsInChildren<Prop>(true).Where(p => p.transform.parent != null))
                        if (SyncInstanceId(prop)) prefabOverrides++;
                    PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved); Check(saved, "Cannot save " + path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (string path in scenes)
            {
                var scene = EditorSceneManager.OpenScene(path);
                bool changed = false;
                foreach (var prop in SceneProps(scene))
                    if (SyncInstanceId(prop)) { sceneOverrides++; changed = true; }
                foreach (var setup in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MapSetup>(true)))
                {
                    var definition = setup.definition;
                    if (definition.sceneName == path || definition.sceneName == scene.name) continue;
                    Check(!File.Exists(definition.sceneName), $"Map definition also points to an existing scene: {definition.name}; choose the intended scene manually.");
                    Backup(AssetDatabase.GetAssetPath(definition));
                    definition.sceneName = path; EditorUtility.SetDirty(definition);
                    AssetDatabase.SaveAssetIfDirty(definition);
                    Debug.Log($"PROP MAP PATH REPAIRED {definition.name}: {path}");
                }
                if (changed) { EditorSceneManager.MarkSceneDirty(scene); Check(EditorSceneManager.SaveScene(scene), "Cannot save " + path); }
            }
            foreach (var pair in members)
            {
                Backup(pair.Key);
                var catalog = AssetDatabase.LoadAssetAtPath<PropCatalog>(pair.Key);
                var allowed = pair.Value.Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p).GetComponent<Prop>()).OrderBy(p => p.id).ToArray();
                var data = new SerializedObject(catalog); var entries = data.FindProperty("props"); entries.arraySize = allowed.Length;
                for (int i = 0; i < allowed.Length; i++) entries.GetArrayElementAtIndex(i).objectReferenceValue = allowed[i];
                data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(catalog);
                Debug.Log($"PROP CATALOG REBUILT {pair.Key}: {allowed.Length} types");
            }
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log($"Prop repair completed: {types.Length} types; {prefabOverrides} prefab instance overrides; {sceneOverrides} scene overrides. Backup: {BackupFolder}");
        }
        finally { if (!Application.isBatchMode) EditorSceneManager.RestoreSceneManagerSetup(previous); }
    }

    static bool SyncInstanceId(Prop prop)
    {
        var type = TypeOf(prop);
        Check(type != null, "No canonical prefab for " + prop.name);
        if (prop.id == type.id) return false;
        var data = new SerializedObject(prop); var id = data.FindProperty("id");
        if (id.prefabOverride) PrefabUtility.RevertPropertyOverride(id, InteractionMode.AutomatedAction);
        Check(prop.id == type.id, "ID does not match the canonical prefab after reverting: " + prop.name);
        PrefabUtility.RecordPrefabInstancePropertyModifications(prop);
        EditorUtility.SetDirty(prop);
        return true;
    }

    [MenuItem("PropHunt/Validate project prop IDs and map catalogs")]
    public static void Validate()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var previous = EditorSceneManager.GetSceneManagerSetup();
        try { ValidateProject(); }
        finally { if (!Application.isBatchMode) EditorSceneManager.RestoreSceneManagerSetup(previous); }
    }

    static void ValidateProject()
    {
        var types = Types();
        Check(types.All(p => p.id > 0) && types.Select(p => p.id).Distinct().Count() == types.Length, "Prop type IDs must be positive and globally unique.");
        int minimumId = types.Min(p => p.id), maximumId = types.Max(p => p.id);
        var before = types.Select(p => p.id).ToArray();
        PropCatalogBuilder.AssignMissingIds(types.ToList());
        Check(before.SequenceEqual(types.Select(p => p.id)), "ID assignment must be idempotent.");
        foreach (string path in AssetsOfType("Prefab"))
            foreach (var prop in AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<Prop>(true))
                Check(TypeOf(prop) != null && prop.id == TypeOf(prop).id, "Prefab instance ID is inconsistent: " + path + "/" + prop.name);
        int catalogs = 0, instances = 0;
        foreach (string path in AssetsOfType("PropCatalog"))
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PropCatalog>(path);
            var entries = new SerializedObject(catalog).FindProperty("props");
            var ids = new HashSet<int>();
            for (int i = 0; i < entries.arraySize; i++)
            {
                var prop = entries.GetArrayElementAtIndex(i).objectReferenceValue as Prop;
                Check(prop != null && prop.transform.parent == null && prop.id > 0 && ids.Add(prop.id), "Invalid/duplicate catalog entry: " + path);
                Check(catalog.TryGet(prop.id, out var resolved) && resolved == prop, "Catalog lookup is stale: " + path);
            }
            catalogs++;
        }
        foreach (string path in AssetsOfType("Scene"))
        {
            var scene = EditorSceneManager.OpenScene(path); var props = SceneProps(scene); instances += props.Length;
            foreach (var prop in props) Check(TypeOf(prop) != null && prop.id == TypeOf(prop).id, "Scene instance has an inconsistent type ID: " + path + "/" + prop.name);
            foreach (var setup in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MapSetup>(true)))
            {
                Check(setup.definition != null && setup.definition.catalog != null &&
                    (setup.definition.sceneName == path || setup.definition.sceneName == scene.name), "Map definition does not match its scene: " + path);
                foreach (var prop in props)
                    Check(setup.definition.catalog.TryGet(prop.id, out var resolved) && resolved == TypeOf(prop), "Scene prop missing from map catalog: " + path + "/" + prop.name);
            }
        }
        Debug.Log($"Prop ID validation passed: {types.Length} unique types (IDs {minimumId}–{maximumId}), {instances} scene instances, {catalogs} map catalogs; all canonical references and scene paths resolve.");
    }

    static void CheckIdAssignment()
    {
        var props = new[] { "A", "B", "C", "D", "E" }.Select(n => new GameObject("ID check " + n).AddComponent<Prop>()).ToList();
        try
        {
            props[0].id = 5; props[1].id = 5; props[2].id = 0; props[3].id = -1; props[4].id = int.MaxValue;
            PropCatalogBuilder.AssignMissingIds(props);
            Check(props[0].id == 5 && props[4].id == int.MaxValue && props.All(p => p.id > 0) && props.Select(p => p.id).Distinct().Count() == props.Count,
                "ID assignment must preserve valid IDs and repair duplicates, zero and negatives without overflow.");
            var assigned = props.Select(p => p.id).ToArray(); props.Reverse(); PropCatalogBuilder.AssignMissingIds(props); props.Reverse();
            Check(assigned.SequenceEqual(props.Select(p => p.id)), "Repeating assignment must not renumber valid types.");
        }
        finally { foreach (var prop in props) UnityEngine.Object.DestroyImmediate(prop.gameObject); }
    }
}
