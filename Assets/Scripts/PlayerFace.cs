// PlayerFace.cs
using Unity.Netcode;
using UnityEngine;

public class PlayerFace : NetworkBehaviour
{
    public const int MaxPhotoBytes = 200_000;
    [SerializeField] FaceConfig config;
    [SerializeField] Renderer maskRenderer;
    [SerializeField] int materialIndex = 0;

    // URP/HDRP: "_BaseMap" | Built-in: "_MainTex"
    static readonly int TexId = Shader.PropertyToID("_BaseMap");

    Texture2D tex;

    public override void OnNetworkSpawn()
    {
        LocalProfile.LoadSavedFace();
        if (IsOwner && LocalProfile.FaceJpg != null)
            SubmitFaceServerRpc(LocalProfile.FaceJpg);
    }

    public override void OnNetworkDespawn()
    {
        if (tex != null) Destroy(tex);
    }

    [ServerRpc]   // solo el dueño puede llamarlo
    void SubmitFaceServerRpc(byte[] jpg)
    {
        if (jpg == null || jpg.Length == 0 || jpg.Length > MaxPhotoBytes) return;
        ApplyFaceClientRpc(jpg);
    }

    [ClientRpc]
    void ApplyFaceClientRpc(byte[] jpg)
    {
        if (config == null || maskRenderer == null || jpg == null || jpg.Length > MaxPhotoBytes ||
            materialIndex < 0 || materialIndex >= maskRenderer.sharedMaterials.Length) return;
        var composed = config.Compose(jpg);
        if (composed == null) return;
        if (tex != null) Destroy(tex);
        tex = composed;

        var mpb = new MaterialPropertyBlock();
        maskRenderer.GetPropertyBlock(mpb, materialIndex);
        mpb.SetTexture(TexId, tex);
        maskRenderer.SetPropertyBlock(mpb, materialIndex);
    }
}
