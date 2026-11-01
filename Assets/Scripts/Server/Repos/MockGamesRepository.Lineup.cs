// SPDX-License-Identifier: AGPL-3.0-only
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Imma.Common;
using Imma.Foobbuteo.Server.Game.Model;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;

namespace Imma.Foobbuteo.Server.Repos
{
    internal sealed partial class MockGamesRepository
    {
        private readonly Dictionary<(int Game, TeamIndex Side), GameDetails_PlayerContract[]> m_squads = new();

        public Task<GameDetails_PlayerContract[]> LoadMvMSquadAsync(int gameId, TeamIndex team)
        {
            lock (m_gate)
            {
                if (!m_squads.TryGetValue((gameId, team), out var squad))
                    throw new AppException("The match squad is unavailable.");
                return Task.FromResult(squad.ToArray());
            }
        }

        public Task SaveMvMLineupAsync(int gameId, TeamIndex team, int managerId, int[] expected, int[] desired)
        {
            lock (m_gate)
            {
                var game = m_createdGames.Concat(m_gamesFactory()).FirstOrDefault(item => item.GameId == gameId);
                if (
                    game == null
                    || game.MatchType != FoobbuteoGameType.MvM
                    || game.Status != FoobbuteoGameStatus.Pending
                )
                    throw new AppException("The match must be pending MvM.");
                var active = team == TeamIndex.Home ? game.HomeTeam : game.AwayTeam;
                if (active.Id != managerId)
                    throw new AppException("Only this team's manager can edit the lineup.");
                if (!m_squads.TryGetValue((gameId, team), out var squad))
                    throw new AppException("The match squad is unavailable.");
                string error = MvMLineupRules.Validate(desired, squad.Select(player => player.PlayerId).ToArray());
                if (error != null)
                    throw new AppException(error);
                var current = active.Players.Select(player => player.PlayerId);
                if (!current.SequenceEqual(desired) && !current.SequenceEqual(expected))
                    throw new AppException("The lineup changed. Review the latest lineup and try again.");
                if (!current.SequenceEqual(desired))
                {
                    var byId = squad.ToDictionary(player => player.PlayerId);
                    var players = desired
                        .Select(
                            (id, index) =>
                                ServerPlayerState.FromContract(
                                    gameId,
                                    MvMLineupRules.Assign(byId[id], team, (byte)(index + 1))
                                )
                        )
                        .ToArray();
                    foreach (var player in players)
                        player.RefreshState(game.BallPosition);
                    game.SetManagedTeam(
                        new ServerTeamState(
                            team,
                            active.Id,
                            active.Name,
                            active.Color,
                            players,
                            active.Score,
                            active.TeamKey
                        )
                    );
                }
                return Task.CompletedTask;
            }
        }
    }
}
