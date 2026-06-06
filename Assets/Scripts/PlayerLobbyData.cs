using Unity.Netcode;
using Unity.Collections;

public struct PlayerLobbyData : INetworkSerializable
{
    public ulong clientId;
    public FixedString32Bytes name;
    public int characterId;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref clientId);
        serializer.SerializeValue(ref name);
        serializer.SerializeValue(ref characterId);
    }
}