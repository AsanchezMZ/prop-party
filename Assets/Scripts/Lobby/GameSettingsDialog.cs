using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

// Bound by LobbyUI: no extra scene component or network state.
public sealed class GameSettingsDialog
{
    readonly VisualElement panel, videoPage, audioPage;
    readonly Button videoTab, audioTab;
    readonly DropdownField resolution, frameRate;
    readonly Toggle fullscreen, vsync;
    readonly SliderInt master, music, effects;
    readonly Label note;
    readonly Action<bool> visibilityChanged;
    readonly List<Vector2Int> resolutions = new();
    readonly int[] frameRates = { -1, 30, 60, 120, 144, 240 };
    VisualElement previousFocus;
    public bool IsOpen { get; private set; }

    public GameSettingsDialog(VisualElement root, Action<bool> visibilityChanged)
    {
        this.visibilityChanged = visibilityChanged;
        panel = root.Q("game-settings-overlay");
        videoPage = root.Q("game-video-page"); audioPage = root.Q("game-audio-page");
        videoTab = root.Q<Button>("game-video-tab"); audioTab = root.Q<Button>("game-audio-tab");
        resolution = root.Q<DropdownField>("game-resolution");
        frameRate = root.Q<DropdownField>("game-fps");
        fullscreen = root.Q<Toggle>("game-fullscreen"); vsync = root.Q<Toggle>("game-vsync");
        master = root.Q<SliderInt>("game-master"); music = root.Q<SliderInt>("game-music"); effects = root.Q<SliderInt>("game-effects");
        note = root.Q<Label>("game-settings-note");
        Bind(root.Q<Button>("game-settings-button"), Open);
        Bind(videoTab, () => SelectTab(true)); Bind(audioTab, () => SelectTab(false));
        Bind(root.Q<Button>("game-settings-back"), Close);
        Bind(root.Q<Button>("game-settings-apply"), Apply);
        foreach (var slider in new[] { master, music, effects })
            slider.RegisterValueChangedCallback(_ => Click());
        fullscreen.RegisterValueChangedCallback(_ => Click());
        vsync.RegisterValueChangedCallback(evt => { Click(); frameRate.SetEnabled(!evt.newValue); });
        foreach (var dropdown in new[] { resolution, frameRate })
        {
            dropdown.RegisterCallback<PointerDownEvent>(evt => { if (evt.button == 0) Click(); }, TrickleDown.TrickleDown);
            dropdown.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Space || evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter ||
                    evt.keyCode == KeyCode.UpArrow || evt.keyCode == KeyCode.DownArrow) Click();
            }, TrickleDown.TrickleDown);
            dropdown.RegisterValueChangedCallback(_ => Click());
        }
        panel.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode != KeyCode.Escape) return;
            Click(); Close(); evt.StopPropagation();
        });
    }

    static void Click() => PartyAudio.Instance?.Play(PartyAudio.Settings?.uiClick);
    static void Bind(Button button, Action action) => button.clicked += () => { Click(); action(); };

    void Open()
    {
        if (IsOpen) return;
        previousFocus = panel.focusController?.focusedElement as VisualElement;
        resolutions.Clear();
        resolutions.AddRange(Screen.resolutions.Select(r => new Vector2Int(r.width, r.height)).Distinct());
        var current = new Vector2Int(Screen.width, Screen.height);
        if (!resolutions.Contains(current)) resolutions.Add(current);
        resolution.choices = resolutions.Select(r => $"{r.x} × {r.y}").ToList();
        resolution.SetValueWithoutNotify(resolution.choices[resolutions.IndexOf(current)]);
        frameRate.choices = frameRates.Select(fps => fps < 0 ? "Sin límite" : $"{fps} FPS").ToList();
        int index = Array.IndexOf(frameRates, Application.targetFrameRate);
        frameRate.SetValueWithoutNotify(frameRate.choices[Mathf.Max(0, index)]);
        fullscreen.SetValueWithoutNotify(Screen.fullScreen);
        vsync.SetValueWithoutNotify(QualitySettings.vSyncCount > 0);
        frameRate.SetEnabled(!vsync.value);
        master.SetValueWithoutNotify(Mathf.RoundToInt(AudioListener.volume * 100));
        music.SetValueWithoutNotify(Mathf.RoundToInt(GamePreferences.MusicVolume * 100));
        effects.SetValueWithoutNotify(Mathf.RoundToInt(GamePreferences.EffectsVolume * 100));
        note.text = "Ajustes locales. VSync usa la frecuencia de la pantalla y tiene prioridad sobre el límite de FPS.";
        IsOpen = true;
        visibilityChanged(true);
        panel.style.display = DisplayStyle.Flex;
        SelectTab(true); videoTab.Focus();
    }

    void SelectTab(bool video)
    {
        videoPage.style.display = video ? DisplayStyle.Flex : DisplayStyle.None;
        audioPage.style.display = video ? DisplayStyle.None : DisplayStyle.Flex;
        videoTab.EnableInClassList("is-selected", video); audioTab.EnableInClassList("is-selected", !video);
        videoTab.tooltip = video ? "Vídeo: pestaña activa" : "Abrir ajustes de vídeo";
        audioTab.tooltip = video ? "Abrir ajustes de audio" : "Audio: pestaña activa";
    }

    void Apply()
    {
        if (resolution.index < 0 || frameRate.index < 0) return;
        var size = resolutions[resolution.index];
        GamePreferences.SetVideo(size.x, size.y, fullscreen.value, QualitySettings.GetQualityLevel(), vsync.value, frameRates[frameRate.index]);
        GamePreferences.SetAudio(master.value / 100f, music.value / 100f, effects.value / 100f);
        PlayerPrefs.Save();
        note.text = "Ajustes guardados en este equipo.";
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        panel.style.display = DisplayStyle.None;
        visibilityChanged(false);
        previousFocus?.Focus();
    }
}
