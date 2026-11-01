// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Foobbuteo.Shared.Users;

namespace Imma.Foobbuteo.Client.Users
{
    public interface IUserPresenceApi
    {
        bool IsReady { get; }
        ClientRuntimeSettings Settings { get; }
        event Action SessionChanged;
        event Action<UserListResponse> UserListReceived;
        event Action<UserDisconnectResult> UserDisconnectCompleted;
        ulong NextRequestId();
        void RequestUsers(ulong requestId, int roomId);
        void DisconnectUser(ulong requestId, int userId, ulong clientId);
        void JoinRoom(int roomId);
    }
}
