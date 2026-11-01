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
        // Asignar ID a los que no tienen (máximo existente + 1, nunca reutiliza)
        int next = props.Count > 0 ? Mathf.Max(props.Max(p => p.id), 0) + 1 : 1;
        // Snapshot first: changing a base prefab also changes inherited IDs in its variants.
        var missing = props.Where(p => p.id == 0 ||
            (PrefabUtility.GetPrefabAssetType(p) == PrefabAssetType.Variant &&
             !new SerializedObject(p).FindProperty("id").prefabOverride)).ToArray();
        foreach (var p in missing)
        {
            var so = new SerializedObject(p);
            so.FindProperty("id").intValue = next++;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(p);
        }

        // Avisar de duplicados
        foreach (var g in props.GroupBy(p => p.id).Where(g => g.Count() > 1))
            Debug.LogError($"ID {g.Key} duplicado en: {string.Join(", ", g.Select(p => p.name))}");

    }
}
#endif
