using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ConfigureApartmentDoors
{
    [MenuItem("PropHunt/Configure apartment door interactions")]
    public static void Run()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/maps/Map_Apartment.unity");
            var setup = UnityEngine.Object.FindFirstObjectByType<MapSetup>();
            if (setup == null) throw new InvalidOperationException("Apartment MapSetup is missing.");
            var scripts = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).Where(b => b != null).ToArray();
            foreach (var script in scripts)
            {
                string field, opening, closing;
                switch (script.GetType().FullName)
                {
                    case "SojaExiles.opencloseDoor": field = "openandclose"; opening = "Opening"; closing = "Closing"; break;
                    case "SojaExiles.opencloseDoor1": field = "openandclose1"; opening = "Opening 1"; closing = "Closing 1"; break;
                    case "SojaExiles.BRGlassDoor": field = "openandclose"; opening = "BRGlassDoorOpen"; closing = "BRGlassDoorClose"; break;
                    case "SojaExiles.ClosetopencloseDoor": field = "Closetopenandclose"; opening = "ClosetOpening"; closing = "ClosetClosing"; break;
                    default: continue;
                }
                var animator = new SerializedObject(script).FindProperty(field).objectReferenceValue as Animator;
                var collider = script.GetComponent<Collider>() ?? script.GetComponentInChildren<Collider>(true);
                if (animator == null || collider == null || animator.runtimeAnimatorController == null)
                    throw new InvalidOperationException("Door requires its authored animator and collider: " + script.name);
                Transform parent = script.transform;
                while (parent != null && (!animator.transform.IsChildOf(parent) || !collider.transform.IsChildOf(parent))) parent = parent.parent;
                if (parent == null) throw new InvalidOperationException("Door animator and collider must share a hierarchy: " + script.name);
                var door = parent.GetComponent<ApartmentDoor>();
                if (door == null) door = Undo.AddComponent<ApartmentDoor>(parent.gameObject);
                Undo.RecordObject(door, "Configure apartment door");
                door.animator = animator; door.interactionCollider = collider;
                door.openingState = opening; door.closingState = closing;
                Undo.RecordObject(animator, "Keep door collision animated on the server");
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
                PrefabUtility.RecordPrefabInstancePropertyModifications(door);
                Undo.RecordObject(script, "Disable local-only demo door input");
                script.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(script);
                Debug.Log($"Apartment door configured: {door.name} ({opening}/{closing})");
            }
            Undo.RecordObject(setup, "Register apartment doors");
            setup.doors = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ApartmentDoor>(true)).ToArray();
            if (setup.doors.Length == 0 || setup.doors.Select(d => d.animator).Distinct().Count() != setup.doors.Length)
                throw new InvalidOperationException("No apartment doors found, or multiple doors share an animator.");
            PrefabUtility.RecordPrefabInstancePropertyModifications(setup);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save apartment doors.");
            Debug.Log($"Apartment door setup saved: {setup.doors.Length} synchronized doors. Use E as hunter or prop.");
        }
        finally { if (!Application.isBatchMode) EditorSceneManager.RestoreSceneManagerSetup(previous); }
    }
}
