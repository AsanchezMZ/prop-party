using System.Collections.Generic;
using System.Linq;
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
    Label timer, role, hint, result, score, health, risk, targetProp, resultTeam, releaseTimer;
    VisualElement nameLayer;
    readonly Dictionary<PropHuntPlayer, Label> nameTags = new();
    readonly List<PropHuntPlayer> staleTags = new();
    VisualElement blackout, actions, crosshair, toasts, riskHalo, hitFlash, leaderboard;
    Button replay, lobby, resume, whistle;
    NetworkManager network;
    PropHuntPlayer player;
    PlayerController controller;
    readonly List<PropHuntPlayer> ranking = new();
    bool leaving;
    float flash, nextRanking, nextScoreToast;
    int pendingPoints;
    float scorePulse, cuePulse, phasePulse;
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
        nameLayer = new VisualElement { pickingMode = PickingMode.Ignore };
        nameLayer.AddToClassList("name-layer"); root.Q("hud-root").Insert(0, nameLayer);
        timer = root.Q<Label>("timer"); role = root.Q<Label>("role"); hint = root.Q<Label>("hint");
        result = root.Q<Label>("result"); score = root.Q<Label>("score"); health = root.Q<Label>("health"); risk = root.Q<Label>("risk");
        targetProp = root.Q<Label>("target-prop"); resultTeam = root.Q<Label>("result-team"); releaseTimer = root.Q<Label>("release-timer");
        targetProp.enableRichText = false;
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
            phasePulse = 1;
        }
        bool locked = Cursor.lockState == CursorLockMode.Locked;
        blackout.style.display = hiding && hunter ? DisplayStyle.Flex : DisplayStyle.None;
        releaseTimer.text = $"{Mathf.CeilToInt((float)match.SecondsLeft):00}";
        crosshair.style.display = locked && !finished && !waiting && (prop || (hunter && !hiding)) ? DisplayStyle.Flex : DisplayStyle.None;
        role.text = prop ? "🎭 ESCONDEDOR" : hunter ? "🔎 BUSCADOR · FPV" : "👻 ESPECTADOR";
        int seconds = Mathf.CeilToInt((float)match.SecondsLeft);
        scorePulse = Mathf.MoveTowards(scorePulse, 0, Time.unscaledDeltaTime * 3);
        cuePulse = Mathf.MoveTowards(cuePulse, 0, Time.unscaledDeltaTime * 4);
        phasePulse = Mathf.MoveTowards(phasePulse, 0, Time.unscaledDeltaTime * 2);
        score.style.scale = new Scale(Vector3.one * (1 + 0.18f * Mathf.Sin(scorePulse * Mathf.PI)));
        crosshair.style.scale = new Scale(Vector3.one * (1 + cuePulse * 2));
        role.style.translate = new Translate(0, -12 * Mathf.Sin(phasePulse * Mathf.PI));
        result.style.scale = new Scale(Vector3.one * (1 + 0.04f * Mathf.Sin(Time.unscaledTime * 2)));
        float urgency = phase == MatchPhase.Hunting && seconds <= 10 ? 1 + 0.07f * Mathf.Sin(Time.unscaledTime * 8) : 1;
        timer.style.scale = new Scale(Vector3.one * urgency);
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
        targetProp.style.display = prop && locked && !finished && controller.AimedProp != null ? DisplayStyle.Flex : DisplayStyle.None;
        targetProp.text = controller.AimedProp != null ? $"[E]  {controller.AimedProp.propName}" : "";
        bool menu = finished || waiting || ((prop || hunter) ? !locked : controller.SpectatorMenu);
        actions.style.display = menu ? DisplayStyle.Flex : DisplayStyle.None;
        replay.style.display = network.IsServer && (finished || waiting) ? DisplayStyle.Flex : DisplayStyle.None;
        lobby.style.display = network.IsServer ? DisplayStyle.Flex : DisplayStyle.None;
        resume.style.display = !finished && !waiting && (prop || hunter) ? DisplayStyle.Flex : DisplayStyle.None;
        bool won = player.Role.Value == match.Winner.Value;
        result.text = finished ? (player.Role.Value == PlayerRole.Spectator ? "¡RONDA TERMINADA!" : won ? "¡VICTORIA!" : "¡DERROTA!") : waiting ? "ESPERANDO AL EQUIPO" : "UN RESPIRO";
        result.EnableInClassList("result-title", finished);
        result.EnableInClassList("result-win", finished && won);
        result.EnableInClassList("result-lose", finished && !won && player.Role.Value != PlayerRole.Spectator);
        resultTeam.style.display = finished ? DisplayStyle.Flex : DisplayStyle.None;
        resultTeam.text = match.Winner.Value == PlayerRole.Prop ? "GANAN LOS ESCONDEDORES" : "GANAN LOS BUSCADORES";
        if (menu && Time.unscaledTime >= nextRanking) { nextRanking = Time.unscaledTime + 0.5f; RefreshRanking(); }
        if (pendingPoints > 0 && Time.unscaledTime >= nextScoreToast)
        { Toast($"⭐ +{pendingPoints} puntos"); pendingPoints = 0; nextScoreToast = Time.unscaledTime + 2; }
    }

    public static bool CanSeeName(PlayerRole viewer, PlayerRole target, bool occluded) =>
        viewer != PlayerRole.Spectator && target != PlayerRole.Spectator &&
        (viewer == target || (target == PlayerRole.Hunter && !occluded));

    void LateUpdate()
    {
        if (nameLayer == null || player == null || match == null || controller == null) return;
        bool active = match.IsSpawned && (match.Phase.Value == MatchPhase.Hunting ||
            (match.Phase.Value == MatchPhase.Hiding && player.Role.Value == PlayerRole.Prop)) &&
            Cursor.lockState == CursorLockMode.Locked;
        nameLayer.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
        staleTags.Clear();
        foreach (var entry in nameTags)
            if (entry.Key == null || !match.Players.Contains(entry.Key)) staleTags.Add(entry.Key);
        foreach (var entry in staleTags) { nameTags[entry].RemoveFromHierarchy(); nameTags.Remove(entry); }
        if (!active) return;
        var camera = controller.ViewCamera;
        float width = nameLayer.resolvedStyle.width, height = nameLayer.resolvedStyle.height;
        if (camera == null || width < 200 || height < 100) return;
        foreach (var other in match.Players)
        {
            if (other == player) continue;
            if (!nameTags.TryGetValue(other, out var tag))
            {
                tag = new Label { enableRichText = false, pickingMode = PickingMode.Ignore };
                tag.AddToClassList("name-tag"); nameLayer.Add(tag); nameTags.Add(other, tag);
            }
            tag.style.display = DisplayStyle.None;
            if (!CanSeeName(player.Role.Value, other.Role.Value, false)) continue;
            var body = other.GetComponent<CharacterController>().bounds;
            var head = new Vector3(body.center.x, body.max.y + 0.25f, body.center.z);
            bool blocked = false;
            var delta = body.center - camera.transform.position;
            foreach (var hit in Physics.RaycastAll(camera.transform.position, delta.normalized, delta.magnitude,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                var owner = hit.collider.GetComponentInParent<PropHuntPlayer>();
                if (owner != player && owner != other) { blocked = true; break; }
            }
            if (!CanSeeName(player.Role.Value, other.Role.Value, blocked)) continue;
            var viewport = camera.WorldToViewportPoint(head);
            bool outside = viewport.z <= 0 || viewport.x < 0.05f || viewport.x > 0.95f || viewport.y < 0.08f || viewport.y > 0.9f;
            bool teammate = other.Role.Value == player.Role.Value;
            if (outside && !teammate) continue;
            Vector2 direction = new(viewport.x - 0.5f, viewport.y - 0.5f);
            if (viewport.z <= 0) direction = -direction;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.down;
            string arrow = outside ? (Mathf.Abs(direction.x) > Mathf.Abs(direction.y) ? (direction.x > 0 ? "→ " : "← ") : direction.y > 0 ? "↑ " : "↓ ") : blocked ? "▼ " : "";
            float x = outside ? 0.5f + direction.normalized.x * 0.48f : viewport.x;
            float y = outside ? 0.5f + direction.normalized.y * 0.48f : viewport.y;
            tag.style.left = Mathf.Clamp(x * width - 90, 8, width - 188);
            tag.style.top = Mathf.Clamp((1 - y) * height - 32, 64, height - 80);
            tag.style.color = other.Role.Value == PlayerRole.Hunter ? new Color(1, 0.24f, 0.24f) : Color.white;
            tag.text = arrow + other.DisplayName.Value.ToString(); tag.style.display = DisplayStyle.Flex;
        }
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
        if (value > oldValue) scorePulse = 1;
        pendingPoints = value > oldValue ? (int)System.Math.Min(int.MaxValue, (long)pendingPoints + value - oldValue) : 0;
    }
    void NearbyChanged(bool oldValue, bool value) { if (value) Toast("🔥 Un buscador está cerca. ¡No te delates!"); }
    void SightChanged(bool oldValue, bool value) { if (value) Toast("👀 Estás en su campo de visión. ¡Eso da puntos!"); }
    void OnCue(int cue)
    {
        cuePulse = 1;
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
        float born = Time.unscaledTime;
        toast.schedule.Execute(() =>
        {
            float age = Time.unscaledTime - born;
            toast.style.opacity = Mathf.Clamp01(age / 0.18f) * Mathf.Clamp01((3.5f - age) / 0.3f);
            toast.style.translate = new Translate(24 * (1 - Mathf.Clamp01(age / 0.25f)), 0);
        }).Every(33).Until(() => toast.parent == null);
        toast.schedule.Execute(() => toast.RemoveFromHierarchy()).StartingIn(3500);
    }

    void Disconnected(ulong id) { if (!network.IsServer && (id == network.LocalClientId || !network.IsConnectedClient)) Leave(); }
    void Leave()
    {
        if (leaving) return;
        leaving = true; network.Shutdown();
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        if (Application.CanStreamedLevelBeLoaded(lobbyScene))
        { PartyLoadingScreen.Show(); SceneManager.LoadSceneAsync(lobbyScene); }
    }

    void OnDestroy()
    {
        UnbindPlayer();
        if (network != null) network.OnClientDisconnectCallback -= Disconnected;
    }
}
