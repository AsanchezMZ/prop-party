using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// One-shot asset migration, invoked explicitly with -executeMethod; never runs on import.
public static class ConfigureMapProps
{
    const string House = "Assets/Prefabs/Props/HauntedHouse";
    const string Town = "Assets/Prefabs/Props/Town";
    static readonly Dictionary<string, string> Names = new Dictionary<string, string>
    {
        { "baseball_bat", "Bate de béisbol" }, { "bath", "Bañera" }, { "bed", "Cama de matrimonio" },
        { "BigVase", "Jarrón grande" }, { "big_carpet", "Alfombra grande" }, { "Bookshelf", "Estantería" },
        { "chair", "Silla" }, { "closet", "Armario" }, { "couch", "Sillón" }, { "crowbar", "Palanca" },
        { "desktop", "Escritorio" }, { "door", "Puerta" }, { "double_door", "Puerta doble" },
        { "double_door_a", "Hoja de puerta doble" }, { "double_door_b", "Hoja de puerta doble" },
        { "drawer", "Cajonera" }, { "drawers_a", "Cómoda" }, { "entry_platform", "Plataforma de entrada" },
        { "entr_stairs", "Escalones de entrada" }, { "fridge", "Frigorífico" }, { "h_paint", "Cuadro apaisado" },
        { "key", "Llave" }, { "kitchen", "Mueble de cocina" }, { "long_bool_pile", "Pila de libros" },
        { "matress", "Colchón de matrimonio" }, { "medium_carpet", "Alfombra" }, { "mini_table", "Mesita auxiliar" },
        { "paper_pile", "Montón de papeles" }, { "phone", "Teléfono" }, { "pillow", "Almohada" },
        { "safe_box", "Caja fuerte" }, { "sink", "Lavabo" }, { "SmallTable", "Mesa pequeña" },
        { "small_bed", "Cama individual" }, { "small_matress", "Colchón individual" }, { "sofa", "Sofá" },
        { "stove", "Cocina con horno" }, { "Toilet", "Inodoro" }, { "Tv", "Televisor" },
        { "TvTable", "Mueble de televisión" }, { "Vase", "Jarrón" }, { "vault", "Puerta de cámara acorazada" },
        { "v_paint", "Cuadro vertical" }, { "washing_machine", "Lavadora" },
        { "Ladder", "Escalera de mano" }, { "LampA", "Farol" }, { "LampB", "Farol de pared" },
        { "WoodenBarrel", "Barril de madera" }, { "WoodenPlanks", "Tablones de madera" }, { "WoodenTray", "Bandeja de madera" }
    };

