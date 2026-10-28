// FaceConfig.cs
using UnityEngine;

[CreateAssetMenu(menuName = "Face/Face Config")]
public class FaceConfig : ScriptableObject
{
    public Texture2D alphaMask;     // blanco = foto, negro = textura original
    public Texture2D baseTexture;   // textura original de la zona de la cara (512x512)
    public Color32 fallbackColor = new Color32(200, 170, 150, 255); // si no hay baseTexture
    public int size = 512;

    // Devuelve la textura final: foto recortada sobre la textura base
    public Texture2D Compose(byte[] jpg)
    {
        var photo = new Texture2D(2, 2, TextureFormat.RGB24, false);
        if (!photo.LoadImage(jpg) || photo.width != size || photo.height != size)
        {
            Object.Destroy(photo);
            return null;
        }

        var p = photo.GetPixels32();
        var m = alphaMask.GetPixels32();
        var b = baseTexture != null ? baseTexture.GetPixels32() : null;

        for (int i = 0; i < p.Length; i++)
        {
            Color32 under = b != null ? b[i] : fallbackColor;
            float a = m[i].r / 255f;
            p[i] = Color32.Lerp(under, p[i], a);
        }

        var result = new Texture2D(size, size, TextureFormat.RGB24, false);
        result.SetPixels32(p);
        result.Apply();
        Object.Destroy(photo);
        return result;
    }
}