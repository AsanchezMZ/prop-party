using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

[RequireComponent(typeof(UIDocument))]
public class MatchHUD : MonoBehaviour
{
    [SerializeField] PropHuntMatch match;
    [SerializeField] StyleSheet theme;
    [SerializeField] string lobbyScene;
    Label timer, role, hint, result;
    VisualElement blackout, actions;
    Button replay, lobby;
    NetworkManager network;
    bool leaving;

    void Start()
    {
        network = NetworkManager.Singleton;
        network.OnClientDisconnectCallback += Disconnected;
        var document = GetComponent<UIDocument>();
        if (document.panelSettings == null)
        { Debug.LogError("Assign the party PanelSettings to the HUD UIDocument.", this); enabled = false; return; }
        var root = document.rootVisualElement;
        if (theme != null) root.styleSheets.Add(theme);
        root.style.flexGrow = 1;
        root.pickingMode = PickingMode.Ignore;
        blackout = new VisualElement(); blackout.style.position = Position.Absolute;
        blackout.style.left = blackout.style.right = blackout.style.top = blackout.style.bottom = 0;
        blackout.style.backgroundColor = new Color(0.08f, 0.06f, 0.16f); root.Add(blackout);
        var bar = new VisualElement(); bar.AddToClassList("card"); bar.style.maxWidth = 650; root.Add(bar);
        role = new Label(); role.AddToClassList("card-title"); bar.Add(role);
        timer = new Label(); timer.style.color = Color.white; bar.Add(timer);
        hint = new Label(); hint.AddToClassList("description"); bar.Add(hint);
        result = new Label(); result.AddToClassList("card-title"); bar.Add(result);
        actions = new VisualElement(); bar.Add(actions);
        replay = new Button(() => match.BeginRound()) { text = "OTRA RONDA" }; replay.AddToClassList("primary"); actions.Add(replay);
        lobby = new Button(() => match.ReturnToLobby()) { text = "VOLVER A LA SALA" }; lobby.AddToClassList("secondary"); actions.Add(lobby);
        var leave = new Button(Leave) { text = "DESCONECTAR" }; leave.AddToClassList("quiet"); actions.Add(leave);
    }

    void Update()
    {
        if (match == null || !match.IsSpawned || network.LocalClient == null) return;
        var player = network.LocalClient.PlayerObject != null ? network.LocalClient.PlayerObject.GetComponent<PropHuntPlayer>() : null;
        if (player == null) return;
        bool hiding = match.Phase.Value == MatchPhase.Hiding;
        bool finished = match.Phase.Value == MatchPhase.Finished;
        bool waiting = match.Phase.Value == MatchPhase.Waiting;
        blackout.style.display = hiding && player.Role.Value == PlayerRole.Hunter ? DisplayStyle.Flex : DisplayStyle.None;
        role.text = player.Role.Value == PlayerRole.Prop ? $"¡DISIMULA!  ·  {player.Health.Value} vidas" :
            player.Role.Value == PlayerRole.Hunter ? "¡A CAZAR!" : "ESPECTADOR";
        timer.text = waiting ? "Esperando jugadores / configuración" : $"{(hiding ? "Escondite" : "Caza")}  ·  {Mathf.CeilToInt((float)match.SecondsLeft):000} s";
        hint.text = player.Role.Value == PlayerRole.Prop ? "E camuflar · Q/R girar · F suspender · Espacio saltar" + (player.Frozen.Value ? " · SUSPENDIDO" : "") :
            player.Role.Value == PlayerRole.Hunter ? (hiding ? "Espera… los props se están escondiendo." : "Clic lanzar · Esc menú · Tab continuar · escucha los silbidos") : "→ cambiar jugador observado";
        result.text = finished ? (match.Winner.Value == PlayerRole.Prop ? "¡GANAN LOS PROPS!" : "¡GANAN LOS HUNTERS!") : "";
        actions.style.display = finished || waiting || Cursor.lockState != CursorLockMode.Locked ? DisplayStyle.Flex : DisplayStyle.None;
        replay.style.display = network.IsServer && (finished || waiting) ? DisplayStyle.Flex : DisplayStyle.None;
        lobby.style.display = network.IsServer ? DisplayStyle.Flex : DisplayStyle.None;
        if (finished || waiting) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }

    void Disconnected(ulong id) { if (!network.IsServer && (id == network.LocalClientId || !network.IsConnectedClient)) Leave(); }
    void Leave()
    {
        if (leaving) return;
        leaving = true;
        network.Shutdown();
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        if (Application.CanStreamedLevelBeLoaded(lobbyScene)) SceneManager.LoadScene(lobbyScene);
    }
    void OnDestroy()
    {
        if (network != null) network.OnClientDisconnectCallback -= Disconnected;
    }
}
