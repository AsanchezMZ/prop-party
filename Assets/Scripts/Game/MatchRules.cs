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
}
