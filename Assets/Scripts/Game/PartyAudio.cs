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
    AudioSource music, effects, danger, ambience, results, announcements;
    AudioClip pendingAnnouncement;
    float nextAnnouncement;
    bool announcing;
    AudioClip wantedMusic;
    PropHuntMatch match;
    MatchHUD hud;
    PropHuntPlayer player;
    MatchPhase previousPhase = (MatchPhase)255;
    PlayerRole resultRole = PlayerRole.Spectator;
    bool wasNearby, wasSpotted, warned;
    int previousScore;

    void Awake()
    {
        if (Instance != null && Instance != this) { enabled = false; return; }
        Instance = this;
        music = Source("Music", true); effects = Source("UI and round effects", false);
        danger = Source("Local danger", true); ambience = Source("Ambience", true);
        results = Source("Results", true);
        announcements = Source("Round announcements", false);
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
        if (announcements != null) announcements.Stop();
        pendingAnnouncement = null; announcing = false; nextAnnouncement = 0;
        match = FindAnyObjectByType<PropHuntMatch>(); player = null;
        hud = FindAnyObjectByType<MatchHUD>();
        previousPhase = (MatchPhase)255; resultRole = PlayerRole.Spectator;
        wasNearby = wasSpotted = warned = false; previousScore = 0;
    }

    public void Play(AudioClip clip)
    {
        if (clip != null && effects != null) effects.PlayOneShot(clip, config != null ? config.EffectsVolume : GamePreferences.EffectsVolume);
    }

    void Update()
    {
        if (config == null || Instance != this) return;
        var network = NetworkManager.Singleton;
        bool inMatch = match != null && match.IsSpawned;
        var phase = inMatch ? match.Phase.Value : MatchPhase.Waiting;
        if (!inMatch || phase != MatchPhase.Finished) results.Stop();
        results.volume = config.MusicVolume;
        double seconds = inMatch ? match.SecondsLeft : 0;
        var local = network != null && network.IsClient && network.LocalClient != null ? network.LocalClient.PlayerObject : null;
        var currentPlayer = local != null ? local.GetComponent<PropHuntPlayer>() : null;
        if (currentPlayer != player)
        {
            player = currentPlayer; previousScore = player != null ? player.Score.Value : 0;
            resultRole = player != null ? player.Role.Value : PlayerRole.Spectator;
        }
        if (player != null && player.Role.Value != PlayerRole.Spectator) resultRole = player.Role.Value;
        if (inMatch && phase != previousPhase)
        {
            if (phase == MatchPhase.Hiding)
            {
                warned = false;
                announcements.Stop(); announcing = false;
                pendingAnnouncement = config.roundStart;
                nextAnnouncement = Time.unscaledTime + 2.5f;
            }
            else if (phase == MatchPhase.Hunting)
            {
                warned = false;
                pendingAnnouncement = config.huntStart;
                nextAnnouncement = Mathf.Max(nextAnnouncement, Time.unscaledTime + 2);
            }
            else if (phase == MatchPhase.Finished)
            {
                announcements.Stop(); pendingAnnouncement = null; announcing = false;
                music.Stop(); music.volume = 0;
                if (player != null && resultRole != PlayerRole.Spectator)
                {
                    results.clip = resultRole == match.Winner.Value ? config.victory : config.defeat;
                    results.volume = config.MusicVolume;
                    if (results.clip != null) results.Play();
                }
            }
            previousPhase = phase;
        }
        if (inMatch && phase == MatchPhase.Hunting && seconds <= 10 && !warned)
        {
            warned = true;
            // A delayed hunt announcement takes priority over a late countdown cue.
            if (pendingAnnouncement == null && !announcements.isPlaying && Time.unscaledTime >= nextAnnouncement)
                pendingAnnouncement = config.lastSecondsWarning;
        }
        if (!PartyLoadingScreen.Ready)
        {
            announcements.Pause();
        }
        else
        {
            if (announcing) announcements.UnPause();
            if (announcing && !announcements.isPlaying)
            { announcing = false; nextAnnouncement = Time.unscaledTime + 5; }
            if (pendingAnnouncement != null && !announcements.isPlaying && Time.unscaledTime >= nextAnnouncement)
            {
                announcements.clip = pendingAnnouncement; pendingAnnouncement = null;
                announcements.volume = config.EffectsVolume;
                announcements.Play(); announcing = true;
            }
        }
        announcements.volume = config.EffectsVolume;
        wantedMusic = config.SelectMusic(inMatch, network != null && network.IsConnectedClient, phase, seconds,
            inMatch && player != null && player.Role.Value == PlayerRole.Prop && match.LastHiderRemaining.Value,
            player != null && player.Role.Value == PlayerRole.Spectator);
        UpdateMusic();
        SetLoop(ambience, PartyLoadingScreen.Ready && inMatch && phase != MatchPhase.Finished ? config.ambienceLoop : null, config.AmbienceVolume);
        bool hidingPlayer = inMatch && phase == MatchPhase.Hunting && player != null && player.Role.Value == PlayerRole.Prop;
        bool nearby = hidingPlayer && player.HunterNearby.Value;
        bool spotted = hidingPlayer && player.InHunterSight.Value;
        if (nearby && !wasNearby) Play(config.nearbyEnter);
        if (!nearby && wasNearby && hidingPlayer) Play(config.nearbyExit);
        if (spotted && !wasSpotted) Play(config.spottedEnter);
        bool heartbeat = hidingPlayer && hud != null && hud.isActiveAndEnabled && hud.HeartbeatActive;
        SetLoop(danger, PartyLoadingScreen.Ready && heartbeat ? config.nearbyLoop : null, config.DangerVolume);
        wasNearby = nearby; wasSpotted = spotted;
        if (player != null)
        {
            if (player.Score.Value > previousScore) Play(config.scoreTick);
            previousScore = player.Score.Value;
        }
    }

    void UpdateMusic()
    {
        if (!PartyLoadingScreen.Ready || announcing || pendingAnnouncement != null)
        {
            music.Stop();
            music.volume = 0;
            return;
        }
        float level = wantedMusic != null && (wantedMusic == config.hidingMusic || wantedMusic == config.finalStretchMusic) ? 0.7f : 1;
        float target = music.clip == wantedMusic ? config.MusicVolume * level : 0;
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
        if (announcements != null) announcements.Stop();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }
}
