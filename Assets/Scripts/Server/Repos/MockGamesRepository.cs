// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Imma.Common;
using Imma.Foobbuteo.Server.Game.Model;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using Imma.Foobbuteo.Shared.Lobby;

namespace Imma.Foobbuteo.Server.Repos
{
    internal sealed partial class MockGamesRepository : IGamesRepository
    {
        private const int k_FirstGeneratedGameId = 2001;
        private readonly Func<List<ServerGameState>> m_gamesFactory;
        private readonly Func<DateTime> m_utcNow;
        private readonly List<ServerGameState> m_createdGames = new();
        private readonly object m_gate = new();
        private readonly IReadOnlyList<MockTeamRoster> m_rosters;

        public MockGamesRepository(
            Func<List<ServerGameState>> gamesFactory,
            Func<DateTime> utcNow = null,
            IReadOnlyList<MockTeamRoster> rosters = null
        )
        {
            m_gamesFactory = gamesFactory ?? throw new ArgumentNullException(nameof(gamesFactory));
            m_utcNow = utcNow ?? (() => DateTime.UtcNow);
            m_rosters = rosters ?? Array.Empty<MockTeamRoster>();
        }

        public Task<int> CreateGameAsync(NewGameOptions options, GameUser creator)
        {
            if (creator == null)
                throw new ArgumentNullException(nameof(creator));

            if (!NewGameOptionsRules.TryValidate(options, out string error))
                throw new ArgumentException(error, nameof(options));
            if (options.GameType == FoobbuteoGameType.PvP && creator.PlayerId <= 0)
                throw new ArgumentException("A valid creator player is required.", nameof(creator));

            lock (m_gate)
            {
                List<ServerGameState> seedGames = m_gamesFactory();
                int gameId =
                    seedGames
                        .Concat(m_createdGames)
                        .Select(game => game.GameId)
                        .DefaultIfEmpty(k_FirstGeneratedGameId - 1)
                        .Max() + 1;
                var game = ServerGameState.CreateNewGame(gameId, options, creator, m_utcNow());
                if (options.GameType == FoobbuteoGameType.MvM)
                {
                    game.SetManagedTeam(
                        CreateManagedTeam(gameId, TeamIndex.Home, options.HomeTeamColor, options.Team, creator)
                    );
                    game.SetManagedTeam(
                        ServerGameState.CreateUnassignedTeam(gameId, TeamIndex.Away, options.AwayTeamColor)
                    );
                }
                m_createdGames.Add(game);
                return Task.FromResult(gameId);
            }
        }

        public Task<List<ServerGameState>> GetGameListAsync(int minutesToDisplay, int minId)
        {
            if (minutesToDisplay < 0)
                throw new ArgumentOutOfRangeException(nameof(minutesToDisplay));

            DateTime earliestStart = m_utcNow().AddMinutes(-minutesToDisplay);
            List<ServerGameState> games;
            lock (m_gate)
            {
                games = m_gamesFactory()
                    .Concat(m_createdGames)
                    .Where(game =>
                        game.GameId > minId && (game.StartDateTime >= earliestStart || game.IsWaitingForOpponent)
                    )
                    .OrderByDescending(game => game.GameId)
                    .ToList();
            }
            return Task.FromResult(games);
        }

        public void Dispose() { }

        public Task<MvMJoinState> JoinMvMGameAsync(int gameId, TeamKey team, GameUser manager)
        {
            lock (m_gate)
            {
                var game =
                    m_createdGames.Concat(m_gamesFactory()).FirstOrDefault(g => g.GameId == gameId)
                    ?? throw new ArgumentException("Game not found.", nameof(gameId));
                if (
                    game.Status == FoobbuteoGameStatus.Pending
                    && game.AwayTeam.Id == manager.UserId
                    && game.AwayTeam.TeamKey == team
                )
                    return Task.FromResult(new MvMJoinState(game.AwayTeam, game.StartDateTime));
                if (!game.CanJoinAsManager(manager, team, out string reason))
                    throw new AppException(reason);
                var away = CreateManagedTeam(gameId, TeamIndex.Away, game.AwayTeam.Color, team, manager);
                game.SetManagedTeam(away);
                game.RescheduleStart(
                    Imma.Foobbuteo.Server.Game.MvMPreparationRules.GetStart(
                        game.StartDateTime,
                        new DateTimeOffset(DateTime.SpecifyKind(m_utcNow(), DateTimeKind.Utc))
                    )
                );
                return Task.FromResult(new MvMJoinState(away, game.StartDateTime));
            }
        }

        private ServerTeamState CreateManagedTeam(
            int gameId,
            TeamIndex side,
            ShirtColorEnum color,
            TeamKey key,
            GameUser manager
        )
        {
            if (!manager.TryGetManagedTeam(key, out var managedTeam))
                throw new ArgumentException("The user does not manage the selected Footstar team.");
            var roster = m_rosters.FirstOrDefault(item => item.Team == key);
            if (roster?.Players == null || roster.Players.Length != 11)
                throw new RepositoryUnavailableException("Configure all 11 players of the selected mock team.");
            var squad = roster.Players.Concat(roster.Reserves ?? Array.Empty<GameDetails_PlayerContract>()).ToArray();
            if (
                squad.Any(player => player.PlayerId <= 0)
                || squad.Select(player => player.PlayerId).Distinct().Count() != squad.Length
            )
                throw new RepositoryUnavailableException("Configure distinct positive IDs for every squad player.");
            var players = new ServerPlayerState[11];
            for (byte index = 1; index <= players.Length; index++)
            {
                var player = roster.Players[index - 1];
                player.TeamIdx = side;
                player.PlayerIdx = index;
                player.UserId = 0;
                player.IsCaptain = false;
                player.IsConnected = false;
                player.IsInactive = false;
                players[index - 1] = ServerPlayerState.FromContract(gameId, player);
            }
            var team = new ServerTeamState(side, manager.UserId, managedTeam.DisplayName, color, players, teamKey: key);
            ServerTeamState.ValidateManagedRoster(gameId, team);
            m_squads[(gameId, side)] = squad;
            return team;
        }
    }
}
