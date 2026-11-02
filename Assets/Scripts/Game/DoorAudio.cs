using Unity.Netcode;
using UnityEngine;

// Hook these server-only methods to the final door/interaction controller, not client input.
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject), typeof(AudioSource))]
public class DoorAudio : NetworkBehaviour
{
    [SerializeField] AudioClip openSound, closeSound, handleSound, knockSound;
    AudioSource source;
    int lastCue = -1;
    double lastCueTime = double.NegativeInfinity;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false; source.spatialBlend = 1;
    }

    public void Opened() => Send(0);
    public void Closed() => Send(1);
    public void HandleUsed() => Send(2);
    public void Knocked() => Send(3);

    void Send(int cue)
    {
        if (!IsSpawned || !IsServer) return;
        double now = NetworkManager.ServerTime.Time;
        if (cue == lastCue && now - lastCueTime < 0.1) return;
        lastCue = cue; lastCueTime = now;
        PlayClientRpc(cue);
    }

    [ClientRpc]
    void PlayClientRpc(int cue)
    {
        var config = PartyAudio.Settings;
        var clip = cue == 0 ? openSound : cue == 1 ? closeSound : cue == 2 ? handleSound : knockSound;
        if (clip == null && config != null)
            clip = cue == 0 ? config.doorOpen : cue == 1 ? config.doorClose : cue == 2 ? config.doorHandle : config.doorKnock;
        if (clip != null) source.PlayOneShot(clip, config != null ? config.effectsVolume : 1);
    }
}
