#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class PropCatalogBuilder
{
    // Opcional: limita la búsqueda a una carpeta para no recorrer todo el proyecto
    const string SearchFolder = "Assets";

    [MenuItem("PropHunt/Rebuild Prop Catalog")]
    static void Rebuild()
    {
        var catalog = Selection.activeObject as PropCatalog;

        if (catalog == null)
        {
            Debug.LogError("Selecciona el PropCatalog que quieres reconstruir.");
            return;
        }

        var props = AssetDatabase.FindAssets("t:Prefab", new[] { SearchFolder })
            .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(go => go != null)
            .Select(go => go.GetComponent<Prop>())
            .Where(p => p != null)
            .ToList();

        AssignMissingIds(props);
        props = props.OrderBy(p => p.id).ToList();

        // Rellenar el catálogo seleccionado; no modifica los catálogos de otros mapas.
        var catSo = new SerializedObject(catalog);
        var arr = catSo.FindProperty("props");
        arr.arraySize = props.Count;
        for (int i = 0; i < props.Count; i++)
            arr.GetArrayElementAtIndex(i).objectReferenceValue = props[i];
        catSo.ApplyModifiedProperties();

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log($"Catálogo actualizado: {props.Count} tipos de prop");
    }

    public static void AssignMissingIds(System.Collections.Generic.List<Prop> props)
    {
        var ordered = props.Distinct().OrderBy(p => PrefabUtility.GetPrefabAssetType(p) == PrefabAssetType.Variant)
            .ThenBy(AssetDatabase.GetAssetPath, System.StringComparer.Ordinal).ThenBy(p => p.name, System.StringComparer.Ordinal).ToArray();
        var reserved = new System.Collections.Generic.HashSet<int>(ordered.Where(p => p.id > 0).Select(p => p.id));
        var seen = new System.Collections.Generic.HashSet<int>();
        // Snapshot first: changing a base prefab also changes inherited IDs in its variants.
        var missing = ordered.Where(p => p.id <= 0 || !seen.Add(p.id) ||
            (PrefabUtility.GetPrefabAssetType(p) == PrefabAssetType.Variant &&
             !new SerializedObject(p).FindProperty("id").prefabOverride)).ToArray();
        int next = 1;
        foreach (var p in missing)
        {
            while (reserved.Contains(next)) next = checked(next + 1);
            var so = new SerializedObject(p);
            so.FindProperty("id").intValue = next;
            so.ApplyModifiedProperties();
            reserved.Add(next);
            EditorUtility.SetDirty(p);
        }
    }
}
#endif
