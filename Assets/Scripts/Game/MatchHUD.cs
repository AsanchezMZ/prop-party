using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

[RequireComponent(typeof(UIDocument))]
public class MatchHUD : MonoBehaviour
{
    [SerializeField] PropHuntMatch match;
    [SerializeField] string lobbyScene;
    Label timer, role, hint, result, score, health, risk;
    VisualElement blackout, actions, crosshair, toasts, riskHalo, hitFlash, leaderboard;
    Button replay, lobby, resume, whistle;
    NetworkManager network;
    PropHuntPlayer player;
    PlayerController controller;
    readonly List<PropHuntPlayer> ranking = new();
    bool leaving;
    float flash, nextRanking, nextScoreToast;
    int pendingPoints;
    MatchPhase lastPhase = (MatchPhase)255;

    void Start()
    {
        network = NetworkManager.Singleton;
        var document = GetComponent<UIDocument>();
        if (document.panelSettings == null || document.visualTreeAsset == null)
        { Debug.LogError("Assign MatchHUD.uxml and PartyPanel to the HUD UIDocument.", this); enabled = false; return; }
        network.OnClientDisconnectCallback += Disconnected;
        var root = document.rootVisualElement;
        root.pickingMode = PickingMode.Ignore;
        timer = root.Q<Label>("timer"); role = root.Q<Label>("role"); hint = root.Q<Label>("hint");
        result = root.Q<Label>("result"); score = root.Q<Label>("score"); health = root.Q<Label>("health"); risk = root.Q<Label>("risk");
        blackout = root.Q("blackout"); actions = root.Q("actions"); crosshair = root.Q("crosshair");
        toasts = root.Q("toasts"); riskHalo = root.Q("risk-halo"); hitFlash = root.Q("hit-flash"); leaderboard = root.Q("leaderboard");
        replay = root.Q<Button>("replay"); lobby = root.Q<Button>("lobby"); resume = root.Q<Button>("resume"); whistle = root.Q<Button>("whistle");
        replay.clicked += () => { if (!match.BeginRound()) Toast("👥 Faltan jugadores o puntos de aparición."); };
        lobby.clicked += () => match.ReturnToLobby();
        resume.clicked += () => { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; };
        whistle.clicked += () => { if (player != null) player.ManualWhistleServerRpc(); };
        root.Q<Button>("leave").clicked += Leave;
    }

    void BindPlayer(PropHuntPlayer value)
    {
        UnbindPlayer();
        player = value; controller = player.GetComponent<PlayerController>();
        player.Cue += OnCue; player.Score.OnValueChanged += ScoreChanged;
        player.HunterNearby.OnValueChanged += NearbyChanged;
        player.InHunterSight.OnValueChanged += SightChanged;
        pendingPoints = 0; flash = 0;
    }

    void UnbindPlayer()
    {
        if (player == null) return;
        player.Cue -= OnCue; player.Score.OnValueChanged -= ScoreChanged;
        player.HunterNearby.OnValueChanged -= NearbyChanged;
        player.InHunterSight.OnValueChanged -= SightChanged;
    }

