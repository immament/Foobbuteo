// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Linq;
using System.Threading.Tasks;
using Imma.Common;
using Imma.Foobbuteo.Common;
using Imma.Foobbuteo.Server.Game.Model;
using Imma.Foobbuteo.Server.Repos;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;

namespace Imma.Foobbuteo.Server.Game
{
    internal partial class FoobbuteoGameEngine
    {
        internal bool TryStartGame(DateTimeOffset now)
        {
            if (!m_persistenceGate.Wait(0))
                return false;
            try
            {
                if (
                    m_gameState.Status != FoobbuteoGameStatus.Pending
                    || !m_gameState.CanStart
                    || now < m_gameState.StartDateTime
                )
                    return false;
                m_gameState.StartGame();
                return true;
            }
            finally
            {
                m_persistenceGate.Release();
            }
        }

        private ServerTeamState GetManagedTeam(ulong clientId)
        {
            var user = m_userRegistry.GetUser(clientId);
            if (m_gameState.MatchType != FoobbuteoGameType.MvM)
                throw new AppException("Lineup preparation is available only in MvM.");
            if (m_gameState.Status != FoobbuteoGameStatus.Pending)
                throw new AppException("The match has already started.");
            if (user.InGameRoomId != GameId || !user.GameSlot.HasValue)
                throw new AppException("Join your assigned game room to edit the lineup.");
            var slot = user.GameSlot.Value;
            if (slot.GameId != GameId || !slot.IsManager)
                throw new AppException("Only an assigned manager can edit the lineup.");
            var team = slot.TeamIdx == TeamIndex.Home ? m_gameState.HomeTeam : m_gameState.AwayTeam;
            if (team.Id != user.UserId)
                throw new AppException("Only this team's manager can edit the lineup.");
            return team;
        }

        public async Task<MvMLineupResult> PrepareMvMLineupAsync(
            ulong clientId,
            int[] expected,
            int[] desired,
            bool save
        )
        {
            await m_persistenceGate.WaitAsync();
            ServerTeamState team = null;
            GameDetails_PlayerContract[] squad = Array.Empty<GameDetails_PlayerContract>();
            try
            {
                team = GetManagedTeam(clientId);
                var repository = ServiceLocator.Current.Get<IGamesRepository>();
                squad = await repository.LoadMvMSquadAsync(GameId, team.TeamIdx);
                // Recheck the sender after asynchronous I/O, including a possible connection takeover.
                team = GetManagedTeam(clientId);
                if (save)
                {
                    string error = MvMLineupRules.Validate(desired, squad.Select(player => player.PlayerId).ToArray());
                    if (error != null)
                        throw new AppException(error);
                    var current = team.Players.Select(player => player.PlayerId).ToArray();
                    if (!current.SequenceEqual(desired))
                    {
                        if (expected == null || !current.SequenceEqual(expected))
                            throw new AppException("The lineup changed. Review the latest lineup and try again.");
                        var byId = squad.ToDictionary(player => player.PlayerId);
                        var players = desired
                            .Select(
                                (id, index) =>
                                    ServerPlayerState.FromContract(
                                        GameId,
                                        MvMLineupRules.Assign(byId[id], team.TeamIdx, (byte)(index + 1))
                                    )
                            )
                            .ToArray();
                        foreach (var player in players)
                            player.RefreshState(m_gameState.BallPosition);
                        var replacement = new ServerTeamState(
                            team.TeamIdx,
                            team.Id,
                            team.Name,
                            team.Color,
                            players,
                            team.Score,
                            team.TeamKey
                        );
                        ServerTeamState.ValidateManagedRoster(GameId, replacement);
                        await repository.SaveMvMLineupAsync(GameId, team.TeamIdx, team.Id, expected, desired);
                        m_gameState.SetManagedTeam(replacement);
                        team = replacement;
                    }
                }
                return Result(true, "");
            }
            catch (AppException exception)
            {
                return Result(false, exception.Message);
            }
            finally
            {
                m_persistenceGate.Release();
            }

            MvMLineupResult Result(bool success, string error) =>
                new()
                {
                    Success = success,
                    Error = error,
                    Squad = squad,
                    Lineup = team?.Players.Select(player => player.PlayerId).ToArray() ?? Array.Empty<int>(),
                };
        }
    }
}
