using UnityEngine;

[CreateAssetMenu(menuName = "PropHunt/Audio")]
public class GameAudioConfig : ScriptableObject
{
    [Header("Música · bucles 2D")]
    public AudioClip menuMusic;
    public AudioClip lobbyMusic, hidingMusic, gameplayMusic, finalStretchMusic, resultsMusic;
    [Header("Ronda · efectos 2D")]
    public AudioClip roundStart;
    public AudioClip huntStart, roundEnd, victory, defeat, lastSecondsWarning;
    public AudioClip countdown3, countdown2, countdown1, lastHider;
    [Header("Interfaz · efectos 2D")]
    public AudioClip uiClick;
    public AudioClip uiBack, uiConfirm, uiError, connected, disconnected, ready, unready;
    public AudioClip cameraOn, photoCaptured;
    public AudioClip mapSelectWhoosh, mapSelectConfirm, otherPlayerJoined, otherPlayerLeft;
    [Header("Puertas · eventos 3D del controlador de interacción")]
    public AudioClip doorOpen;
    public AudioClip doorClose, doorHandle, doorKnock;
    [Header("Peligro · solo lo oye el escondedor")]
    public AudioClip nearbyEnter;
    public AudioClip nearbyExit, nearbyLoop, spottedEnter, spottedLoop;
    [Header("Jugador · efectos espaciales salvo confirmaciones")]
    public AudioClip disguise;
    public AudioClip rotate, freeze, unfreeze, jump, hurt, captured;
    public AudioClip[] footsteps;
    public AudioClip automaticWhistle, manualWhistle;
    [Header("Arma")]
    public AudioClip throwSound;
    public AudioClip impactSound, hitConfirmation, captureConfirmation;
    public AudioClip shotMiss;
    [Header("Otros")]
    public AudioClip scoreTick;
    public AudioClip spectatorEnter, ambienceLoop;
    [Header("Volúmenes")]
    [Range(0, 1)] public float musicVolume = 0.35f;
    [Range(0, 1)] public float effectsVolume = 0.8f;
    [Range(0, 1)] public float dangerVolume = 0.35f;
    [Range(0, 1)] public float ambienceVolume = 0.2f;

    public AudioClip SelectMusic(bool inMatch, bool connectedToRoom, MatchPhase phase, double secondsLeft)
    {
        if (!inMatch) return connectedToRoom ? lobbyMusic : menuMusic;
        if (phase == MatchPhase.Hiding) return hidingMusic;
        if (phase == MatchPhase.Hunting)
            return secondsLeft <= 30 && finalStretchMusic != null ? finalStretchMusic : gameplayMusic;
        return phase == MatchPhase.Finished ? resultsMusic : lobbyMusic;
    }
}
