using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public struct PlayerData : IEquatable<PlayerData>, INetworkSerializable
{
    public ulong clientId;
    public int modelId; // Replaces colorId
    public FixedString64Bytes playerName;
    public FixedString64Bytes playerId;
    public bool isReady;

    public bool Equals(PlayerData other)
    {
        return clientId == other.clientId;
    }


    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref clientId);
        serializer.SerializeValue(ref modelId);
        serializer.SerializeValue(ref playerName);
        serializer.SerializeValue(ref playerId);
        serializer.SerializeValue(ref isReady);
    }
}
