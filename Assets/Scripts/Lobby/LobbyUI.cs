using System.Collections.Generic;
using System.Net;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class LobbyUI : MonoBehaviour
{
    [SerializeField] LobbyManager lobby;
    [SerializeField] ushort port = 7777;
    [SerializeField] Texture2D background;
    Texture2D randomMapPreview;
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip clickSound;
    [SerializeField] AudioClip menuMusic;
    [SerializeField] FaceCapture faceCapture;
    VisualElement home, room, roster, preview, joinPanel;
    Label status, mapDescription, count, previewTitle;
    TextField playerName, address;
    DropdownField maps;
    Button host, join, start, ready, connect;
    Button faceButton;
    VisualElement content;
    VisualElement settingsPanel;
    Button settingsButton, settingsApply;
    SliderInt settingHunters, settingHide, settingHunt, settingWhistle, settingHealth, settingHunterMisses;
    Toggle settingConvert, settingIdentifyHiders;
    MatchSettings shownSettings;
    Label settingsNote;
    bool settingsOpen;
    GameSettingsDialog gameSettings;
    Button gameSettingsButton;
    IVisualElementScheduledItem toastTimer;
    NetworkManager network;
    bool connecting;
    bool leaving;
    bool disconnectSoundPlayed;
    float connectionStarted;
    VisualElement[] mascots;
    List<VisualElement> backgroundShapes;
    float mapChangedAt = -10;
    int shownMap = -1;

    void Start()
    {
        network = NetworkManager.Singleton;
        var document = GetComponent<UIDocument>();
        if (document.panelSettings == null || document.visualTreeAsset == null)
        {
            Debug.LogError("Assign MainMenu.uxml and the party PanelSettings to the menu UIDocument.", this);
            enabled = false;
            return;
        }
        var root = document.rootVisualElement;
        content = root.Q("menu-root");
        backgroundShapes = root.Query(className: "ambient-shape").ToList();
        if (background != null) content.style.backgroundImage = new StyleBackground(background);
        home = root.Q("home"); room = root.Q("room"); roster = root.Q("roster");
        preview = root.Q("map-preview"); joinPanel = root.Q("join-overlay");
        randomMapPreview = Resources.Load<Texture2D>("RandomMapPreview");
        playerName = root.Q<TextField>("player-name");
        playerName.value = PlayerPrefs.GetString("PlayerName", "Player");
        address = root.Q<TextField>("address");
        status = root.Q<Label>("status"); count = root.Q<Label>("player-count");
        previewTitle = root.Q<Label>("map-title"); mapDescription = root.Q<Label>("map-description");
        maps = root.Q<DropdownField>("maps");
        maps.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button == 0 && maps.enabledInHierarchy) PartyAudio.Instance?.Play(PartyAudio.Settings?.uiClick);
        }, TrickleDown.TrickleDown);
        maps.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Space || evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter ||
                evt.keyCode == KeyCode.UpArrow || evt.keyCode == KeyCode.DownArrow)
                PartyAudio.Instance?.Play(PartyAudio.Settings?.uiClick);
        }, TrickleDown.TrickleDown);
        mascots = new[] { root.Q(className: "suspect-box"), root.Q(className: "suspect-mug"), root.Q(className: "suspect-egg") };
        maps.RegisterValueChangedCallback(_ =>
        {
            if (!network.IsServer || !lobby.IsSpawned || maps.index < 0 || maps.index > lobby.Maps.Length) return;
            lobby.SelectMap(maps.index == lobby.Maps.Length ? LobbyManager.RandomMap : maps.index);
        });
        host = Bind(root, "host", () => Connect(true));
        join = Bind(root, "join", () => ShowJoin(true));
        connect = Bind(root, "connect", () => { ShowJoin(false); Connect(false); });
        ready = Bind(root, "ready", () =>
        {
            foreach (var player in lobby.Players)
                if (player.ClientId == network.LocalClientId)
                { PartyAudio.Instance?.Play(player.Ready ? PartyAudio.Settings?.unready : PartyAudio.Settings?.ready); break; }
            lobby.ToggleReadyServerRpc();
        });
        start = Bind(root, "start", () => lobby.StartGame());
        Bind(root, "leave", Leave);
        Bind(root, "join-back", () => { PartyAudio.Instance?.Play(PartyAudio.Settings?.uiBack); ShowJoin(false); });
        settingsPanel = root.Q("settings-overlay");
        settingsNote = root.Q<Label>("settings-note");
        settingHunters = root.Q<SliderInt>("setting-hunters"); settingHide = root.Q<SliderInt>("setting-hide");
        settingHunt = root.Q<SliderInt>("setting-hunt"); settingWhistle = root.Q<SliderInt>("setting-whistle");
        settingHealth = root.Q<SliderInt>("setting-health"); settingConvert = root.Q<Toggle>("setting-convert");
        settingHunterMisses = root.Q<SliderInt>("setting-hunter-misses");
        settingIdentifyHiders = root.Q<Toggle>("setting-identify-hiders");
        settingsButton = Bind(root, "settings", () => ShowSettings(true));
        Bind(root, "settings-back", () => { PartyAudio.Instance?.Play(PartyAudio.Settings?.uiBack); ShowSettings(false); });
        settingsApply = Bind(root, "settings-apply", () =>
        {
            var settings = new MatchSettings { Hunters = settingHunters.value, HidingSeconds = settingHide.value,
                HuntingSeconds = settingHunt.value, WhistleSeconds = settingWhistle.value,
                Health = settingHealth.value, ConvertCaptured = settingConvert.value,
                HunterMissLimit = settingHunterMisses.value, IdentifyHiders = settingIdentifyHiders.value };
            if (lobby.ApplySettings(settings))
            { PartyAudio.Instance?.Play(PartyAudio.Settings?.uiConfirm); ShowSettings(false); Notify("Reglas guardadas. ¡Preparaos otra vez!"); }
        });
        faceButton = Bind(root, "face-button", () =>
        {
            gameSettingsButton.SetEnabled(false);
            home.SetEnabled(false); room.SetEnabled(false); playerName.SetEnabled(false); faceButton.SetEnabled(false);
            faceCapture.Open();
        });
        if (faceCapture != null)
        {
            faceCapture.Bind(root);
            faceCapture.Closed += FaceClosed;
        }
        else faceButton.SetEnabled(false);
        gameSettingsButton = root.Q<Button>("game-settings-button");
        gameSettings = new GameSettingsDialog(root, visible =>
        {
            home.SetEnabled(!visible); room.SetEnabled(!visible);
            Refresh();
        });
        root.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode != KeyCode.Escape) return;
            if (gameSettings.IsOpen) { PartyAudio.Instance?.Play(PartyAudio.Settings?.uiClick); gameSettings.Close(); }
            else if (settingsOpen) ShowSettings(false);
            else if (faceCapture != null && faceCapture.IsOpen) faceCapture.Close();
            else if (joinPanel.style.display == DisplayStyle.Flex) ShowJoin(false);
            else return;
            evt.StopPropagation();
        });
        lobby.Changed += Refresh;
        network.OnClientConnectedCallback += Connected;
        network.OnClientDisconnectCallback += Disconnected;
        network.OnTransportFailure += TransportFailed;
        if (PartyAudio.Instance == null && audioSource != null && menuMusic != null)
        { audioSource.clip = menuMusic; audioSource.loop = true; }
        Refresh();
    }

    static VisualElement Element(VisualElement parent, string classes)
    {
        var element = new VisualElement();
        foreach (var style in classes.Split(' ')) element.AddToClassList(style);
        parent.Add(element); return element;
    }

    static Label Text(VisualElement parent, string text, string classes)
    {
        var label = new Label(text) { enableRichText = false };
        foreach (var style in classes.Split(' ')) label.AddToClassList(style);
        parent.Add(label); return label;
    }

    void FaceClosed()
    {
        if (lobby.IsSpawned && network.IsConnectedClient) lobby.SendLocalPortrait();
        home.SetEnabled(true); room.SetEnabled(true);
        Refresh();
        faceButton.Focus();
    }

    void Notify(string message)
    {
        toastTimer?.Pause();
        status.text = message; status.style.display = DisplayStyle.Flex;
        toastTimer = status.schedule.Execute(() => status.style.display = DisplayStyle.None).StartingIn(4500);
    }

    void ShowSettings(bool visible)
    {
        gameSettingsButton.SetEnabled(!visible);
        settingsOpen = visible;
        settingsPanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        room.SetEnabled(!visible); faceButton.SetEnabled(!visible && faceCapture != null);
        if (visible) { PopulateSettings(); settingsApply.Focus(); } else settingsButton.Focus();
    }

    void PopulateSettings()
    {
        shownSettings = lobby.Settings.Value;
        settingHunters.SetValueWithoutNotify(shownSettings.Hunters); settingHide.SetValueWithoutNotify(shownSettings.HidingSeconds);
        settingHunt.SetValueWithoutNotify(shownSettings.HuntingSeconds); settingWhistle.SetValueWithoutNotify(shownSettings.WhistleSeconds);
        settingHealth.SetValueWithoutNotify(shownSettings.Health); settingConvert.SetValueWithoutNotify(shownSettings.ConvertCaptured);
        settingHunterMisses.SetValueWithoutNotify(shownSettings.HunterMissLimit);
        settingIdentifyHiders.SetValueWithoutNotify(shownSettings.IdentifyHiders);
        settingHunters.SetEnabled(network.IsServer); settingHide.SetEnabled(network.IsServer); settingHunt.SetEnabled(network.IsServer);
        settingWhistle.SetEnabled(network.IsServer); settingHealth.SetEnabled(network.IsServer); settingConvert.SetEnabled(network.IsServer);
        settingHunterMisses.SetEnabled(network.IsServer); settingIdentifyHiders.SetEnabled(network.IsServer);
        settingsApply.SetEnabled(network.IsServer);
        if (lobby.Rules != null)
            settingsNote.text = $"Cerca: +{lobby.Rules.proximityPoints} cada 3 s. A la vista: +{lobby.Rules.visiblePoints} por segundo.\nSilbar: +{lobby.Rules.manualWhistlePoints}, con 5 s de recarga. Los bonos de riesgo se suman.";
    }

    void ShowJoin(bool visible)
    {
        gameSettingsButton.SetEnabled(!visible);
        joinPanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        home.SetEnabled(!visible); playerName.SetEnabled(!visible);
        faceButton.SetEnabled(!visible && faceCapture != null);
        if (visible) address.Focus(); else join.Focus();
    }

    Button Bind(VisualElement root, string name, System.Action action)
    {
        var button = root.Q<Button>(name);
        button.clicked += () =>
        {
            if (name != "ready")
            {
                if (audioSource != null && clickSound != null) audioSource.PlayOneShot(clickSound);
                else PartyAudio.Instance?.Play(PartyAudio.Settings?.uiClick);
            }
            action();
        };
        return button;
    }

    void Connect(bool asHost)
    {
        if (network.IsListening || network.ShutdownInProgress || connecting || leaving) return;
        disconnectSoundPlayed = false;
        string ip = address.value.Trim();
        if (!asHost && !IPAddress.TryParse(ip, out _)) { Notify("Introduce una dirección IP válida."); return; }
        string name = playerName.value.Trim();
        if (string.IsNullOrEmpty(name)) name = "Player";
        // FixedString32Bytes has 29 UTF-8 payload bytes, not 29 characters.
        while (System.Text.Encoding.UTF8.GetByteCount(name) > 29) name = name.Substring(0, name.Length - 1);
        LocalProfile.PlayerName = name;
        PlayerPrefs.SetString("PlayerName", name);
        connecting = true; connectionStarted = Time.unscaledTime;
        host.SetEnabled(false); join.SetEnabled(false);
        connect.SetEnabled(false);
        Notify(asHost ? "🎉 Creando sala…" : "🔌 Conectando…");
        var transport = network.GetComponent<UnityTransport>();
        transport.SetConnectionData(asHost ? "127.0.0.1" : ip, port, asHost ? "0.0.0.0" : null);
        try
        {
            if (!(asHost ? network.StartHost() : network.StartClient())) Fail("No se pudo iniciar la conexión.");
        }
        catch (System.Exception exception) { Debug.LogException(exception); Fail("No se pudo iniciar el transporte."); }
    }

    void Update()
    {
        if (PartyAudio.Instance == null && audioSource != null && menuMusic != null && audioSource.clip == menuMusic)
        {
            audioSource.volume = 0.18f * GamePreferences.MusicVolume;
            if (!PartyLoadingScreen.Ready) audioSource.Stop();
            else if (!audioSource.isPlaying) audioSource.Play();
        }
        if (leaving && !network.IsListening && !network.ShutdownInProgress)
        { leaving = false; Refresh(false); }
        if (connecting && Time.unscaledTime - connectionStarted > 15) Fail("Sin respuesta. Comprueba la IP y el puerto.");
        if (mascots == null) return;
        for (int i = 0; i < backgroundShapes.Count; i++)
        {
            float phase = Time.unscaledTime * 0.22f + i * 1.7f;
            backgroundShapes[i].style.translate = new Translate(Mathf.Sin(phase) * 32, Mathf.Cos(phase * 0.8f) * 24);
            backgroundShapes[i].style.rotate = new Rotate(new Angle(Time.unscaledTime * (i % 2 == 0 ? 3 : -3)));
        }
        for (int i = 0; i < mascots.Length; i++)
            if (mascots[i] != null) mascots[i].style.translate = new Translate(0, Mathf.Sin(Time.unscaledTime * 1.6f + i * 2) * 7);
        float pop = 1 + Mathf.Sin(Mathf.Clamp01((Time.unscaledTime - mapChangedAt) / 0.4f) * Mathf.PI) * 0.025f;
        preview.style.scale = new Scale(new Vector3(pop, pop, 1));
    }

    void Connected(ulong id)
    {
        if (id != network.LocalClientId) return;
        PartyAudio.Instance?.Play(PartyAudio.Settings?.connected);
        connecting = false; Notify("🎉 ¡Dentro! Todos deben marcarse como listos."); Refresh();
    }

    void Disconnected(ulong id)
    {
        if (id == network.LocalClientId || !network.IsClient)
        {
            if (leaving) return;
            PlayDisconnect();
            connecting = false;
            string reason = network.DisconnectReason;
            StopNetwork();
            Notify(string.IsNullOrEmpty(reason) ? "Se ha cerrado la sala." : reason);
            Refresh(false);
        }
    }
    void PlayDisconnect()
    {
        if (disconnectSoundPlayed) return;
        disconnectSoundPlayed = true;
        PartyAudio.Instance?.Play(PartyAudio.Settings?.disconnected);
    }
    void TransportFailed() => Fail("Error de transporte. Revisa la conexión.");
    void StopNetwork()
    {
        leaving = true;
        connecting = false;
        if (network.IsListening && !network.ShutdownInProgress) network.Shutdown();
    }
    void Fail(string message) { PartyAudio.Instance?.Play(PartyAudio.Settings?.uiError); StopNetwork(); Notify(message); Refresh(false); }
    void Leave() { if (leaving) return; PlayDisconnect(); StopNetwork(); Notify("👋 Has salido de la sala."); Refresh(false); }
    void Refresh() => Refresh(!leaving && network.IsConnectedClient);

    void Refresh(bool inRoom)
    {
        home.style.display = inRoom ? DisplayStyle.None : DisplayStyle.Flex;
        room.style.display = inRoom ? DisplayStyle.Flex : DisplayStyle.None;
        bool canConnect = !connecting && !leaving && !network.ShutdownInProgress;
        host.SetEnabled(canConnect); join.SetEnabled(canConnect); connect.SetEnabled(canConnect);
        bool faceOpen = faceCapture != null && faceCapture.IsOpen;
        bool gameSettingsOpen = gameSettings != null && gameSettings.IsOpen;
        playerName.SetEnabled(!inRoom && !connecting && !faceOpen && !gameSettingsOpen);
        faceButton.SetEnabled(!connecting && faceCapture != null && !faceOpen && !settingsOpen && !gameSettingsOpen);
        gameSettingsButton.SetEnabled(!connecting && !leaving && !faceOpen && !settingsOpen && !gameSettingsOpen &&
            joinPanel.style.display != DisplayStyle.Flex);
        if (gameSettingsOpen) { home.SetEnabled(false); room.SetEnabled(false); }
        if (!inRoom && settingsOpen) { ShowSettings(false); room.SetEnabled(true); }
        if (settingsOpen && !shownSettings.Equals(lobby.Settings.Value)) PopulateSettings();
        if (inRoom) joinPanel.style.display = DisplayStyle.None;
        if (!inRoom || !lobby.IsSpawned) return;
        roster.Clear();
        count.text = $"PARTICIPANTES  /  {lobby.Players.Count:00}";
        foreach (var player in lobby.Players)
        {
            var row = Element(roster, "player-row");
            row.AddToClassList(player.Ready ? "is-ready" : "is-waiting");
            var portrait = new Image { image = lobby.Portrait(player.ClientId), scaleMode = ScaleMode.StretchToFill };
            portrait.AddToClassList("player-portrait"); row.Add(portrait);
            var identity = Element(row, "player-identity");
            Text(identity, player.Name.ToString(), "player-name");
            Text(identity, player.ClientId == 0 ? "ANFITRIÓN" : "INVITADO", "player-role");
            Text(row, player.Ready ? "LISTO" : "ESPERANDO", "player-state");
            if (player.ClientId == network.LocalClientId) ready.text = player.Ready ? "LISTO ✓  ·  CANCELAR" : "ESTOY LISTO";
        }
        var choices = new List<string>();
        for (int i = 0; i < lobby.Maps.Length; i++) choices.Add($"{i + 1}. {(lobby.Maps[i] != null ? lobby.Maps[i].displayName : "Sin configurar")}");
        choices.Add("Aleatorio");
        maps.choices = choices;
        int selected = lobby.SelectedMap.Value;
        preview.EnableInClassList("is-random", selected == LobbyManager.RandomMap);
        if (selected != shownMap) { if (shownMap >= 0) mapChangedAt = Time.unscaledTime; shownMap = selected; }
        if (selected == LobbyManager.RandomMap)
        {
            maps.SetValueWithoutNotify(choices[choices.Count - 1]);
            previewTitle.text = "MAPA ALEATORIO";
            mapDescription.text = "El anfitrión sorteará un mapa disponible al empezar la partida.";
            preview.style.backgroundImage = new StyleBackground(randomMapPreview);
        }
        else if (selected >= 0 && selected < lobby.Maps.Length)
        {
            maps.SetValueWithoutNotify(choices[selected]);
            var map = lobby.Maps[selected];
            previewTitle.text = map != null ? map.displayName : "Sin configurar";
            mapDescription.text = map != null && !string.IsNullOrWhiteSpace(map.description) ? map.description : "Un buen escondite está a la vista de todos.";
            preview.style.backgroundImage = map != null && map.preview != null ? new StyleBackground(map.preview) : StyleKeyword.None;
        }
        else mapDescription.text = "Añade mapas al LobbyManager desde el Inspector.";
        maps.SetEnabled(network.IsServer);
        start.style.display = network.IsServer ? DisplayStyle.Flex : DisplayStyle.None;
        start.SetEnabled(lobby.CanStart);
    }

    void OnDestroy()
    {
        if (faceCapture != null) faceCapture.Closed -= FaceClosed;
        if (lobby != null) lobby.Changed -= Refresh;
        if (network != null)
        {
            network.OnClientConnectedCallback -= Connected;
            network.OnClientDisconnectCallback -= Disconnected;
            network.OnTransportFailure -= TransportFailed;
        }
    }
}
