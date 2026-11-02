using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// Lives on the persistent NetworkManager; all playback stays local.
[DisallowMultipleComponent]
public class PartyAudio : MonoBehaviour
{
    [SerializeField] GameAudioConfig config;
    public static PartyAudio Instance { get; private set; }
    public GameAudioConfig Config => config;
    public static GameAudioConfig Settings => Instance != null ? Instance.config : null;
    AudioSource music, effects, danger, ambience, results;
    AudioClip wantedMusic;
    PropHuntMatch match;
    PropHuntPlayer player;
    MatchPhase previousPhase = (MatchPhase)255;
    PlayerRole previousRole = PlayerRole.Spectator;
    bool wasNearby, wasSpotted, warned;
    int previousScore;

    void Awake()
    {
        if (Instance != null && Instance != this) { enabled = false; return; }
        Instance = this;
        music = Source("Music", true); effects = Source("UI and round effects", false);
        danger = Source("Local danger", true); ambience = Source("Ambience", true);
        results = Source("Results", false);
        music.volume = 0;
    }

    AudioSource Source(string label, bool loop)
    {
        var child = new GameObject(label); child.transform.SetParent(transform, false);
        var source = child.AddComponent<AudioSource>();
        source.playOnAwake = false; source.spatialBlend = 0; source.loop = loop;
        return source;
    }

    void OnEnable()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded += SceneLoaded;
        FindMatch();
    }

    void SceneLoaded(Scene scene, LoadSceneMode mode) => FindMatch();
    void FindMatch()
    {
        if (results != null) results.Stop();
        match = FindAnyObjectByType<PropHuntMatch>(); player = null;
        previousPhase = (MatchPhase)255; previousRole = PlayerRole.Spectator;
        wasNearby = wasSpotted = warned = false; previousScore = 0;
    }

    public void Play(AudioClip clip)
    {
        if (clip != null && effects != null) effects.PlayOneShot(clip, config != null ? config.effectsVolume : 1);
    }

    void Update()
    {
        if (config == null || Instance != this) return;
        var network = NetworkManager.Singleton;
        bool inMatch = match != null && match.IsSpawned;
        var phase = inMatch ? match.Phase.Value : MatchPhase.Waiting;
        if (!inMatch || phase != MatchPhase.Finished) results.Stop();
        double seconds = inMatch ? match.SecondsLeft : 0;
        var local = network != null && network.IsClient && network.LocalClient != null ? network.LocalClient.PlayerObject : null;
        var currentPlayer = local != null ? local.GetComponent<PropHuntPlayer>() : null;
        if (currentPlayer != player)
        {
            player = currentPlayer; previousScore = player != null ? player.Score.Value : 0;
            previousRole = player != null ? player.Role.Value : PlayerRole.Spectator;
        }
        wantedMusic = config.SelectMusic(inMatch, network != null && network.IsConnectedClient, phase, seconds);
        UpdateMusic();
        SetLoop(ambience, inMatch && phase != MatchPhase.Finished ? config.ambienceLoop : null, config.ambienceVolume);
        if (inMatch && phase != previousPhase)
        {
            if (phase == MatchPhase.Hiding) { warned = false; Play(config.roundStart); }
            else if (phase == MatchPhase.Hunting)
            {
                if (previousPhase != MatchPhase.Hiding) { warned = false; Play(config.roundStart); }
                Play(config.huntStart);
            }
            else if (phase == MatchPhase.Finished)
            {
                if (player != null && player.Role.Value != PlayerRole.Spectator)
                {
                    results.clip = player.Role.Value == match.Winner.Value ? config.victory : config.defeat;
                    results.volume = config.effectsVolume;
                    if (results.clip != null) results.Play();
                }
            }
            previousPhase = phase;
        }
        if (inMatch && phase == MatchPhase.Hunting && seconds <= 10 && !warned)
        { warned = true; Play(config.lastSecondsWarning); }
        bool hidingPlayer = inMatch && phase == MatchPhase.Hunting && player != null && player.Role.Value == PlayerRole.Prop;
        bool nearby = hidingPlayer && player.HunterNearby.Value;
        bool spotted = hidingPlayer && player.InHunterSight.Value;
        if (nearby && !wasNearby) Play(config.nearbyEnter);
        if (!nearby && wasNearby && hidingPlayer) Play(config.nearbyExit);
        if (spotted && !wasSpotted) Play(config.spottedEnter);
        SetLoop(danger, spotted && config.spottedLoop != null ? config.spottedLoop : nearby ? config.nearbyLoop : null, config.dangerVolume);
        wasNearby = nearby; wasSpotted = spotted;
        if (player != null)
        {
            if (player.Role.Value == PlayerRole.Spectator && previousRole != PlayerRole.Spectator) Play(config.spectatorEnter);
            if (player.Score.Value > previousScore) Play(config.scoreTick);
            previousRole = player.Role.Value; previousScore = player.Score.Value;
        }
    }

    void UpdateMusic()
    {
        float target = music.clip == wantedMusic ? config.musicVolume : 0;
        music.volume = Mathf.MoveTowards(music.volume, target, Time.unscaledDeltaTime * 1.5f);
        if (music.clip == wantedMusic)
        {
            if (wantedMusic != null && !music.isPlaying) music.Play();
            return;
        }
        if (music.volume > 0) return;
        music.Stop(); music.clip = wantedMusic;
        if (wantedMusic != null) music.Play();
    }

    static void SetLoop(AudioSource source, AudioClip clip, float volume)
    {
        source.volume = volume;
        if (source.clip == clip) { if (clip != null && !source.isPlaying) source.Play(); return; }
        source.Stop(); source.clip = clip;
        if (clip != null) source.Play();
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
        if (music != null) music.Stop();
        if (danger != null) danger.Stop();
        if (ambience != null) ambience.Stop();
        if (results != null) results.Stop();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }
}
