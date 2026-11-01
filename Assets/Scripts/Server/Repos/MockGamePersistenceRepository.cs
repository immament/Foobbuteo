// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System.Threading.Tasks;
using Imma.Common;
using Imma.Foobbuteo.Server.Game.Persistence;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;

namespace Imma.Foobbuteo.Server.Repos
{
    internal sealed class MockGamePersistenceRepository : IGamePersistenceRepository
    {
        public Task SavePlayerCommandAsync(int gameId, PlayerCommand command, bool hadBall)
        {
            this.LDebug(() =>
                $"++ #{gameId}, player: {command.PlayerIdx}, team: {command.TeamIdx}, turn: {command.Turn}, action: {command.Action}, hadBall: {hadBall}"
            );
            return Task.CompletedTask;
        }

        public Task SavePlayerModeAsync(int gameId, PlayerModeCommand command)
        {
            this.LDebug(() =>
                $"++ #{gameId}, player: {command.PlayerIdx}, team: {command.TeamIdx}, turn: {command.Turn}, mode: {command.Mode},"
            );
            return Task.CompletedTask;
        }

        public Task<GameDetails_PlayerContract?> SavePlayerClaimAsync(
            int gameId,
            PlayerKey playerKey,
            ServerUser user,
            bool isCaptain
        )
        {
            this.LDebug(() =>
                $"++ #{gameId}, player: {playerKey}, userId: @{user.UserId}, clientId: ^{user.ClientId}, isCaptain: {isCaptain}"
            );
            return Task.FromResult<GameDetails_PlayerContract?>(null);
        }

        public Task SaveTurnAsync(TurnPersistenceRequest request)
        {
            this.LDebug(() => $"++ #{request.GameId}, turn: {request.Turn}");
            return Task.CompletedTask;
        }

        public Task SaveTeamInfoAsync(
            int gameId,
            TeamIndex teamIndex,
            int captainUserId,
            int captainPlayerId,
            string captainName
        )
        {
            this.LDebug(() =>
                $"++ #{gameId}, team: {teamIndex}, captain user: @{captainUserId}, player: {captainPlayerId}, captainName: {captainName}"
            );
            return Task.CompletedTask;
        }

        public void Dispose() { }
    }
}
