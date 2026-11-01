// LobbyPlayer.cs
using System;
using Unity.Collections;
using Unity.Netcode;

public struct LobbyPlayer : INetworkSerializable, IEquatable<LobbyPlayer>
{
    public ulong ClientId;
    public FixedString32Bytes Name;
    public bool Ready;

    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        s.SerializeValue(ref ClientId);
        s.SerializeValue(ref Name);
        s.SerializeValue(ref Ready);
    }

    public bool Equals(LobbyPlayer o) => ClientId == o.ClientId && Name == o.Name && Ready == o.Ready;
}
