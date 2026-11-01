using System;
using UnityEditor;
using UnityEngine;

public static class PropHuntChecks
{
    [MenuItem("PropHunt/Run deterministic prop checks")]
    public static void Run()
    {
        Require(FaceCapture.Crop(1920, 1080, false) == new Rect(0.21875f, 0, 0.5625f, 1), "Landscape webcam must crop centrally.");
        Require(FaceCapture.Crop(1080, 1920, false) == new Rect(0, 0.21875f, 1, 0.5625f), "Portrait webcam must crop centrally.");
        Require(FaceCapture.Crop(512, 512, true) == new Rect(0, 1, 1, -1), "Mirrored camera must flip vertically.");
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
}