    void Update()
    {
        if (match == null || !match.IsSpawned || network.LocalClient == null || network.LocalClient.PlayerObject == null) return;
        var current = network.LocalClient.PlayerObject.GetComponent<PropHuntPlayer>();
        if (current == null) return;
        if (player != current) BindPlayer(current);
        var phase = match.Phase.Value;
        bool hiding = phase == MatchPhase.Hiding, finished = phase == MatchPhase.Finished, waiting = phase == MatchPhase.Waiting;
        bool prop = player.Role.Value == PlayerRole.Prop, hunter = player.Role.Value == PlayerRole.Hunter;
        if (phase != lastPhase)
        {
            pendingPoints = 0;
            Toast(waiting ? "👥 Esperando jugadores…" : hiding ? "🙈 ¡A esconderse!" : finished ?
                (match.Winner.Value == PlayerRole.Prop ? "🎉 ¡Ganan los escondedores!" : "🔎 ¡Ganan los buscadores!") : "🔥 ¡Empieza la caza!");
            lastPhase = phase;
        }
        bool locked = Cursor.lockState == CursorLockMode.Locked;
        blackout.style.display = hiding && hunter ? DisplayStyle.Flex : DisplayStyle.None;
        crosshair.style.display = locked && !finished && !waiting && (prop || (hunter && !hiding)) ? DisplayStyle.Flex : DisplayStyle.None;
        role.text = prop ? "🎭 ESCONDEDOR" : hunter ? "🔎 BUSCADOR · FPV" : "👻 ESPECTADOR";
        int seconds = Mathf.CeilToInt((float)match.SecondsLeft);
        timer.text = waiting ? "⏳ Esperando" : $"⏱ {seconds / 60:00}:{seconds % 60:00}";
        health.text = $"❤ {player.Health.Value}"; health.style.display = prop ? DisplayStyle.Flex : DisplayStyle.None;
        score.text = $"⭐ {player.Score.Value}";
        risk.style.display = prop ? DisplayStyle.Flex : DisplayStyle.None;
        risk.text = player.InHunterSight.Value ? "👀 ¡A LA VISTA!" : player.HunterNearby.Value ? "🔥 ¡CERCA!" : "🌿 Tranquilo";
        float cooldown = Mathf.Max(0, (float)(player.NextManualWhistle.Value - network.ServerTime.Time));
        whistle.style.display = prop ? DisplayStyle.Flex : DisplayStyle.None;
        whistle.text = cooldown > 0 ? $"🎵 {Mathf.CeilToInt(cooldown)} s" : "🎵 Silbar [G]";
        whistle.SetEnabled(prop && phase == MatchPhase.Hunting && cooldown <= 0);
        riskHalo.style.opacity = prop && (player.HunterNearby.Value || player.InHunterSight.Value) ? 0.22f + 0.22f * (1 + Mathf.Sin(Time.unscaledTime * 6)) : 0;
        riskHalo.style.borderTopColor = riskHalo.style.borderBottomColor = riskHalo.style.borderLeftColor = riskHalo.style.borderRightColor =
            player.InHunterSight.Value ? new Color(1, 0.3f, 0.4f) : new Color(1, 0.72f, 0.2f);
        flash = Mathf.MoveTowards(flash, 0, Time.unscaledDeltaTime * 2);
        hitFlash.style.opacity = flash;
        hint.text = prop ? $"E camuflar · Q/R girar · F suspender{(player.Frozen.Value ? " ❄" : "")} · V {(controller.FirstPerson ? "FPV" : "TPV")} · G silbar · Esc menú" :
            hunter ? "Clic lanzar · Esc menú · Tab continuar" : "→ cambiar jugador observado · Esc menú · Tab cerrar";
        bool menu = finished || waiting || ((prop || hunter) ? !locked : controller.SpectatorMenu);
        actions.style.display = menu ? DisplayStyle.Flex : DisplayStyle.None;
        replay.style.display = network.IsServer && (finished || waiting) ? DisplayStyle.Flex : DisplayStyle.None;
        lobby.style.display = network.IsServer ? DisplayStyle.Flex : DisplayStyle.None;
        resume.style.display = !finished && !waiting && (prop || hunter) ? DisplayStyle.Flex : DisplayStyle.None;
        result.text = finished ? (match.Winner.Value == PlayerRole.Prop ? "🎉 ¡GANAN LOS ESCONDEDORES!" : "🔎 ¡GANAN LOS BUSCADORES!") : waiting ? "👥 ESPERANDO AL EQUIPO" : "🍹 UN RESPIRO";
        if (menu && Time.unscaledTime >= nextRanking) { nextRanking = Time.unscaledTime + 0.5f; RefreshRanking(); }
        if (pendingPoints > 0 && Time.unscaledTime >= nextScoreToast)
        { Toast($"⭐ +{pendingPoints} puntos"); pendingPoints = 0; nextScoreToast = Time.unscaledTime + 2; }
    }

    void RefreshRanking()
    {
        ranking.Clear(); ranking.AddRange(match.Players);
        ranking.Sort((a, b) => b.Score.Value.CompareTo(a.Score.Value));
        leaderboard.Clear();
        for (int i = 0; i < ranking.Count; i++)
        {
            var entry = ranking[i];
            var label = new Label($"{i + 1}.  {entry.DisplayName.Value}     ⭐ {entry.Score.Value}") { enableRichText = false };
            leaderboard.Add(label);
        }
    }

    void ScoreChanged(int oldValue, int value)
    {
        pendingPoints = value > oldValue ? (int)System.Math.Min(int.MaxValue, (long)pendingPoints + value - oldValue) : 0;
    }
    void NearbyChanged(bool oldValue, bool value) { if (value) Toast("🔥 Un buscador está cerca. ¡No te delates!"); }
    void SightChanged(bool oldValue, bool value) { if (value) Toast("👀 Estás en su campo de visión. ¡Eso da puntos!"); }
    void OnCue(int cue)
    {
        if (cue == 6) { flash = 0.35f; Toast("💥 ¡Te han dado!"); }
        else if (cue == 2) { flash = 0.6f; Toast("🎊 ¡Te han encontrado!"); }
        else if (cue == 5) Toast("🎵 ¡Silbido manual! Te has delatado.");
        else if (cue == 0) Toast("🎶 Silbido automático · sin puntos");
        else if (cue == 1) Toast("🎭 ¡Camuflaje listo!");
    }

    void Toast(string message)
    {
        if (toasts == null) return;
        if (toasts.childCount >= 3) toasts.RemoveAt(0);
        var toast = new Label(message) { enableRichText = false, pickingMode = PickingMode.Ignore };
        toast.AddToClassList("toast"); toasts.Add(toast);
        toast.schedule.Execute(() => toast.RemoveFromHierarchy()).StartingIn(3500);
    }

    void Disconnected(ulong id) { if (!network.IsServer && (id == network.LocalClientId || !network.IsConnectedClient)) Leave(); }
    void Leave()
    {
        if (leaving) return;
        leaving = true; network.Shutdown();
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        if (Application.CanStreamedLevelBeLoaded(lobbyScene)) SceneManager.LoadScene(lobbyScene);
    }

    void OnDestroy()
    {
        UnbindPlayer();
        if (network != null) network.OnClientDisconnectCallback -= Disconnected;
    }
}
