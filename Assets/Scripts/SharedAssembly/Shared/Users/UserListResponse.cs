// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Unity.Netcode;

namespace Imma.Foobbuteo.Shared.Users
{
    public struct UserListResponse : INetworkSerializable
    {
        public ulong RequestId;

        // Zero selects the global list; positive IDs select a room roster.
        public int RoomId;
        public UserPresenceRow[] Users;
        public string Error;

        public UserListResponse(ulong requestId, int roomId, UserPresenceRow[] users = null, string error = "")
        {
            RequestId = requestId;
            RoomId = roomId;
            Users = users ?? Array.Empty<UserPresenceRow>();
            Error = error ?? "";
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref RequestId);
            serializer.SerializeValue(ref RoomId);
            serializer.SerializeValue(ref Error);
            int count = Users?.Length ?? 0;
            serializer.SerializeValue(ref count);
            if (serializer.IsReader)
                Users = new UserPresenceRow[count];
            for (int i = 0; i < count; i++)
                Users[i].NetworkSerialize(serializer);
        }
    }
}
