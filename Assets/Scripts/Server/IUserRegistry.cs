// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;

namespace Imma.Foobbuteo.Server
{
    internal interface IUserRegistry : IDisposable
    {
        event Action<int> ConnectionChanged;
        bool TryGetUser(ulong clientId, out ServerUser user);
        bool TryGetUser(ulong clientId, out ServerUser user, out string reason);
        bool TryGetUser(int userId, out ServerUser user);

        ServerUser GetUser(ulong clientId);
        List<ServerUser> GetUsersInRoom(int gameId);
        IReadOnlyCollection<ServerUser> GetUsers();

        string RegisterUser(ServerUser user, bool validateClientId = true, bool dev_forceNew = false);

        string RegisterUser(
            ServerUser user,
            out ulong? replacedClientId,
            bool validateClientId = true,
            bool dev_forceNew = false
        );

        void UnregisterUserOnDisconnect(ulong clientId);
    }
}
