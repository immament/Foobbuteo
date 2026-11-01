// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Threading.Tasks;
using Imma.Foobbuteo.Server.Game.Persistence;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;

namespace Imma.Foobbuteo.Server.Repos
{
    internal interface IGamePersistenceRepository : IDisposable
    {
        Task SavePlayerCommandAsync(int gameId, PlayerCommand command, bool hadBall);
        Task SavePlayerModeAsync(int gameId, PlayerModeCommand command);
        Task<GameDetails_PlayerContract?> SavePlayerClaimAsync(
            int gameId,
            PlayerKey playerKey,
            ServerUser user,
            bool isCaptain
        );
        Task SaveTurnAsync(TurnPersistenceRequest request);
        Task SaveTeamInfoAsync(
            int gameId,
            TeamIndex teamIndex,
            int captainUserId,
            int captainPlayerId,
            string captainName
        );
    }
}
