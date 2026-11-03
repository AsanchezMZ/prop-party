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
    float scorePulse, cuePulse, phasePulse, healthPulse, bannerUntil;
    Label phaseBanner, winnerTitle;
    Label winnerTeam;
    VisualElement winnerSplash, resultPaper;
    PlayerRole resultRole = PlayerRole.Spectator;
    float phaseStartedAt;
    public const float IdentificationDelay = 1.25f;
    Label identifiedName;
    PropHuntPlayer identificationTarget;
    float identificationSince;
    VisualElement whistleFill, whistleRecharge, identityCard;
    Label whistleLabel;
    MatchPhase lastPhase = (MatchPhase)255;
    VisualElement hudRoot, clockCard, clockFill, vitalsCard, healthPips, riskCard, riskSegments, abilityCard, hitMarker;
    Label objective, phaseLabel;
    HudScreenEffect dangerEffect, damageEffect;
    float hitConfirm, dangerStrength;
    int shownPipCount = -1;
    public bool HeartbeatActive { get; private set; }

    void Start()
    {
        network = NetworkManager.Singleton;
        var document = GetComponent<UIDocument>();
        if (document.panelSettings == null || document.visualTreeAsset == null)
        { Debug.LogError("Assign MatchHUD.uxml and PartyPanel to the HUD UIDocument.", this); enabled = false; return; }
        network.OnClientDisconnectCallback += Disconnected;
        var root = document.rootVisualElement;
        hudRoot = root.Q("hud-root");
        root.Query<VisualElement>().ForEach(element => { if (!(element is Button)) element.pickingMode = PickingMode.Ignore; });
        root.pickingMode = PickingMode.Ignore;
        nameLayer = new VisualElement { pickingMode = PickingMode.Ignore };
        nameLayer.AddToClassList("name-layer"); root.Q("hud-root").Insert(0, nameLayer);
        timer = root.Q<Label>("timer"); role = root.Q<Label>("role"); hint = root.Q<Label>("hint");
        result = root.Q<Label>("result"); score = root.Q<Label>("score"); health = root.Q<Label>("health"); risk = root.Q<Label>("risk");
        targetProp = root.Q<Label>("target-prop"); resultTeam = root.Q<Label>("result-team"); releaseTimer = root.Q<Label>("release-timer");
        targetProp.enableRichText = false;
        identifiedName = new Label { enableRichText = false, pickingMode = PickingMode.Ignore };
        identifiedName.AddToClassList("identified-name");
        identifiedName.style.display = DisplayStyle.None;
        root.Q("hud-root").Add(identifiedName);
        blackout = root.Q("blackout"); actions = root.Q("actions"); crosshair = root.Q("crosshair");
        toasts = root.Q("toasts"); riskHalo = root.Q("risk-halo"); hitFlash = root.Q("hit-flash"); leaderboard = root.Q("leaderboard");
        replay = root.Q<Button>("replay"); lobby = root.Q<Button>("lobby"); resume = root.Q<Button>("resume"); whistle = root.Q<Button>("whistle");
        phaseBanner = root.Q<Label>("phase-banner");
        winnerTitle = root.Q<Label>("winner-title");
        winnerTeam = root.Q<Label>("winner-team");
        winnerSplash = root.Q("winner-splash"); resultPaper = root.Q("result-paper");
        objective = root.Q<Label>("objective"); phaseLabel = root.Q<Label>("phase-label");
        clockCard = root.Q("clock-card"); clockFill = root.Q("clock-fill"); vitalsCard = root.Q("vitals-card");
        healthPips = root.Q("health-pips"); riskCard = root.Q("risk-card"); abilityCard = root.Q("ability-card"); hitMarker = root.Q("hit-marker");
        riskSegments = riskCard.Q(className: "risk-meter");
        dangerEffect = new HudScreenEffect(false); riskHalo.Add(dangerEffect);
        damageEffect = new HudScreenEffect(true); hitFlash.Add(damageEffect);
        whistleFill = root.Q("whistle-fill"); whistleRecharge = root.Q("whistle-recharge");
        whistleLabel = root.Q<Label>("whistle-label"); identityCard = root.Q("identity-card");
        replay.clicked += () => { if (!match.BeginRound()) Toast(MatchHudStrings.CannotStartToast); };
        lobby.clicked += () => match.ReturnToLobby();
        resume.clicked += () => { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; };
        whistle.clicked += () => { if (player != null) player.ManualWhistleServerRpc(); };
        root.Q<Button>("leave").clicked += Leave;
    }

    void BindPlayer(PropHuntPlayer value)
    {
        UnbindPlayer();
        player = value; controller = player.GetComponent<PlayerController>();
        resultRole = player.Role.Value;
        player.Cue += OnCue; player.Score.OnValueChanged += ScoreChanged;
        player.HunterNearby.OnValueChanged += NearbyChanged;
        player.InHunterSight.OnValueChanged += SightChanged;
        player.Health.OnValueChanged += HealthChanged;
        pendingPoints = 0; flash = 0; hitConfirm = 0; dangerStrength = 0;
        identificationTarget = null;
    }

    void UnbindPlayer()
    {
        if (player == null) return;
        player.Cue -= OnCue; player.Score.OnValueChanged -= ScoreChanged;
        player.HunterNearby.OnValueChanged -= NearbyChanged;
        player.InHunterSight.OnValueChanged -= SightChanged;
        player.Health.OnValueChanged -= HealthChanged;
    }

    void Update()
    {
        HeartbeatActive = false;
        if (match == null || !match.IsSpawned || network.LocalClient == null || network.LocalClient.PlayerObject == null) return;
        var current = network.LocalClient.PlayerObject.GetComponent<PropHuntPlayer>();
        if (current == null) return;
        if (player != current) BindPlayer(current);
        var phase = match.Phase.Value;
        bool hiding = phase == MatchPhase.Hiding, finished = phase == MatchPhase.Finished, waiting = phase == MatchPhase.Waiting;
        bool prop = player.Role.Value == PlayerRole.Prop, hunter = player.Role.Value == PlayerRole.Hunter;
        if (prop || hunter) resultRole = player.Role.Value;
        bool won = resultRole == match.Winner.Value;
        identityCard.EnableInClassList("is-prop", prop);
        identityCard.EnableInClassList("is-hunter", hunter);
        identityCard.EnableInClassList("is-spectator", !prop && !hunter);
        hudRoot.EnableInClassList("is-blackout", hiding && hunter);
        hudRoot.EnableInClassList("compact", hudRoot.resolvedStyle.width < 1050 || hudRoot.resolvedStyle.height < 650);
        if (phase != lastPhase)
        {
            pendingPoints = 0;
            if (!hiding || prop) Toast(waiting ? MatchHudStrings.WaitingToast : hiding ? MatchHudStrings.HidingToast : finished ?
                MatchHudStrings.FinishedBanner : MatchHudStrings.HuntingToast);
            lastPhase = phase;
            phaseStartedAt = Time.unscaledTime;
            phasePulse = 1;
            bannerUntil = Time.unscaledTime + 2.8f;
            phaseBanner.text = hiding ? MatchHudStrings.HidingBanner : finished ? MatchHudStrings.FinishedBanner : waiting ? MatchHudStrings.WaitingBanner : MatchHudStrings.HuntingBanner;
        }
        bool locked = Cursor.lockState == CursorLockMode.Locked;
        float resultElapsed = Time.unscaledTime - phaseStartedAt;
        int resultStage = ResultStage(resultElapsed);
        winnerSplash.style.display = finished && resultStage == 1 ? DisplayStyle.Flex : DisplayStyle.None;
        winnerSplash.EnableInClassList("is-win", won);
        winnerSplash.EnableInClassList("is-lose", !won && resultRole != PlayerRole.Spectator);
        winnerTitle.text = resultRole == PlayerRole.Spectator ? MatchHudStrings.SpectatorResult : won ? MatchHudStrings.VictoryResult : MatchHudStrings.DefeatResult;
        winnerTeam.text = match.Winner.Value == PlayerRole.Prop ? MatchHudStrings.PropsWinResult : MatchHudStrings.HuntersWinResult;
        float titleEntry = Mathf.SmoothStep(0, 1, Mathf.Clamp01((resultElapsed - 3) / 0.5f));
        float titleExit = Mathf.SmoothStep(0, 1, Mathf.Clamp01((resultElapsed - 5.4f) / 0.6f));
        winnerSplash.style.opacity = ResultTitleOpacity(resultElapsed);
        winnerSplash.style.translate = new Translate(0, 45 * (1 - titleEntry) - 30 * titleExit);
        winnerSplash.style.scale = new Scale(Vector3.one * (0.72f + 0.28f * titleEntry + 0.16f * Mathf.Sin(titleEntry * Mathf.PI) + 0.12f * titleExit));
        phaseBanner.style.display = !hiding || prop ? DisplayStyle.Flex : DisplayStyle.None;
        blackout.style.display = hiding && hunter ? DisplayStyle.Flex : DisplayStyle.None;
        releaseTimer.text = string.Format(MatchHudStrings.CountdownFormat, Mathf.CeilToInt((float)match.SecondsLeft));
        crosshair.style.display = locked && !finished && !waiting && (prop || (hunter && !hiding)) ? DisplayStyle.Flex : DisplayStyle.None;
        role.text = prop ? MatchHudStrings.PropRole : hunter ? MatchHudStrings.HunterRole : MatchHudStrings.SpectatorRole;
        objective.text = prop ? (player.Frozen.Value ? MatchHudStrings.FrozenObjective : MatchHudStrings.PropObjective) :
            hunter ? MatchHudStrings.HunterObjective : MatchHudStrings.SpectatorObjective;
        phaseLabel.text = waiting ? MatchHudStrings.WaitingPhase : hiding ? MatchHudStrings.HidingPhase : finished ? MatchHudStrings.FinishedPhase : MatchHudStrings.HuntingPhase;
        int seconds = Mathf.CeilToInt((float)match.SecondsLeft);
        scorePulse = Mathf.MoveTowards(scorePulse, 0, Time.unscaledDeltaTime * 3);
        cuePulse = Mathf.MoveTowards(cuePulse, 0, Time.unscaledDeltaTime * 4);
        phasePulse = Mathf.MoveTowards(phasePulse, 0, Time.unscaledDeltaTime * 2);
        healthPulse = Mathf.MoveTowards(healthPulse, 0, Time.unscaledDeltaTime * 2);
        health.style.translate = new Translate(Mathf.Sin(healthPulse * 32) * healthPulse * 7, 0);
        float bannerAge = 2.8f - (bannerUntil - Time.unscaledTime);
        phaseBanner.style.opacity = Mathf.Clamp01(bannerAge / 0.2f) * Mathf.Clamp01((bannerUntil - Time.unscaledTime) / 0.45f);
        phaseBanner.style.translate = new Translate(0, -30 * (1 - Mathf.Clamp01(bannerAge / 0.35f)));
        phaseBanner.style.scale = new Scale(Vector3.one * (1 + 0.12f * Mathf.Sin(Mathf.Clamp01(bannerAge / 0.5f) * Mathf.PI)));
        releaseTimer.style.scale = new Scale(Vector3.one * (1 + 0.06f * Mathf.Pow(Mathf.Max(0, Mathf.Sin(Time.unscaledTime * Mathf.PI * 2)), 6)));
        score.style.scale = new Scale(Vector3.one * (1 + 0.18f * Mathf.Sin(scorePulse * Mathf.PI)));
        crosshair.style.scale = new Scale(Vector3.one * (1 + cuePulse * 0.35f));
        crosshair.EnableInClassList("has-target", controller.AimedProp != null || controller.AimedDoor != null);
        hitConfirm = Mathf.MoveTowards(hitConfirm, 0, Time.unscaledDeltaTime * 2.5f);
        hitMarker.style.opacity = locked && hunter && !hiding && !finished ? Mathf.Clamp01(hitConfirm * 3) : 0;
        hitMarker.style.scale = new Scale(Vector3.one * (1 + (1 - hitConfirm) * 0.35f));
        role.style.translate = new Translate(0, -12 * Mathf.Sin(phasePulse * Mathf.PI));
        result.style.scale = new Scale(Vector3.one * (1 + 0.04f * Mathf.Sin(phasePulse * Mathf.PI)));
        float urgency = phase == MatchPhase.Hunting && seconds <= 10 ? 1 + 0.07f * Mathf.Sin(Time.unscaledTime * 8) : 1;
        timer.style.scale = new Scale(Vector3.one * urgency);
        timer.text = waiting ? MatchHudStrings.WaitingTimer : string.Format(MatchHudStrings.TimerFormat, Mathf.Max(0, seconds) / 60, Mathf.Max(0, seconds) % 60);
        clockCard.EnableInClassList("is-urgent", phase == MatchPhase.Hunting && seconds <= 30);
        float duration = hiding ? match.Settings.Value.HidingSeconds : match.Settings.Value.HuntingSeconds;
        clockFill.style.width = Length.Percent(waiting || finished ? 0 : 100 * Mathf.Clamp01((float)match.SecondsLeft / Mathf.Max(1, duration)));
        int maximum = hunter ? match.Settings.Value.HunterMissLimit : match.Settings.Value.Health;
        int remaining = hunter ? player.HunterMissesRemaining.Value : player.Health.Value;
        health.text = hunter && maximum == 0 ? MatchHudStrings.UnlimitedHealth : string.Format(MatchHudStrings.HealthFormat, remaining);
        vitalsCard.style.display = prop || hunter ? DisplayStyle.Flex : DisplayStyle.None;
        UpdateHealthPips(remaining, maximum);
        score.text = string.Format(MatchHudStrings.ScoreFormat, player.Score.Value);
        riskCard.style.display = prop ? DisplayStyle.Flex : DisplayStyle.None;
        float radius = Mathf.Max(1, match.Rules.proximityRadius);
        float nearest = radius;
        if (prop && phase == MatchPhase.Hunting)
            foreach (var other in match.Players)
            {
                if (other == null || other == player || !other.IsSpawned || other.Role.Value != PlayerRole.Hunter) continue;
                float distance = Vector3.Distance(player.transform.position, other.transform.position);
                if (distance < nearest) nearest = distance;
            }
        bool confirmed = prop && phase == MatchPhase.Hunting && player.InHunterSight.Value;
        int lit = PresenceSegments(nearest, radius, riskSegments.childCount);
        HeartbeatActive = prop && phase == MatchPhase.Hunting && IsPresenceAlert(lit, confirmed);
        risk.text = confirmed ? MatchHudStrings.SeenRisk : lit > 0 ? MatchHudStrings.NearbyRisk : MatchHudStrings.CalmRisk;
        riskCard.EnableInClassList("is-near", lit >= 3 && !confirmed);
        riskCard.EnableInClassList("is-seen", confirmed);
        for (int i = 0; i < riskSegments.childCount; i++) riskSegments[i].EnableInClassList("is-lit", i < lit);
        float cooldown = Mathf.Max(0, (float)(player.NextManualWhistle.Value - network.ServerTime.Time));
        whistleFill.style.width = Length.Percent(100 * CountdownFraction(network.ServerTime.Time,
            player.AutomaticWhistleStartedAt.Value, player.NextAutomaticWhistle.Value));
        whistleRecharge.style.width = Length.Percent(phase == MatchPhase.Hunting ? 100 * (1 - Mathf.Clamp01(cooldown / PropHuntPlayer.ManualWhistleCooldown)) : 0);
        whistle.EnableInClassList("whistle-ready", cooldown <= 0 && phase == MatchPhase.Hunting);
        risk.style.scale = new Scale(Vector3.one * (1 + (HeartbeatActive ? 0.1f * Mathf.Pow(Mathf.Max(0, Mathf.Sin(Time.unscaledTime * 6)), 8) : 0)));
        abilityCard.style.display = prop ? DisplayStyle.Flex : DisplayStyle.None;
        whistleLabel.text = cooldown > 0 ? string.Format(MatchHudStrings.WhistleCooldownFormat, cooldown) : MatchHudStrings.WhistleReady;
        whistle.SetEnabled(prop && phase == MatchPhase.Hunting && cooldown <= 0);
        float threat = HeartbeatActive && locked ? (confirmed ? 1 : 0.55f) : 0;
        dangerStrength = threat > 0 ? Mathf.MoveTowards(dangerStrength, threat, Time.unscaledDeltaTime * 2) : 0;
        float heartbeat = 0.8f + 0.2f * Mathf.Pow(Mathf.Max(0, Mathf.Sin(Time.unscaledTime * 4.5f)), 4);
        dangerEffect.SetState(dangerStrength * heartbeat, player.InHunterSight.Value ? new Color(1, 0.32f, 0.25f) : new Color(1, 0.73f, 0.22f), Time.unscaledTime);
        flash = Mathf.MoveTowards(flash, 0, Time.unscaledDeltaTime * 1.8f);
        damageEffect.SetState(flash, new Color(1, 0.28f, 0.23f), Time.unscaledTime);
        hint.text = prop ? string.Format(MatchHudStrings.PropHintFormat, player.Frozen.Value ? MatchHudStrings.FrozenHint : string.Empty,
            controller.FirstPerson ? MatchHudStrings.FirstPerson : MatchHudStrings.ThirdPerson) :
            hunter ? MatchHudStrings.HunterHint : MatchHudStrings.SpectatorHint;
        targetProp.style.display = locked && !finished && (controller.AimedProp != null || controller.AimedDoor != null) ? DisplayStyle.Flex : DisplayStyle.None;
        targetProp.text = controller.AimedDoor != null ? (controller.AimedDoor.IsOpen ? MatchHudStrings.CloseDoor : MatchHudStrings.OpenDoor) :
            controller.AimedProp != null ? string.Format(MatchHudStrings.PropInteractionFormat, controller.AimedProp.propName) : string.Empty;
        bool menu = finished ? resultStage == 2 : waiting || ((prop || hunter) ? !locked : controller.SpectatorMenu);
        actions.style.display = menu ? DisplayStyle.Flex : DisplayStyle.None;
        float paperEntry = finished ? Mathf.Clamp01((resultElapsed - 6) / 0.7f) : 1;
        float paperEase = 1 - Mathf.Pow(1 - paperEntry, 3);
        actions.style.opacity = paperEntry;
        resultPaper.style.translate = new Translate(70 * (1 - paperEase), 180 * (1 - paperEase));
        resultPaper.style.rotate = new Rotate(new Angle(-12 * (1 - paperEase)));
        resultPaper.style.scale = new Scale(Vector3.one * (0.88f + 0.12f * paperEase));
        actions.EnableInClassList("show-results", finished);
        replay.style.display = network.IsServer && (finished || waiting) ? DisplayStyle.Flex : DisplayStyle.None;
        lobby.style.display = network.IsServer ? DisplayStyle.Flex : DisplayStyle.None;
        resume.style.display = !finished && !waiting && (prop || hunter) ? DisplayStyle.Flex : DisplayStyle.None;
        result.text = finished ? (resultRole == PlayerRole.Spectator ? MatchHudStrings.SpectatorResult : won ? MatchHudStrings.VictoryResult : MatchHudStrings.DefeatResult) : waiting ? MatchHudStrings.WaitingResult : MatchHudStrings.PauseResult;
        result.EnableInClassList("result-title", finished);
        result.EnableInClassList("result-win", finished && won);
        result.EnableInClassList("result-lose", finished && !won && resultRole != PlayerRole.Spectator);
        resultTeam.style.display = finished ? DisplayStyle.Flex : DisplayStyle.None;
        resultTeam.text = match.Winner.Value == PlayerRole.Prop ? MatchHudStrings.PropsWinResult : MatchHudStrings.HuntersWinResult;
        if (menu && Time.unscaledTime >= nextRanking) { nextRanking = Time.unscaledTime + 0.5f; RefreshRanking(); }
        if (pendingPoints > 0 && Time.unscaledTime >= nextScoreToast)
        { Toast(string.Format(MatchHudStrings.ScoreToastFormat, pendingPoints)); pendingPoints = 0; nextScoreToast = Time.unscaledTime + 2; }
    }

    public static bool CanSeeName(PlayerRole viewer, PlayerRole target, bool occluded) =>
        viewer != PlayerRole.Spectator && target != PlayerRole.Spectator &&
        (viewer == target || (target == PlayerRole.Hunter && !occluded));

    public static int FilledSegments(int current, int maximum, int count) => maximum <= 0 ? 0 :
        Mathf.CeilToInt(Mathf.Clamp01(current / (float)maximum) * Mathf.Max(0, count));

    public static bool IsPresenceAlert(int lit, bool confirmed) => confirmed || lit >= 3;

    public static int ResultStage(float elapsed) => elapsed < 3 ? 0 : elapsed < 6 ? 1 : 2;

    public static float ResultTitleOpacity(float elapsed) =>
        Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - 3) / 0.5f)) *
        (1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - 5.4f) / 0.6f)));

    public static float CountdownFraction(double now, double startedAt, double deadline) =>
        deadline <= startedAt ? 0 : Mathf.Clamp01((float)((deadline - now) / (deadline - startedAt)));

    public static int PresenceSegments(float distance, float radius, int count)
    {
        count = Mathf.Max(0, count);
        radius = Mathf.Max(1, radius);
        if (!(distance < radius)) return 0;
        return Mathf.CeilToInt((1 - Mathf.Clamp01(distance / radius)) * count);
    }

    void UpdateHealthPips(int current, int maximum)
    {
        int count = Mathf.Clamp(maximum, 0, 10);
        if (shownPipCount != count)
        {
            shownPipCount = count; healthPips.Clear();
            for (int i = 0; i < count; i++)
            {
                var pip = new VisualElement { pickingMode = PickingMode.Ignore };
                pip.AddToClassList("health-pip"); healthPips.Add(pip);
            }
        }
        int filled = FilledSegments(current, maximum, count);
        for (int i = 0; i < count; i++) healthPips[i].EnableInClassList("is-empty", i >= filled);
    }

    void LateUpdate()
    {
        if (nameLayer == null || player == null || match == null || controller == null) return;
        bool active = match.IsSpawned && (match.Phase.Value == MatchPhase.Hunting ||
            (match.Phase.Value == MatchPhase.Hiding && player.Role.Value == PlayerRole.Prop)) &&
            Cursor.lockState == CursorLockMode.Locked;
        UpdateIdentification(active);
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
            string arrow = outside ? (Mathf.Abs(direction.x) > Mathf.Abs(direction.y) ? (direction.x > 0 ? MatchHudStrings.ArrowRight : MatchHudStrings.ArrowLeft) :
                direction.y > 0 ? MatchHudStrings.ArrowUp : MatchHudStrings.ArrowDown) : blocked ? MatchHudStrings.OccludedArrow : string.Empty;
            float x = outside ? 0.5f + direction.normalized.x * 0.48f : viewport.x;
            float y = outside ? 0.5f + direction.normalized.y * 0.48f : viewport.y;
            tag.style.left = Mathf.Clamp(x * width - 90, 8, width - 188);
            tag.style.top = Mathf.Clamp((1 - y) * height - 32, 64, height - 80);
            tag.style.color = other.Role.Value == PlayerRole.Hunter ? new Color(1, 0.24f, 0.24f) : Color.white;
            tag.text = string.Format(MatchHudStrings.NameTagFormat, arrow, other.DisplayName.Value.ToString()); tag.style.display = DisplayStyle.Flex;
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
            var label = new Label(string.Format(MatchHudStrings.RankingRowFormat, i + 1, entry.DisplayName.Value.ToString(), entry.Score.Value)) { enableRichText = false };
            label.EnableInClassList("ranking-leader", i == 0);
            label.EnableInClassList("ranking-self", entry == player);
            leaderboard.Add(label);
        }
    }

    void ScoreChanged(int oldValue, int value)
    {
        if (value > oldValue) scorePulse = 1;
        pendingPoints = value > oldValue ? (int)System.Math.Min(int.MaxValue, (long)pendingPoints + value - oldValue) : 0;
    }

    void UpdateIdentification(bool active)
    {
        identifiedName.style.display = DisplayStyle.None;
        PropHuntPlayer target = null;
        if (active && match.Settings.Value.IdentifyHiders && player.Role.Value != PlayerRole.Spectator && controller.ViewCamera != null)
        {
            var camera = controller.ViewCamera.transform;
            var hits = Physics.RaycastAll(camera.position, camera.forward, match.Rules.visibilityDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                var other = hit.collider.GetComponentInParent<PropHuntPlayer>();
                if (other == player || (other != null && (other.Role.Value == PlayerRole.Spectator || other.IgnoreMovementHitbox(hit.collider)))) continue;
                if (other != null && other.IsSpawned && other.Role.Value == PlayerRole.Prop) target = other;
                break;
            }
        }
        if (target != identificationTarget)
        {
            identificationTarget = target;
            identificationSince = Time.unscaledTime;
        }
        if (target == null || Time.unscaledTime - identificationSince < IdentificationDelay) return;
        identifiedName.text = string.Format(MatchHudStrings.IdentifiedNameFormat, target.DisplayName.Value.ToString());
        identifiedName.style.display = DisplayStyle.Flex;
    }
    void HealthChanged(int previous, int current) { if (current < previous) healthPulse = 1; }
    void NearbyChanged(bool oldValue, bool value) { if (value) Toast(MatchHudStrings.NearbyToast); }
    void SightChanged(bool oldValue, bool value) { if (value) Toast(MatchHudStrings.SeenToast); }
    void OnCue(int cue)
    {
        cuePulse = 1;
        if (cue == 12) { hitConfirm = 1; return; }
        if (cue == 6) { flash = 0.85f; Toast(MatchHudStrings.HitToast); }
        else if (cue == 2) { flash = 1; Toast(MatchHudStrings.CapturedToast); }
        else if (cue == 5) Toast(MatchHudStrings.ManualWhistleToast);
        else if (cue == 0) Toast(MatchHudStrings.AutomaticWhistleToast);
        else if (cue == 1) Toast(MatchHudStrings.DisguiseToast);
        else if (cue == 10 || cue == 11)
        {
            healthPulse = 1; flash = 0.2f;
            Toast(cue == 11 ? MatchHudStrings.NoLivesToast : MatchHudStrings.WrongPropToast);
        }
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
