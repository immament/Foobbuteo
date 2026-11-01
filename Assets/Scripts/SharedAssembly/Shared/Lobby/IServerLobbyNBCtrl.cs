// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Threading.Tasks;
using Imma.Foobbuteo.Shared.Lobby.Contract;
using Imma.Foobbuteo.Shared.Users;

namespace Imma.Foobbuteo.Shared.Lobby
{
    public interface IServerLobbyNBCtrl : IDisposable
    {
        Task InitAsync(LobbyNB lobbyNB);
        bool AddClientToGame(ulong senderClientId, int gameId);
        ServerInfoContract GetServerInfoContract();

        void DeleteGame(ulong clientId, int gameId);

        // void c(ulong clientId, UserSessionData user);
        Task<int> CreateNewGameAsync(ulong clientId, NewGameOptions gameOptions);
        GameUser GetUserInfo(ulong senderClientId);
        void OnClientLobbyReady(ulong clientId);
        bool TryGetUserInfo(ulong clientId, out GameUser user, out string reason);
        UserPresenceRow[] GetUsers(ulong clientId, int roomId, Func<ulong, bool> isConnected);
        void AuthorizeUserDisconnect(
            ulong requesterClientId,
            int targetUserId,
            ulong targetClientId,
            ulong hostClientId,
            Func<ulong, bool> isConnected
        );
    }
}