    public static void Run()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Run the conversion outside Play Mode.");
        string[] paths = new[] { House, Town }.SelectMany(folder => AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            .Select(AssetDatabase.GUIDToAssetPath).ToArray();
        Require(paths.Length == 50 && paths.All(p => Names.ContainsKey(Path.GetFileNameWithoutExtension(p))), "Unexpected prefab inventory; review the name list.");
        var targetPaths = new HashSet<string>(paths);
        string backup = Path.Combine(Path.GetTempPath(), "opencode", "map-props-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        foreach (string path in paths.Concat(new[] { "Assets/PropHuntConfig/Map_HauntedHouse Props.asset", "Assets/PropHuntConfig/Map_Town Props.asset" }))
        {
            string destination = Path.Combine(backup, path);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(path, destination);
        }
        Debug.Log("MAP PROPS BACKUP: " + backup);

        var types = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
            .Select(p => new { Path = p, Prop = AssetDatabase.LoadAssetAtPath<GameObject>(p).GetComponent<Prop>() })
            .Where(p => p.Prop != null).ToArray();
        var reserved = new HashSet<int>(types.Where(p => p.Prop.id > 0).Select(p => p.Prop.id));
        var claimed = new HashSet<int>(types.Where(p => !targetPaths.Contains(p.Path)).Select(p => p.Prop.id));
        int next = 1;
        // Process bases before their variants so inherited visuals and component IDs stay valid.
        paths = paths.OrderBy(p => AssetDatabase.GetDependencies(p).Count(targetPaths.Contains)).ThenBy(p => p, StringComparer.Ordinal).ToArray();
        foreach (string path in paths)
        {
            var loaded = PrefabUtility.LoadPrefabContents(path);
            GameObject root = loaded;
            try
            {
                var prop = root.GetComponent<Prop>();
                float probability = prop != null ? new SerializedObject(prop).FindProperty("spawnProbability").floatValue : 1;
                Vector3 randomRotation = prop != null ? new SerializedObject(prop).FindProperty("randomRotationThreshold").vector3Value : Vector3.zero;
                Require(probability == 1, path + ": existing probability is not 1; do not overwrite it.");
                if (prop == null)
                {
                    root = new GameObject(loaded.name);
                    SceneManager.MoveGameObjectToScene(root, loaded.scene);
                    var visual = new GameObject("Visual").transform;
                    visual.SetParent(root.transform, false);
                    loaded.name = "Model";
                    loaded.transform.SetParent(visual, false);
                    prop = root.AddComponent<Prop>();
                    prop.visual = visual;
                }
                Require(prop.visual != null && prop.visual != root.transform && prop.visual.IsChildOf(root.transform), path + ": missing presentation-only visual.");
                Require(prop.visual.GetComponentsInChildren<Prop>(true).Length == 0, path + ": nested Prop in visual.");
                if (prop.id <= 0 || claimed.Contains(prop.id))
                {
                    while (reserved.Contains(next)) next++;
                    prop.id = next; reserved.Add(next);
                }
                claimed.Add(prop.id);
                prop.propName = Names[Path.GetFileNameWithoutExtension(path)];
                prop.collisionShape = Prop.CollisionShape.Model;
                // Reuse the shared mesh-local bounds recovery, not renderer bounds of an unloaded asset.
                prop.disguiseRadius = prop.disguiseHeight = float.NaN;
                prop.disguiseBoxSize = Vector3.one * float.NaN;
                prop.ValidateDisguiseDimensions();
                var meshes = prop.visual.GetComponentsInChildren<MeshFilter>(true).Where(m => m.sharedMesh != null).ToArray();
                Require(meshes.Length > 0, path + ": model has no mesh.");
                foreach (var mesh in meshes)
                    if (mesh.GetComponent<Collider>() == null) mesh.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh.sharedMesh;
                var data = new SerializedObject(prop);
                Require(data.FindProperty("spawnProbability").floatValue == probability &&
                    data.FindProperty("randomRotationThreshold").vector3Value == randomRotation, path + ": randomization changed.");
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                Require(saved, "Could not save " + path);
                Debug.Log($"MAP PROP CONFIGURED: {path} | {prop.id} | {prop.propName} | {prop.disguiseRadius} x {prop.disguiseHeight}");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        UpdateCatalog(House, "Assets/PropHuntConfig/Map_HauntedHouse Props.asset");
        UpdateCatalog(Town, "Assets/PropHuntConfig/Map_Town Props.asset");
        Validate();
        Debug.Log("MAP PROPS CONVERSION PASSED: 44 HauntedHouse, 6 Town. Backup: " + backup);
    }

    static void UpdateCatalog(string folder, string path)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<PropCatalog>(path);
        Require(catalog != null, "Missing catalog: " + path);
        var existing = new SerializedObject(catalog).FindProperty("props");
        var props = new List<Prop>();
        for (int i = 0; i < existing.arraySize; i++)
        {
            var prop = existing.GetArrayElementAtIndex(i).objectReferenceValue as Prop;
            Require(prop != null, path + ": broken existing reference; refusing to drop it.");
            props.Add(prop);
        }
        props.AddRange(AssetDatabase.FindAssets("t:Prefab", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath)
            .Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p).GetComponent<Prop>()));
        var allowed = props.Distinct().OrderBy(p => p.id).ToArray();
        var data = new SerializedObject(catalog);
        var entries = data.FindProperty("props"); entries.arraySize = allowed.Length;
        for (int i = 0; i < allowed.Length; i++) entries.GetArrayElementAtIndex(i).objectReferenceValue = allowed[i];
        data.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(catalog);
    }

