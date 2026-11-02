using UnityEngine;

[CreateAssetMenu(menuName = "PropHunt/Match Rules")]
public class MatchRules : ScriptableObject
{
    [Min(2)] public int minimumPlayers = 2;
    [Min(1)] public int hunters = 1;
    [Min(0)] public float hidingSeconds = 20;
    [Min(10)] public float huntingSeconds = 180;
    [Min(1)] public float whistleSeconds = 25;
    public bool capturedBecomeHunters = true;
    [Min(0.5f)] public float disguiseDistance = 3;
    [Min(1)] public int propHealth = 2;
    [Header("Puntuación (servidor)")]
    [Min(1)] public float proximityRadius = 8;
    [Min(1)] public float visibilityDistance = 20;
    [Range(20, 120)] public float hunterFieldOfView = 75;
    [Min(1)] public int proximityPoints = 10;
    [Min(1)] public int visiblePoints = 20;
    [Min(1)] public int manualWhistlePoints = 100;
    [Min(1)] public int capturePoints = 150;
}
