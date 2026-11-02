using System;
using UnityEditor;
using UnityEngine;

public static class PropHuntChecks
{
    [MenuItem("PropHunt/Run deterministic prop checks")]
    public static void Run()
    {
        CheckMusicSelection();
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

    static void CheckMusicSelection()
    {
        Require(PartyAudio.CountdownDelay(100, 90, 3) == 7, "Countdown 3 schedules against the server deadline.");
        Require(PartyAudio.CountdownDelay(100, 98, 3) < 0, "Late clients must skip a past countdown beat.");
        Require(PartyAudio.CountdownDelay(100, 98, 1) == 1, "Late clients still schedule future countdown beats.");
        var config = ScriptableObject.CreateInstance<GameAudioConfig>();
        var music = AudioClip.Create("Audio selection check", 32, 1, 8000, false);
        var finale = AudioClip.Create("Final stretch check", 32, 1, 8000, false);
        try
        {
            Require(config.SelectMusic(false, false, MatchPhase.Waiting, 0) == null, "Empty audio slots stay silent.");
            config.menuMusic = music; config.gameplayMusic = music;
            Require(config.SelectMusic(false, false, MatchPhase.Waiting, 0) == music, "Menu selects its track.");
            Require(config.SelectMusic(true, true, MatchPhase.Hunting, 20) == music, "Missing final track keeps gameplay music.");
            config.finalStretchMusic = finale;
            Require(config.SelectMusic(true, true, MatchPhase.Hunting, 20) == finale, "Final stretch selects its optional track.");
            Require(config.SelectMusic(true, true, MatchPhase.Finished, 0) == null, "Empty results track stays silent.");
            var door = new GameObject("Unspawned door audio check").AddComponent<DoorAudio>();
            try
            {
                var data = new SerializedObject(door);
                foreach (var field in new[] { "openSound", "closeSound", "handleSound", "knockSound" })
                    data.FindProperty(field).objectReferenceValue = music;
                data.ApplyModifiedPropertiesWithoutUndo();
                door.Opened(); door.Closed(); door.HandleUsed(); door.Knocked();
                Require(!door.GetComponent<AudioSource>().isPlaying, "Unspawned doors must not emit audio or send gameplay events.");
            }
            finally { UnityEngine.Object.DestroyImmediate(door.gameObject); }
        }
        finally { UnityEngine.Object.DestroyImmediate(config); UnityEngine.Object.DestroyImmediate(music); UnityEngine.Object.DestroyImmediate(finale); }
    }
}
