// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Imma.Foobbuteo.Server.Game.Model;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game.Model;
using Imma.Foobbuteo.Shared.Lobby;

namespace Imma.Foobbuteo.Server.Repos
{
    internal interface IGamesRepository : IDisposable
    {
        Task<int> CreateGameAsync(NewGameOptions options, GameUser creator);
        Task<List<ServerGameState>> GetGameListAsync(int minutesToDisplay = 240, int minId = 0);
        Task<MvMJoinState> JoinMvMGameAsync(int gameId, TeamKey team, GameUser manager);
        Task<Imma.Foobbuteo.Shared.Game.Contract.GameDetails_PlayerContract[]> LoadMvMSquadAsync(
            int gameId,
            TeamIndex team
        );
        Task SaveMvMLineupAsync(int gameId, TeamIndex team, int managerId, int[] expected, int[] desired);
    }
}
