using UnityEngine;

[CreateAssetMenu(menuName = "PropHunt/Audio")]
public class GameAudioConfig : ScriptableObject
{
    [Header("Música · bucles 2D")]
    public AudioClip menuMusic;
    public AudioClip lobbyMusic, hidingMusic, gameplayMusic, finalStretchMusic;
    [Header("Ronda · efectos 2D")]
    public AudioClip roundStart;
    public AudioClip huntStart, victory, defeat, lastSecondsWarning;
    public AudioClip lastHider;
    [Header("Interfaz · efectos 2D")]
    public AudioClip uiClick;
    public AudioClip uiBack, uiConfirm, uiError;
    [Tooltip("Played when you or another player joins the lobby.")]
    public AudioClip connected;
    [Tooltip("Played when you or another player leaves the lobby; assign a different clip from Connected.")]
    public AudioClip disconnected;
    [Tooltip("Played when the local player becomes ready.")]
    public AudioClip ready;
    [Tooltip("Played when the local player becomes unready; assign a different clip from Ready.")]
    public AudioClip unready;
    public AudioClip photoCaptured;
    [Header("Puertas · sonido 3D compartido")]
    public AudioClip doorKnock;
    [Header("Peligro · solo lo oye el escondedor")]
    public AudioClip nearbyEnter;
    public AudioClip nearbyExit, nearbyLoop, spottedEnter, spottedLoop;
    [Header("Jugador · efectos espaciales salvo confirmaciones")]
    public AudioClip disguise;
    public AudioClip rotate, freeze, jump, hurt, captured;
    public AudioClip[] footsteps;
    [Tooltip("Shared pool for automatic and manual whistles. Use the context menu to collect whistle_{name} clips.")]
    public AudioClip[] whistles = System.Array.Empty<AudioClip>();
    [Header("Arma")]
    public AudioClip throwSound;
    public AudioClip impactSound, hitConfirmation;
    public AudioClip shotMiss;
    [Header("Otros")]
    public AudioClip scoreTick;
    public AudioClip spectatorEnter, ambienceLoop;
    [Header("Volúmenes")]
    [Range(0, 1)] public float musicVolume = 0.35f;
    [Range(0, 1)] public float effectsVolume = 0.8f;
    [Range(0, 1)] public float dangerVolume = 0.35f;
    [Range(0, 1)] public float ambienceVolume = 0.2f;

    public float MusicVolume => musicVolume * GamePreferences.MusicVolume;
    public float EffectsVolume => effectsVolume * GamePreferences.EffectsVolume;
    public float DangerVolume => dangerVolume * GamePreferences.EffectsVolume;
    public float AmbienceVolume => ambienceVolume * GamePreferences.EffectsVolume;

    public AudioClip SelectMusic(bool inMatch, bool connectedToRoom, MatchPhase phase, double secondsLeft, bool lastHider = false)
    {
        if (!inMatch) return connectedToRoom ? lobbyMusic : menuMusic;
        if (phase == MatchPhase.Hiding) return hidingMusic;
        if (phase == MatchPhase.Hunting)
            return (secondsLeft <= 30 || lastHider) && finalStretchMusic != null ? finalStretchMusic : gameplayMusic;
        return phase == MatchPhase.Finished ? null : lobbyMusic;
    }

    public int PickWhistleIndex()
    {
        int selected = -1;
        float total = 0;
        if (whistles == null) return selected;
        for (int i = 0; i < whistles.Length; i++)
        {
            if (whistles[i] == null) continue;
            float weight = 1 / Mathf.Max(0.25f, whistles[i].length);
            total += weight;
            if (Random.value * total < weight) selected = i;
        }
        return selected;
    }

    public AudioClip WhistleAt(int index) => whistles != null && index >= 0 && index < whistles.Length ? whistles[index] : null;

#if UNITY_EDITOR
    [ContextMenu("Collect whistle_* audio clips")]
    void CollectWhistles()
    {
        var clips = new System.Collections.Generic.List<AudioClip>();
        foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:AudioClip"))
        {
            var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            if (clip != null && clip.name.StartsWith("whistle_", System.StringComparison.Ordinal) && clip.name.Length > 8)
                clips.Add(clip);
        }
        clips.Sort((a, b) =>
        {
            int order = string.CompareOrdinal(a.name, b.name);
            return order != 0 ? order : string.CompareOrdinal(UnityEditor.AssetDatabase.GetAssetPath(a), UnityEditor.AssetDatabase.GetAssetPath(b));
        });
        UnityEditor.Undo.RecordObject(this, "Collect whistle clips");
        whistles = clips.ToArray();
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[{name}] {whistles.Length} whistle_* clips collected.", this);
    }
#endif
}
