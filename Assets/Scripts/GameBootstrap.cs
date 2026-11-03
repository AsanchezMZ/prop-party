// GameBootstrap.cs  (en el mismo GameObject que el NetworkManager, en la escena Lobby)
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameBootstrap : MonoBehaviour
{
    [SerializeField] NetworkObject playerPrefab;
    [SerializeField] string lobbyScene;
    [SerializeField] int maxPlayers = 4;

    NetworkManager nm;
    public MatchSettings? SessionSettings { get; set; }

    void Start()
    {
        nm = NetworkManager.Singleton;
        nm.ConnectionApprovalCallback = Approve;
        nm.NetworkConfig.ConnectionApproval = true;
        nm.OnServerStarted += OnServerStarted;
    }

    void OnDestroy()
    {
        if (nm == null) return;
        nm.OnServerStarted -= OnServerStarted;
        nm.ConnectionApprovalCallback -= Approve;
        if (nm.SceneManager != null)
            nm.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
    }

    // El SceneManager de NGO se crea de nuevo en cada StartHost,
    // así que hay que suscribirse cada vez que el servidor arranca.
    void OnServerStarted()
    {
        SessionSettings = null;
        nm.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
        nm.SceneManager.OnLoadEventCompleted += OnSceneLoaded;
    }

    // ---------- Aprobación de conexión ----------

    void Approve(NetworkManager.ConnectionApprovalRequest req,
                 NetworkManager.ConnectionApprovalResponse res)
    {
        bool inLobby = SceneManager.GetActiveScene().name == lobbyScene;
        bool hasRoom = nm.ConnectedClientsIds.Count < maxPlayers;

        res.Approved = inLobby && hasRoom;
        res.CreatePlayerObject = false;      // los spawneamos nosotros al cargar el mapa
        res.Pending = false;
        res.Reason = !inLobby ? "La partida ya ha empezado" : "Lobby lleno";
    }

    // ---------- Spawn en el mapa elegido ----------

    void OnSceneLoaded(string scene, LoadSceneMode mode,
                       List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (!nm.IsServer || scene == lobbyScene) return;
        var match = FindFirstObjectByType<PropHuntMatch>();
        if (match == null || match.Map == null || playerPrefab == null)
        { Debug.LogError("Map requires a configured PropHuntMatch and player prefab."); return; }

        foreach (var id in clientsTimedOut)
            Debug.LogWarning($"El cliente {id} no terminó de cargar {scene}");

        // Puntos de spawn del mapa recién cargado, barajados
        var points = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None)
                     .OrderBy(_ => Random.value)
                     .ToList();

        if (points.Count == 0)
            Debug.LogWarning($"El mapa {scene} no tiene SpawnPoints");

        int i = 0;
        foreach (var clientId in clientsCompleted)
        {
            if (!nm.ConnectedClients.ContainsKey(clientId)) continue;
            // Evita duplicar el jugador si ya existe
            if (nm.ConnectedClients.TryGetValue(clientId, out var c) && c.PlayerObject != null)
                continue;

            Vector3 pos;
            Quaternion rot;
            if (points.Count > 0)
            {
                Debug.Log("Spawning player at spawn point " + (i%points.Count) + ". Position: " + points[i%points.Count]);
                var sp = points[i % points.Count];
                pos = GetGroundedSpawnPosition(sp);
                rot = sp.transform.rotation;
            }
            else
            {
                Debug.Log("Spawning player at " + (i * 2f) + ", " + 1f + ", " + 0f);
                pos = new Vector3(i * 2f, 1f, 0f);
                rot = Quaternion.identity;
            }
            i++;

            var obj = Instantiate(playerPrefab, pos, rot);
            obj.SpawnAsPlayerObject(clientId, true);   // true = se destruye con el dueño
        }
        StartCoroutine(BeginWhenPrepared(match));
    }

    System.Collections.IEnumerator BeginWhenPrepared(PropHuntMatch match)
    {
        float deadline = Time.realtimeSinceStartup + 60;
        while (match != null && nm != null && nm.IsServer)
        {
            if (match.Phase.Value != MatchPhase.Waiting) yield break;
            bool enoughPlayers = match.Players.Count >= Mathf.Max(2, match.Rules != null ? match.Rules.minimumPlayers : 2);
            bool ready = enoughPlayers;
            foreach (var player in match.Players) ready &= player.AssetsReady.Value;
            if (ready)
            {
                if (!match.BeginRound()) Debug.LogWarning("Round waiting: check player count, rules and team spawn points.");
                yield break;
            }
            if (Time.realtimeSinceStartup >= deadline)
            {
                if (enoughPlayers) Debug.LogWarning("Round waiting: a client has not prepared its assets. Retry from the HUD when ready.");
                yield break;
            }
            yield return null;
        }
    }

    static Vector3 GetGroundedSpawnPosition(SpawnPoint spawnPoint)
    {
        Vector3 point = spawnPoint.transform.position;
        Vector3 rayOrigin = point + Vector3.up * 1.5f;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 5f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return hit.point + Vector3.up * 0.05f;

        Debug.LogWarning($"El SpawnPoint {spawnPoint.name} no tiene suelo debajo; se usará su posición configurada.");
        return point;
    }
}
