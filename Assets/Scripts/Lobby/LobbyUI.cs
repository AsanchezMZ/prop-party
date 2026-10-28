// LobbyUI.cs
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;

public class LobbyUI : MonoBehaviour
{
    [Header("Paneles")]
    [SerializeField] GameObject menuPanel;
    [SerializeField] GameObject lobbyPanel;

    [Header("Menú")]
    [SerializeField] TMP_InputField nameInput;
    [SerializeField] TMP_InputField ipInput;
    [SerializeField] Button hostBtn;
    [SerializeField] Button joinBtn;
    [SerializeField] TMP_Text errorText;

    [Header("Lobby")]
    [SerializeField] Button startBtn;
    [SerializeField] Transform listRoot;
    [SerializeField] TMP_Text rowPrefab;
    [SerializeField] LobbyManager lobby;

    [Header("Red")]
    [SerializeField] ushort port = 7777;

    void Start()
    {
        hostBtn.onClick.AddListener(OnHost);
        joinBtn.onClick.AddListener(OnJoin);
        startBtn.onClick.AddListener(() => lobby.StartGame());
        lobby.Changed += Refresh;

        var nm = NetworkManager.Singleton;
        nm.OnClientConnectedCallback += OnClientConnected;
        nm.OnClientDisconnectCallback += OnClientDisconnected;

        ShowMenu();
    }

    void OnDestroy()
    {
        if (lobby != null) lobby.Changed -= Refresh;

        var nm = NetworkManager.Singleton;
        if (nm == null) return;
        nm.OnClientConnectedCallback -= OnClientConnected;
        nm.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    // ---------- Botones ----------

    void OnHost()
    {
        var nm = NetworkManager.Singleton;
        if (nm.IsListening) return;

        LocalProfile.PlayerName = string.IsNullOrWhiteSpace(nameInput.text) ? "Host" : nameInput.text.Trim();
        errorText.text = "";

        var utp = nm.GetComponent<UnityTransport>();
        // Escucha en todas las interfaces para que otros puedan conectar
        utp.SetConnectionData("127.0.0.1", port, "0.0.0.0");

        SetButtonsInteractable(false);
        if (!nm.StartHost())
        {
            errorText.text = "No se pudo iniciar el host";
            SetButtonsInteractable(true);
        }
    }

    void OnJoin()
    {
        var nm = NetworkManager.Singleton;
        if (nm.IsListening) return;

        LocalProfile.PlayerName = string.IsNullOrWhiteSpace(nameInput.text) ? "Guest" : nameInput.text.Trim();
        errorText.text = "";

        string ip = string.IsNullOrWhiteSpace(ipInput.text) ? "127.0.0.1" : ipInput.text.Trim();
        var utp = nm.GetComponent<UnityTransport>();
        utp.SetConnectionData(ip, port);

        SetButtonsInteractable(false);
        if (!nm.StartClient())
        {
            errorText.text = "No se pudo iniciar el cliente";
            SetButtonsInteractable(true);
        }
    }

    // ---------- Eventos de red ----------

    void OnClientConnected(ulong id)
    {
        // Solo nos interesa NUESTRA propia conexión
        if (id == NetworkManager.Singleton.LocalClientId)
            ShowLobby();
    }

    void OnClientDisconnected(ulong id)
    {
        var nm = NetworkManager.Singleton;

        // Si se va otro cliente, no tocamos la UI (la lista se actualiza sola)
        if (id != nm.LocalClientId) return;

        string reason = nm.DisconnectReason;
        errorText.text = string.IsNullOrEmpty(reason) ? "Desconectado" : reason;

        if (nm.IsListening) nm.Shutdown();   // deja el transporte limpio
        ShowMenu();
    }

    // ---------- UI ----------

    void ShowMenu()
    {
        menuPanel.SetActive(true);
        lobbyPanel.SetActive(false);
        SetButtonsInteractable(true);
    }

    void ShowLobby()
    {
        menuPanel.SetActive(false);
        lobbyPanel.SetActive(true);
        startBtn.gameObject.SetActive(NetworkManager.Singleton.IsHost);
        Refresh();
    }

    void SetButtonsInteractable(bool value)
    {
        hostBtn.interactable = value;
        joinBtn.interactable = value;
    }

    void Refresh()
    {
        foreach (Transform c in listRoot) Destroy(c.gameObject);

        foreach (var p in lobby.Players)
        {
            var row = Instantiate(rowPrefab, listRoot);
            bool isHost = p.ClientId == NetworkManager.ServerClientId;
            row.text = p.Name.ToString() + (isHost ? "  (Host)" : "");
        }
    }
}