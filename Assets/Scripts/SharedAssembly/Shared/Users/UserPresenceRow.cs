// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Unity.Netcode;

namespace Imma.Foobbuteo.Shared.Users
{
    public struct UserPresenceRow : INetworkSerializable
    {
        public bool IsConnected;
        public ulong ClientId;
        public int UserId;
        public int PlayerId;
        public string PlayerName;
        public int InRoomId;
        public bool HasGameSlot;
        public GameSlot GameSlot;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref IsConnected);
            if (IsConnected)
                serializer.SerializeValue(ref ClientId);
            else if (serializer.IsReader)
                ClientId = default;
            serializer.SerializeValue(ref UserId);
            serializer.SerializeValue(ref PlayerId);
            serializer.SerializeValue(ref PlayerName);
            serializer.SerializeValue(ref InRoomId);
            serializer.SerializeValue(ref HasGameSlot);
            if (HasGameSlot)
                GameSlot.NetworkSerialize(serializer);
            else if (serializer.IsReader)
                GameSlot = default;
        }
    }
}
