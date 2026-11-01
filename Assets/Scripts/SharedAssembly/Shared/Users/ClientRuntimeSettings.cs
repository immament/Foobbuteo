// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Unity.Netcode;

namespace Imma.Foobbuteo.Shared.Users
{
    public struct ClientRuntimeSettings : INetworkSerializable
    {
        public const int DefaultUserListRefreshIntervalSeconds = 10;
        public int UserListRefreshIntervalSeconds;

        public ClientRuntimeSettings(int userListRefreshIntervalSeconds)
        {
            if (userListRefreshIntervalSeconds <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(userListRefreshIntervalSeconds),
                    "User list refresh interval must be positive."
                );
            UserListRefreshIntervalSeconds = userListRefreshIntervalSeconds;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref UserListRefreshIntervalSeconds);
        }
    }
}
