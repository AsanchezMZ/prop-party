// LobbyPlayer.cs
using System;
using Unity.Collections;
using Unity.Netcode;

public struct LobbyPlayer : INetworkSerializable, IEquatable<LobbyPlayer>
{
    public ulong ClientId;
    public FixedString32Bytes Name;

    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        s.SerializeValue(ref ClientId);
        s.SerializeValue(ref Name);
    }

    public bool Equals(LobbyPlayer o) => ClientId == o.ClientId && Name == o.Name;
}