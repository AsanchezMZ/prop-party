using System;
using Unity.Netcode;
using UnityEngine;

public struct MatchSettings : INetworkSerializable, IEquatable<MatchSettings>
{
    public int Hunters, HidingSeconds, HuntingSeconds, WhistleSeconds, Health;
    public bool ConvertCaptured;

    public static MatchSettings From(MatchRules rules) => new MatchSettings
    {
        Hunters = rules.hunters, HidingSeconds = Mathf.RoundToInt(rules.hidingSeconds),
        HuntingSeconds = Mathf.RoundToInt(rules.huntingSeconds), WhistleSeconds = Mathf.RoundToInt(rules.whistleSeconds),
        Health = rules.propHealth, ConvertCaptured = rules.capturedBecomeHunters
    }.Validated();

    public MatchSettings Validated() => new MatchSettings
    {
        Hunters = Mathf.Clamp(Hunters, 1, 16), HidingSeconds = Mathf.Clamp(HidingSeconds, 0, 120),
        HuntingSeconds = Mathf.Clamp(HuntingSeconds, 30, 900), WhistleSeconds = Mathf.Clamp(WhistleSeconds, 5, 120),
        Health = Mathf.Clamp(Health, 1, 10), ConvertCaptured = ConvertCaptured
    };

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Hunters); serializer.SerializeValue(ref HidingSeconds);
        serializer.SerializeValue(ref HuntingSeconds); serializer.SerializeValue(ref WhistleSeconds);
        serializer.SerializeValue(ref Health); serializer.SerializeValue(ref ConvertCaptured);
    }

    public bool Equals(MatchSettings other) => Hunters == other.Hunters && HidingSeconds == other.HidingSeconds &&
        HuntingSeconds == other.HuntingSeconds && WhistleSeconds == other.WhistleSeconds && Health == other.Health && ConvertCaptured == other.ConvertCaptured;
}
