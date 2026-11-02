using System.Linq;
using UnityEngine;

public static class GamePreferences
{
    const string Prefix = "GameSettings.";
    public static float MusicVolume { get; private set; } = 1;
    public static float EffectsVolume { get; private set; } = 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Load()
    {
        SetAudio(PlayerPrefs.GetFloat(Prefix + "Master", 1), PlayerPrefs.GetFloat(Prefix + "Music", 1),
            PlayerPrefs.GetFloat(Prefix + "Effects", 1), false);
        if (PlayerPrefs.HasKey(Prefix + "Quality"))
            QualitySettings.SetQualityLevel(Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "Quality"), 0, QualitySettings.names.Length - 1));
        if (PlayerPrefs.HasKey(Prefix + "VSync"))
            QualitySettings.vSyncCount = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "VSync"), 0, 1);
        Application.targetFrameRate = ValidFrameRate(PlayerPrefs.GetInt(Prefix + "FPS", Application.targetFrameRate));
        int width = PlayerPrefs.GetInt(Prefix + "Width", Screen.width);
        int height = PlayerPrefs.GetInt(Prefix + "Height", Screen.height);
        if (PlayerPrefs.HasKey(Prefix + "Width"))
        {
            bool supported = Screen.resolutions.Any(r => r.width == width && r.height == height);
            bool windowed = PlayerPrefs.GetInt(Prefix + "Fullscreen", 1) == 0;
            if (supported || (windowed && width > 0 && height > 0 && width <= Screen.currentResolution.width && height <= Screen.currentResolution.height))
                SetResolution(width, height, !windowed);
        }
    }

    public static int ValidFrameRate(int value) => value > 0 ? Mathf.Clamp(value, 30, 240) : -1;

    public static void SetAudio(float master, float music, float effects, bool save = true)
    {
        AudioListener.volume = Mathf.Clamp01(master);
        MusicVolume = Mathf.Clamp01(music);
        EffectsVolume = Mathf.Clamp01(effects);
        if (!save) return;
        PlayerPrefs.SetFloat(Prefix + "Master", AudioListener.volume);
        PlayerPrefs.SetFloat(Prefix + "Music", MusicVolume);
        PlayerPrefs.SetFloat(Prefix + "Effects", EffectsVolume);
    }

    public static void SetVideo(int width, int height, bool fullscreen, int quality, bool vsync, int frameRate)
    {
        quality = Mathf.Clamp(quality, 0, QualitySettings.names.Length - 1);
        QualitySettings.SetQualityLevel(quality);
        QualitySettings.vSyncCount = vsync ? 1 : 0;
        Application.targetFrameRate = ValidFrameRate(frameRate);
        SetResolution(width, height, fullscreen);
        PlayerPrefs.SetInt(Prefix + "Width", width);
        PlayerPrefs.SetInt(Prefix + "Height", height);
        PlayerPrefs.SetInt(Prefix + "Fullscreen", fullscreen ? 1 : 0);
        PlayerPrefs.SetInt(Prefix + "Quality", quality);
        PlayerPrefs.SetInt(Prefix + "VSync", vsync ? 1 : 0);
        PlayerPrefs.SetInt(Prefix + "FPS", Application.targetFrameRate);
    }

    static void SetResolution(int width, int height, bool fullscreen)
    {
        // The editor's Game view is not the player's display.
        if (!Application.isEditor)
            Screen.SetResolution(width, height, fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
    }
}
