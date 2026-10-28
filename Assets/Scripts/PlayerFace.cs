// PlayerFace.cs
using Unity.Netcode;
using UnityEngine;

public class PlayerFace : NetworkBehaviour
{
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
        if (jpg == null || jpg.Length == 0 || jpg.Length > 200_000) return; // validación básica
        ApplyFaceClientRpc(jpg);
    }

    [ClientRpc]
    void ApplyFaceClientRpc(byte[] jpg)
    {
        if (tex == null) tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
        tex.LoadImage(jpg);

        var mpb = new MaterialPropertyBlock();
        maskRenderer.GetPropertyBlock(mpb, materialIndex);
        mpb.SetTexture(TexId, tex);
        maskRenderer.SetPropertyBlock(mpb, materialIndex);
    }
}