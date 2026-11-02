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
    SliderInt settingHunters, settingHide, settingHunt, settingWhistle, settingHealth;
    Toggle settingConvert;
    MatchSettings shownSettings;
    Label settingsNote;
    bool settingsOpen;
    IVisualElementScheduledItem toastTimer;
    NetworkManager network;
    bool connecting;
    bool disconnectSoundPlayed;
    float connectionStarted;

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
        if (background != null) content.style.backgroundImage = new StyleBackground(background);
        home = root.Q("home"); room = root.Q("room"); roster = root.Q("roster");
        preview = root.Q("map-preview"); joinPanel = root.Q("join-overlay");
        playerName = root.Q<TextField>("player-name");
        playerName.value = PlayerPrefs.GetString("PlayerName", "Player");
        address = root.Q<TextField>("address");
        status = root.Q<Label>("status"); count = root.Q<Label>("player-count");
        previewTitle = root.Q<Label>("map-title"); mapDescription = root.Q<Label>("map-description");
        maps = root.Q<DropdownField>("maps");
        maps.RegisterValueChangedCallback(_ =>
        {
            if (!network.IsServer || !lobby.IsSpawned || maps.index < 0 || maps.index >= lobby.Maps.Length || maps.index == lobby.SelectedMap.Value) return;
            PartyAudio.Instance?.Play(PartyAudio.Settings?.mapSelectWhoosh);
            lobby.SelectMap(maps.index);
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
        settingsButton = Bind(root, "settings", () => ShowSettings(true));
        Bind(root, "settings-back", () => { PartyAudio.Instance?.Play(PartyAudio.Settings?.uiBack); ShowSettings(false); });
        settingsApply = Bind(root, "settings-apply", () =>
        {
            var settings = new MatchSettings { Hunters = settingHunters.value, HidingSeconds = settingHide.value,
                HuntingSeconds = settingHunt.value, WhistleSeconds = settingWhistle.value,
                Health = settingHealth.value, ConvertCaptured = settingConvert.value };
            if (lobby.ApplySettings(settings))
            { PartyAudio.Instance?.Play(PartyAudio.Settings?.uiConfirm); ShowSettings(false); Notify("Reglas guardadas. ¡Preparaos otra vez!"); }
        });
        faceButton = Bind(root, "face-button", () =>
        {
            home.SetEnabled(false); room.SetEnabled(false); playerName.SetEnabled(false); faceButton.SetEnabled(false);
            faceCapture.Open();
        });
        if (faceCapture != null)
        {
            faceCapture.Bind(root);
            faceCapture.Closed += FaceClosed;
        }
        else faceButton.SetEnabled(false);
        root.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode != KeyCode.Escape) return;
            if (settingsOpen) ShowSettings(false);
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
        { audioSource.clip = menuMusic; audioSource.loop = true; audioSource.Play(); }
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
        settingHunters.SetEnabled(network.IsServer); settingHide.SetEnabled(network.IsServer); settingHunt.SetEnabled(network.IsServer);
        settingWhistle.SetEnabled(network.IsServer); settingHealth.SetEnabled(network.IsServer); settingConvert.SetEnabled(network.IsServer);
        settingsApply.SetEnabled(network.IsServer);
        if (lobby.Rules != null)
            settingsNote.text = $"Cerca: +{lobby.Rules.proximityPoints} cada 3 s. A la vista: +{lobby.Rules.visiblePoints} por segundo.\nSilbar: +{lobby.Rules.manualWhistlePoints}, con 5 s de recarga. Los bonos de riesgo se suman.";
    }

    void ShowJoin(bool visible)
    {
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
            if (audioSource != null && clickSound != null) audioSource.PlayOneShot(clickSound);
            else PartyAudio.Instance?.Play(PartyAudio.Settings?.uiClick);
            action();
        };
        return button;
    }

    void Connect(bool asHost)
    {
        if (network.IsListening || connecting) return;
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
        if (connecting && Time.unscaledTime - connectionStarted > 15) Fail("Sin respuesta. Comprueba la IP y el puerto.");
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
            PlayDisconnect();
            connecting = false;
            string reason = network.DisconnectReason;
            network.Shutdown();
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
    void Fail(string message) { PartyAudio.Instance?.Play(PartyAudio.Settings?.uiError); connecting = false; network.Shutdown(); Notify(message); Refresh(false); }
    void Leave() { PlayDisconnect(); network.Shutdown(); connecting = false; Notify("👋 Has salido de la sala."); Refresh(false); }
    void Refresh() => Refresh(network.IsConnectedClient);

    void Refresh(bool inRoom)
    {
        home.style.display = inRoom ? DisplayStyle.None : DisplayStyle.Flex;
        room.style.display = inRoom ? DisplayStyle.Flex : DisplayStyle.None;
        host.SetEnabled(!connecting); join.SetEnabled(!connecting); connect.SetEnabled(!connecting);
        bool faceOpen = faceCapture != null && faceCapture.IsOpen;
        playerName.SetEnabled(!inRoom && !connecting && !faceOpen);
        faceButton.SetEnabled(!connecting && faceCapture != null && !faceOpen && !settingsOpen);
        if (!inRoom && settingsOpen) { ShowSettings(false); room.SetEnabled(true); }
        if (settingsOpen && !shownSettings.Equals(lobby.Settings.Value)) PopulateSettings();
        if (inRoom) joinPanel.style.display = DisplayStyle.None;
        if (!inRoom || !lobby.IsSpawned) return;
        roster.Clear();
        count.text = $"EL EQUIPO  /  {lobby.Players.Count:00}";
        int number = 0;
        foreach (var player in lobby.Players)
        {
            var row = Element(roster, "player-row");
            row.AddToClassList(player.Ready ? "is-ready" : "is-waiting");
            Text(row, $"{++number:00}", "player-number");
            var identity = Element(row, "player-identity");
            Text(identity, player.Name.ToString(), "player-name");
            Text(identity, player.ClientId == 0 ? "ANFITRIÓN" : "INVITADO", "player-role");
            Text(row, player.Ready ? "LISTO" : "ESPERANDO", "player-state");
            if (player.ClientId == network.LocalClientId) ready.text = player.Ready ? "LISTO ✓  ·  CANCELAR" : "ESTOY LISTO";
        }
        var choices = new List<string>();
        for (int i = 0; i < lobby.Maps.Length; i++) choices.Add($"{i + 1}. {(lobby.Maps[i] != null ? lobby.Maps[i].displayName : "Sin configurar")}");
        maps.choices = choices;
        int selected = lobby.SelectedMap.Value;
        if (selected >= 0 && selected < choices.Count)
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
