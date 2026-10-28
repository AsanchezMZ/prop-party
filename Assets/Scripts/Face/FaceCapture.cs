// FaceCapture.cs
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FaceCapture : MonoBehaviour
{
    [SerializeField] FaceConfig config;
    [SerializeField] RawImage preview;          // cuadrado (ej. 400x400)
    [SerializeField] GameObject guideOverlay;   // Image con MaskReference, mismo rect que preview
    [SerializeField] Button captureBtn;         // "Capture your face"
    [SerializeField] Button snapBtn;            // "Snap"

    WebCamTexture cam;
    Texture2D result;
    bool starting;

    void Start()
    {
        captureBtn.onClick.AddListener(() => StartCoroutine(StartCam()));
        snapBtn.onClick.AddListener(Snap);

        snapBtn.interactable = false;
        guideOverlay.SetActive(false);
        LocalProfile.LoadSavedFace();
        ShowSavedFace();
    }

    void ShowSavedFace()
    {
        if (LocalProfile.FaceJpg == null) return;

        result = config.Compose(LocalProfile.FaceJpg);
        if (result == null)
        {
            Debug.LogWarning("La foto guardada no tiene un formato compatible y será reemplazada al tomar otra.");
            return;
        }

        preview.uvRect = new Rect(0, 0, 1, 1);
        preview.texture = result;
    }

    IEnumerator StartCam()
    {
        if (starting) yield break;          // ignora clics repetidos
        starting = true;
        captureBtn.interactable = false;
        snapBtn.interactable = false;

        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            Debug.LogWarning("Permiso de cámara denegado");
            EndStart(false);
            yield break;
        }

        if (cam == null)
        {
            var devices = WebCamTexture.devices;
            if (devices.Length == 0)
            {
                Debug.LogWarning("No hay webcams");
                EndStart(false);
                yield break;
            }

            string name = devices[0].name;
            foreach (var d in devices)
                if (d.isFrontFacing) { name = d.name; break; }

            cam = new WebCamTexture(name);   // sin forzar resolución ni fps
        }

        // Vuelve a la cámara en vivo sin descartar la foto guardada hasta tomar otra.
        if (result != null) { Destroy(result); result = null; }

        preview.texture = cam;
        if (!cam.isPlaying) cam.Play();

        // Esperar imagen real, con timeout de 5 s
        float t = 0f;
        while (cam.width <= 16 && t < 5f) { t += Time.unscaledDeltaTime; yield return null; }

        if (cam.width <= 16)
        {
            Debug.LogWarning("La cámara no ha entregado imagen (¿la usa otra app?)");
            cam.Stop();
            EndStart(false);
            yield break;
        }

        GetCrop(out var scale, out var offset);
        preview.uvRect = new Rect(offset.x, offset.y, scale.x, scale.y);
        guideOverlay.SetActive(true);
        EndStart(true);
    }

    void EndStart(bool ok)
    {
        starting = false;
        captureBtn.interactable = true;
        snapBtn.interactable = ok;
    }

    void GetCrop(out Vector2 scale, out Vector2 offset)
    {
        int w = cam.width, h = cam.height;
        float a = (float)Mathf.Min(w, h) / Mathf.Max(w, h);
        scale = w > h ? new Vector2(a, 1) : new Vector2(1, a);
        offset = (Vector2.one - scale) * 0.5f;
    }

    void Snap()
    {
        if (cam == null || !cam.isPlaying || cam.width <= 16) return;

        GetCrop(out var scale, out var offset);
        int size = config.size;

        var rt = RenderTexture.GetTemporary(size, size, 0);
        Graphics.Blit(cam, rt, scale, offset);

        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var photo = new Texture2D(size, size, TextureFormat.RGB24, false);
        photo.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        photo.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        LocalProfile.SaveFace(photo.EncodeToJPG(85));
        Destroy(photo);
        cam.Stop();

        // Muestra el resultado recortado, tal como lo verán los demás
        result = config.Compose(LocalProfile.FaceJpg);
        preview.uvRect = new Rect(0, 0, 1, 1);
        preview.texture = result;
        guideOverlay.SetActive(false);
        snapBtn.interactable = false;       // se reactiva al pulsar "Capture your face"
    }

    void OnDestroy()
    {
        if (cam != null) cam.Stop();
        if (result != null) Destroy(result);
    }
}