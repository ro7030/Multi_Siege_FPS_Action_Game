using System;
using Unity.Collections;
using Unity.Netcode;

namespace ProjectM.Network
{
    public enum RematchPlayerState : byte
    {
        Pending = 0,
        RetryReady = 1,
        LeftHome = 2
    }

    public struct RematchPlayerEntry : INetworkSerializable, IEquatable<RematchPlayerEntry>
    {
        public ulong OwnerClientId;
        public FixedString64Bytes AuthPlayerId;
        public FixedString64Bytes Nickname;
        public byte State;

        public RematchPlayerState PlayerState => (RematchPlayerState)State;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref OwnerClientId);
            serializer.SerializeValue(ref AuthPlayerId);
            serializer.SerializeValue(ref Nickname);
            serializer.SerializeValue(ref State);
        }

        public bool Equals(RematchPlayerEntry other)
        {
            return OwnerClientId == other.OwnerClientId
                   && AuthPlayerId.Equals(other.AuthPlayerId)
                   && Nickname.Equals(other.Nickname)
                   && State == other.State;
        }
    }

    public struct RematchStatusPayload : INetworkSerializable
    {
        public const int MaxPlayers = 4;

        public int RegisteredCount;
        public int RequiredCount;
        public int PlayerCount;
        public RematchPlayerEntry Player0;
        public RematchPlayerEntry Player1;
        public RematchPlayerEntry Player2;
        public RematchPlayerEntry Player3;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref RegisteredCount);
            serializer.SerializeValue(ref RequiredCount);
            serializer.SerializeValue(ref PlayerCount);
            serializer.SerializeValue(ref Player0);
            serializer.SerializeValue(ref Player1);
            serializer.SerializeValue(ref Player2);
            serializer.SerializeValue(ref Player3);
        }

        public RematchPlayerEntry GetPlayer(int index)
        {
            return index switch
            {
                0 => Player0,
                1 => Player1,
                2 => Player2,
                3 => Player3,
                _ => default
            };
        }

        public void SetPlayer(int index, RematchPlayerEntry entry)
        {
            switch (index)
            {
                case 0: Player0 = entry; break;
                case 1: Player1 = entry; break;
                case 2: Player2 = entry; break;
                case 3: Player3 = entry; break;
            }
        }
    }
}
