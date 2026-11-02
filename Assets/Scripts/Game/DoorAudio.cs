using Unity.Netcode;
using UnityEngine;

// Hook these server-only methods to the final door/interaction controller, not client input.
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject), typeof(AudioSource))]
public class DoorAudio : NetworkBehaviour
{
    [SerializeField] AudioClip sound;
    AudioSource source;
    int lastCue = -1;
    double lastCueTime = double.NegativeInfinity;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false; source.spatialBlend = 1;
    }

    public void Opened() => Send();
    public void Closed() => Send();
    public void HandleUsed() => Send();
    public void Knocked() => Send();

    void Send()
    {
        if (!IsSpawned || !IsServer) return;
        double now = NetworkManager.ServerTime.Time;
        if (lastCue == 0 && now - lastCueTime < 0.1) return;
        lastCue = 0; lastCueTime = now;
        PlayClientRpc();
    }

    [ClientRpc]
    void PlayClientRpc()
    {
        var config = PartyAudio.Settings;
        var clip = sound != null ? sound : config != null ? config.doorKnock : null;
        if (clip != null) source.PlayOneShot(clip, config != null ? config.effectsVolume : 1);
    }
}
