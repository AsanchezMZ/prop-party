using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Local resource preparation; NGO still owns scene loading and synchronization.
public class PartyLoadingScreen : MonoBehaviour
{
    public static bool Ready { get; private set; }
    static PartyLoadingScreen instance;
    UIDocument document;
    Label status, mascot;
    ProgressBar progress;
    Button retry;
    Coroutine preparation;
    NetworkSceneManager networkScenes;
    string menuScene;
    readonly List<Texture2D> requested = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { instance = null; Ready = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (instance != null || Application.isBatchMode) { Ready = true; return; }
        instance = new GameObject("Party loading screen").AddComponent<PartyLoadingScreen>();
        DontDestroyOnLoad(instance.gameObject);
        instance.menuScene = SceneManager.GetActiveScene().path;
        SceneManager.sceneLoaded += instance.SceneLoaded;
        instance.document = instance.gameObject.AddComponent<UIDocument>();
        instance.document.sortingOrder = 10000;
        instance.document.visualTreeAsset = Resources.Load<VisualTreeAsset>("LoadingScreen");
        instance.Prepare();
    }

    public static void Show()
    {
        if (instance == null) return;
        Ready = false;
        if (instance.preparation != null) instance.StopCoroutine(instance.preparation);
        instance.preparation = null;
        instance.ClearRequests();
        instance.document.rootVisualElement.style.display = DisplayStyle.Flex;
        if (instance.status != null) instance.status.text = "Cargando el escenario…";
        if (instance.progress != null) instance.progress.value = 0;
        if (instance.retry != null) instance.retry.style.display = DisplayStyle.None;
        instance.document.rootVisualElement.Q<Button>("loading-leave")?.SetEnabled(false);
    }

    public static void Cancel()
    {
        if (instance == null) return;
        Ready = true;
        instance.document.rootVisualElement.style.display = DisplayStyle.None;
    }

    void SceneLoaded(Scene scene, LoadSceneMode mode) => Prepare();

    void Prepare()
    {
        Show();
        preparation = StartCoroutine(PrepareResources());
    }

    IEnumerator PrepareResources()
    {
        // Let scene Awake/Start load their referenced prefabs, sounds and UI first.
        yield return null;
        var source = System.Array.Find(FindObjectsByType<UIDocument>(FindObjectsSortMode.None),
            item => item != document && item.panelSettings != null);
        if (source != null) document.panelSettings = source.panelSettings;
        var root = document.rootVisualElement;
        status = root.Q<Label>("loading-status"); mascot = root.Q<Label>("loading-mascot");
        progress = root.Q<ProgressBar>("loading-progress"); retry = root.Q<Button>("loading-retry");
        if (status == null || source == null)
        { Debug.LogError("Loading screen requires LoadingScreen.uxml and a scene UIDocument with PanelSettings."); yield break; }
        retry.clicked -= Prepare; retry.clicked += Prepare;
        var leave = root.Q<Button>("loading-leave");
        if (leave != null)
        {
            leave.clicked -= ReturnToMenu; leave.clicked += ReturnToMenu;
            leave.SetEnabled(true);
        }
        root.style.display = DisplayStyle.Flex;
        yield return null; // Paint before requesting resource data.

        var clips = Resources.FindObjectsOfTypeAll<AudioClip>();
        float deadline = Time.realtimeSinceStartup + 30;
        for (int i = 0; i < clips.Length; i++)
        {
            var clip = clips[i];
            status.text = "Preparando sonidos…";
            progress.value = 50f * i / Mathf.Max(1, clips.Length);
            // Streaming music stays streamed rather than decoding whole tracks into RAM.
            if (clip.loadType != AudioClipLoadType.Streaming)
            {
                if (clip.loadState == AudioDataLoadState.Unloaded && !clip.LoadAudioData())
                { Fail($"No se pudo cargar el sonido: {clip.name}"); yield break; }
                while (clip.loadState == AudioDataLoadState.Loading && Time.realtimeSinceStartup < deadline) yield return null;
                if (clip.loadState != AudioDataLoadState.Loaded)
                { Fail($"El sonido no está preparado: {clip.name}"); yield break; }
            }
            if (i % 8 == 0) yield return null;
        }

        status.text = "Preparando texturas…";
        foreach (var texture in Resources.FindObjectsOfTypeAll<Texture2D>())
        {
            if (!QualitySettings.streamingMipmapsActive || !texture.streamingMipmaps) continue;
            // Respect Unity's memory budget, not full-resolution mips for every map.
            texture.requestedMipmapLevel = texture.desiredMipmapLevel;
            requested.Add(texture);
        }
        deadline = Time.realtimeSinceStartup + 20;
        while (true)
        {
            int loaded = 0;
            foreach (var texture in requested) if (texture == null || texture.IsRequestedMipmapLevelLoaded()) loaded++;
            progress.value = 50 + 50f * loaded / Mathf.Max(1, requested.Count);
            if (loaded == requested.Count) break;
            if (Time.realtimeSinceStartup >= deadline)
            { Fail("No se pudieron preparar las texturas a tiempo."); yield break; }
            yield return null;
        }
        ClearRequests();
        progress.value = 100;
        status.text = "¡Todo listo!";
        yield return null;
        preparation = null;
        Cancel();
    }

    void Fail(string message)
    {
        ClearRequests();
        Debug.LogError(message);
        status.text = message;
        retry.style.display = DisplayStyle.Flex;
        preparation = null;
    }

    void ClearRequests()
    {
        foreach (var texture in requested) if (texture != null) texture.ClearRequestedMipmapLevel();
        requested.Clear();
    }

    void ReturnToMenu()
    {
        if (!Application.CanStreamedLevelBeLoaded(menuScene)) return;
        var network = NetworkManager.Singleton;
        if (network != null && network.IsListening) network.Shutdown();
        Show();
        StartCoroutine(ReturnAfterShutdown(network));
    }

    IEnumerator ReturnAfterShutdown(NetworkManager network)
    {
        while (network != null && network.ShutdownInProgress) yield return null;
        SceneManager.LoadSceneAsync(menuScene);
    }

    void Update()
    {
        var network = NetworkManager.Singleton;
        var scenes = network != null && network.IsListening ? network.SceneManager : null;
        if (networkScenes != scenes)
        {
            if (networkScenes != null) networkScenes.OnSceneEvent -= SceneEvent;
            networkScenes = scenes;
            if (networkScenes != null) networkScenes.OnSceneEvent += SceneEvent;
        }
        if (mascot != null && !Ready)
        {
            mascot.style.translate = new Translate(0, Mathf.Sin(Time.unscaledTime * 3) * 10);
            mascot.style.rotate = new Rotate(new Angle(Mathf.Sin(Time.unscaledTime * 2) * 9));
        }
        if (!Ready) { UnityEngine.Cursor.lockState = CursorLockMode.None; UnityEngine.Cursor.visible = true; }
    }

    void SceneEvent(SceneEvent sceneEvent)
    {
        if (sceneEvent.SceneEventType == SceneEventType.Load) Show();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
        if (networkScenes != null) networkScenes.OnSceneEvent -= SceneEvent;
        ClearRequests();
        if (instance == this) instance = null;
    }
}
