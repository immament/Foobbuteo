// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Foobbuteo.Shared.Lobby;
using Imma.Foobbuteo.Shared.Lobby.Contract;

namespace Imma.Foobbuteo.Client.Lobby
{
    public interface ILobbyApiClient : IDisposable
    {
        event Action<ServerInfoContract> ServerInfoUpdated;

        // event Action<GameUser> UserInfoUpdated;
        event Action<string> AlertReceived;
        void SendGameJoinRequest(int gameId);
        void RequestServerInfo();
        void DeleteGame(int gameId);
        event Action<NewGameResult> NewGameCompleted;
        void NewGame(NewGameOptions options);
    }
}
