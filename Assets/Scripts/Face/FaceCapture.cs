using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class FaceCapture : MonoBehaviour
{
    [SerializeField] FaceConfig config;
    [Tooltip("Optional transparent framing guide. Falls back to FaceConfig.alphaMask.")]
    [SerializeField] Texture2D guideTexture;
    public event Action Closed;
    public bool IsOpen { get; private set; }
    VisualElement overlay, placeholder;
    Image preview, guide;
    Label status;
    DropdownField devices;
    Button enableCamera, snap;
    WebCamTexture cam;
    Texture2D result;
    Coroutine starting;

    public void Bind(VisualElement root)
    {
        overlay = root.Q("face-overlay"); placeholder = root.Q("face-placeholder");
        preview = root.Q<Image>("face-preview"); guide = root.Q<Image>("face-guide");
        status = root.Q<Label>("face-status"); devices = root.Q<DropdownField>("face-device");
        enableCamera = root.Q<Button>("face-enable"); snap = root.Q<Button>("face-snap");
        enableCamera.clicked += StartCamera;
        snap.clicked += Snap;
        root.Q<Button>("face-back").clicked += () => { PartyAudio.Instance?.Play(PartyAudio.Settings?.uiBack); Close(); };
        devices.RegisterValueChangedCallback(_ =>
        {
            StopCamera(); ShowSaved();
            status.text = "Pulsa «Activar cámara» para usar la webcam seleccionada.";
        });
        guide.image = guideTexture != null ? guideTexture : config != null ? config.alphaMask : null;
    }

    public void Open()
    {
        IsOpen = true;
        overlay.style.display = DisplayStyle.Flex;
        status.text = config != null ? "Tu foto anterior se conserva hasta hacer una nueva." : "Falta asignar FaceConfig en el Inspector.";
        var names = new List<string>();
        foreach (var device in WebCamTexture.devices) names.Add(device.name);
        devices.choices = names;
        devices.SetValueWithoutNotify(names.Count > 0 ? names[0] : "Sin cámara");
        devices.SetEnabled(names.Count > 0);
        enableCamera.SetEnabled(config != null); // Permission may reveal devices not yet listed.
        snap.SetEnabled(false);
        LocalProfile.LoadSavedFace();
        ShowSaved();
        enableCamera.Focus();
    }

    void ShowSaved()
    {
        if (result != null) Destroy(result);
        result = config != null && LocalProfile.FaceJpg != null ? config.Compose(LocalProfile.FaceJpg) : null;
        preview.image = result;
        preview.uv = new Rect(0, 0, 1, 1);
        preview.style.rotate = new Rotate(0);
        placeholder.style.display = result == null ? DisplayStyle.Flex : DisplayStyle.None;
        guide.style.display = DisplayStyle.None;
    }

    void StartCamera()
    {
        if (starting != null || config == null) return;
        starting = StartCoroutine(StartCameraRoutine());
    }

    IEnumerator StartCameraRoutine()
    {
        enableCamera.SetEnabled(false); snap.SetEnabled(false); devices.SetEnabled(false);
        status.text = "Esperando permiso y señal de la cámara…";
        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        if (!IsOpen) yield break;
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        { CameraFailed("Permiso denegado. Habilita la cámara en los ajustes del sistema."); yield break; }
        var available = WebCamTexture.devices;
        if (available.Length == 0) { CameraFailed("No se ha encontrado ninguna webcam."); yield break; }
        string selected = available[0].name;
        foreach (var device in available) if (device.name == devices.value) selected = device.name;
        var names = new List<string>();
        foreach (var device in available) names.Add(device.name);
        devices.choices = names; devices.SetValueWithoutNotify(selected);
        try
        {
            if (cam != null) { cam.Stop(); Destroy(cam); }
            cam = new WebCamTexture(selected);
            cam.Play();
        }
        catch (Exception exception)
        { Debug.LogWarning(exception.Message, this); CameraFailed("No se puede abrir la webcam. Comprueba si otra aplicación la está usando."); yield break; }
        float elapsed = 0;
        while (IsOpen && (cam.width <= 16 || !cam.didUpdateThisFrame) && elapsed < 5)
        { elapsed += Time.unscaledDeltaTime; yield return null; }
        if (!IsOpen) yield break;
        if (cam.width <= 16 || !cam.didUpdateThisFrame)
        { CameraFailed("La webcam no responde. Prueba otra cámara o cierra las aplicaciones que la usan."); yield break; }
        starting = null;
        preview.image = cam;
        placeholder.style.display = DisplayStyle.None;
        guide.style.display = guide.image != null ? DisplayStyle.Flex : DisplayStyle.None;
        devices.SetEnabled(true); enableCamera.SetEnabled(true); snap.SetEnabled(true);
        status.text = "Centra tu cara en la silueta. La foto no se guarda hasta pulsar el botón.";
        PartyAudio.Instance?.Play(PartyAudio.Settings?.cameraOn);
    }

    void Update()
    {
        if (!IsOpen || cam == null || !cam.isPlaying || preview.image != cam) return;
        preview.uv = Crop(cam.width, cam.height, cam.videoVerticallyMirrored);
        preview.style.rotate = new Rotate(-cam.videoRotationAngle);
    }

    public static Rect Crop(int width, int height, bool verticallyMirrored)
    {
        float x = (float)Mathf.Min(width, height) / Mathf.Max(1, width);
        float y = (float)Mathf.Min(width, height) / Mathf.Max(1, height);
        return new Rect((1 - x) * 0.5f, verticallyMirrored ? (1 + y) * 0.5f : (1 - y) * 0.5f, x, verticallyMirrored ? -y : y);
    }

    void Snap()
    {
        if (cam == null || !cam.isPlaying || cam.width <= 16 || starting != null || config == null || config.size <= 0 || config.size > 1024) return;
        Texture2D raw = null, cropped = null;
        RenderTexture target = null;
        var previous = RenderTexture.active;
        try
        {
            raw = new Texture2D(cam.width, cam.height, TextureFormat.RGB24, false);
            raw.SetPixels32(cam.GetPixels32()); raw.Apply();
            var crop = Crop(cam.width, cam.height, cam.videoVerticallyMirrored);
            target = RenderTexture.GetTemporary(config.size, config.size, 0);
            Graphics.Blit(raw, target, crop.size, crop.position);
            RenderTexture.active = target;
            cropped = new Texture2D(config.size, config.size, TextureFormat.RGB24, false);
            cropped.ReadPixels(new Rect(0, 0, config.size, config.size), 0, 0); cropped.Apply();
            // Match the live preview, including webcams reporting portrait/rotated sensor frames.
            int turns = ((cam.videoRotationAngle / 90) % 4 + 4) % 4;
            if (turns != 0)
            {
                var input = cropped.GetPixels32(); var output = new Color32[input.Length]; int n = config.size;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        int rx = x, ry = y;
                        for (int i = 0; i < turns; i++) { int oldX = rx; rx = ry; ry = n - 1 - oldX; }
                        output[ry * n + rx] = input[y * n + x];
                    }
                cropped.SetPixels32(output); cropped.Apply();
            }
            byte[] jpg = cropped.EncodeToJPG(75);
            if (jpg.Length > PlayerFace.MaxPhotoBytes) { status.text = "La foto es demasiado grande. Reduce el tamaño en FaceConfig."; return; }
            var composed = config.Compose(jpg);
            if (composed == null) { status.text = "No se pudo preparar la foto. Revisa FaceConfig."; return; }
            Destroy(composed);
            bool saved = LocalProfile.SaveFace(jpg);
            PartyAudio.Instance?.Play(PartyAudio.Settings?.photoCaptured);
            StopCamera(); ShowSaved();
            status.text = saved ? "¡Foto guardada! La llevará tu personaje en la próxima ronda." : "Foto disponible para esta sesión, pero no se pudo guardar en disco.";
        }
        catch (Exception exception) { Debug.LogWarning(exception.Message, this); status.text = "No se pudo capturar la foto. La anterior se conserva."; }
        finally
        {
            RenderTexture.active = previous;
            if (target != null) RenderTexture.ReleaseTemporary(target);
            if (raw != null) Destroy(raw);
            if (cropped != null) Destroy(cropped);
        }
    }

    void CameraFailed(string message)
    {
        starting = null;
        StopCamera(); ShowSaved(); status.text = message;
    }

    void StopCamera()
    {
        if (starting != null) { StopCoroutine(starting); starting = null; }
        if (cam != null) { cam.Stop(); Destroy(cam); cam = null; }
        if (snap != null) snap.SetEnabled(false);
        if (enableCamera != null) enableCamera.SetEnabled(config != null);
        if (devices != null) devices.SetEnabled(devices.choices.Count > 0);
        if (guide != null) guide.style.display = DisplayStyle.None;
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false; StopCamera();
        overlay.style.display = DisplayStyle.None;
        Closed?.Invoke();
    }

    void OnDisable() => Close();
    void OnDestroy()
    {
        StopCamera();
        if (result != null) Destroy(result);
    }
}