    public static void Validate()
    {
        var ids = new HashSet<int>();
        foreach (string folder in new[] { House, Town })
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PropCatalog>(folder == House ?
                "Assets/PropHuntConfig/Map_HauntedHouse Props.asset" : "Assets/PropHuntConfig/Map_Town Props.asset");
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var prop = root.GetComponent<Prop>();
                Require(prop != null && prop.id > 0 && ids.Add(prop.id), path + ": missing Prop or duplicate ID.");
                Require(prop.propName == Names[Path.GetFileNameWithoutExtension(path)] && prop.collisionShape == Prop.CollisionShape.Model,
                    path + ": wrong name/collision mode.");
                Require(prop.visual != null && prop.visual != prop.transform && prop.visual.GetComponentsInChildren<Prop>(true).Length == 0,
                    path + ": invalid visual hierarchy.");
                Require(float.IsFinite(prop.disguiseRadius) && float.IsFinite(prop.disguiseHeight) && prop.disguiseRadius >= 0.1f &&
                    prop.disguiseHeight >= prop.disguiseRadius * 2, path + ": invalid controller dimensions.");
                Require(new SerializedObject(prop).FindProperty("spawnProbability").floatValue == 1, path + ": probability must stay 1.");
                Require(prop.visual.GetComponentsInChildren<Collider>(true).Any(c => c.enabled), path + ": missing map interaction collider.");
                Require(catalog.TryGet(prop.id, out var found) && found == prop, path + ": missing canonical catalog entry.");
            }
        }
        Require(ids.Count == 50, "Expected all 50 converted props.");
        Debug.Log("MAP PROPS VALIDATION PASSED: names, IDs, visuals, finite dimensions, colliders, probability and catalogs.");
    }

    public static void PreserveSceneRootReferences()
    {
        // Wrapping a model variant changes its root IDs; retain the IDs used by placed instances.
        string scenePath = "Assets/Scenes/maps/Map_HauntedHouse.unity";
        string sceneText = File.ReadAllText(scenePath);
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { House, Town }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (var pair in new[] { new { Object = (UnityEngine.Object)root, Property = "m_Name" },
                new { Object = (UnityEngine.Object)root.transform, Property = "m_LocalPosition.x" } })
            {
                var oldIds = Regex.Matches(sceneText, @"target: \{fileID: (-?\d+), guid: " + guid +
                    @", type: 3\}\r?\n\s+propertyPath: " + Regex.Escape(pair.Property) + @"\r?\n")
                    .Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToArray();
                if (oldIds.Length == 0) continue;
                Require(oldIds.Length == 1, path + ": ambiguous scene root identity.");
                Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(pair.Object, out string _, out long currentId), path + ": missing root identity.");
                string previous = oldIds[0];
                if (previous == currentId.ToString()) continue;
                string text = File.ReadAllText(path).Replace("\r\n", "\n");
                Require(!Regex.IsMatch(text, @"(?m)^--- !u!\d+ &" + previous + @"$"), path + ": root ID collision.");
                string header = "&" + currentId + "\n";
                Require(text.Contains(header), path + ": refusing to rewrite an inherited root identity.");
                text = text.Replace(header, "&" + previous + "\n").Replace("{fileID: " + currentId + "}", "{fileID: " + previous + "}");
                File.WriteAllText(path, text, new System.Text.UTF8Encoding(false));
                Debug.Log($"MAP PROP ROOT ID PRESERVED: {path} | {pair.Property} | {currentId} -> {previous}");
            }
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Validate();
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
        int instances = 0;
        foreach (var prop in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Prop>(true)))
        {
            string source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prop.gameObject);
            if (!source.StartsWith(House + "/", StringComparison.Ordinal)) continue;
            Require(PrefabUtility.GetCorrespondingObjectFromSource(prop.transform) != null, source + ": lost scene root reference.");
            instances++;
        }
        Require(instances == 87, "Expected all 87 placed HauntedHouse prop instances.");
        Debug.Log("MAP PROP SCENE REFERENCES PASSED: 87 placed props, root IDs preserved; scene was not saved.");
    }

    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
