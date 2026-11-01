// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Linq;
using System.Threading.Tasks;
using Imma.Foobbuteo.Server;
using Imma.Foobbuteo.Server.dev;
using Imma.Foobbuteo.Server.Game.Model;
using Imma.Foobbuteo.Server.Repos;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using Imma.Foobbuteo.Shared.Lobby;
using NUnit.Framework;

namespace Tests.Editor
{
    public class MockAuthenticationRepositoryTests
    {
        private static readonly DateTime TestNow = new(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);

        [Test]
        public async Task ValidateAndConsumeTokenAsync_ValidToken_ReturnsUserOnlyOnce()
        {
            using var repository = CreateRepository(TestNow.AddSeconds(50));

            ServerUser firstResult = await repository.ValidateAndConsumeTokenAsync(101, "one-time-token");
            ServerUser secondResult = await repository.ValidateAndConsumeTokenAsync(102, "one-time-token");

            Assert.IsNotNull(firstResult);
            Assert.AreEqual((ulong)101, firstResult.ClientId);
            Assert.AreEqual(201, firstResult.UserId);
            Assert.AreEqual(301, firstResult.PlayerId);
            Assert.IsNull(secondResult);
        }

        [Test]
        public async Task ValidateAndConsumeTokenAsync_ExpiredToken_RejectsAndConsumesToken()
        {
            using var repository = CreateRepository(TestNow.AddSeconds(-1));

            ServerUser expiredResult = await repository.ValidateAndConsumeTokenAsync(101, "one-time-token");

            Assert.IsNull(expiredResult);
        }

        [Test]
        public async Task ValidateAndConsumeTokenAsync_ConcurrentRequests_OnlyOneSucceeds()
        {
            using var repository = CreateRepository(TestNow.AddSeconds(60));
            Task<ServerUser>[] requests = Enumerable
                .Range(1, 16)
                .Select(clientId => repository.ValidateAndConsumeTokenAsync((ulong)clientId, "one-time-token"))
                .ToArray();

            ServerUser[] results = await Task.WhenAll(requests);

            Assert.AreEqual(1, results.Count(user => user != null));
        }

        [Test]
        public async Task ValidateAndConsumeTokenAsync_MissingToken_ReturnsNull()
        {
            using var repository = CreateRepository(TestNow.AddSeconds(60));

            ServerUser result = await repository.ValidateAndConsumeTokenAsync(101, "missing-token");

            Assert.IsNull(result);
        }

        private static MockAuthenticationRepository CreateRepository(DateTime expiresAtUtc) =>
            new(
                new[]
                {
                    new MockUserConfig
                    {
                        Token = "one-time-token",
                        UserId = 201,
                        PlayerId = 301,
                        Name = "Test user",
                        Role = UserRoles.Player,
                        ExpiresAtUtc = expiresAtUtc,
                    },
                },
                () => TestNow
            );
    }

    public class MockGamesRepositoryTests
    {
        [Test]
        public async Task CreateGameAsync_AddsGameThatCanBeLoaded()
        {
            DateTime now = new(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);
            using var repository = new MockGamesRepository(() => new(), () => now);
            var creator = new ServerUser(1, 201, 301, "Creator", UserRoles.Player, 0);

            int gameId = await repository.CreateGameAsync(
                new NewGameOptions(startInMinutes: 3, numberOfTurns: 40),
                creator
            );
            var games = await repository.GetGameListAsync(240, gameId - 1);

            var game = games.Single();
            Assert.AreEqual(gameId, game.GameId, "A created mock game must remain available for reload.");
            Assert.AreEqual(
                new DateTimeOffset(now.AddMinutes(3)),
                game.StartDateTime,
                "The mock must preserve the selected delay as a UTC start time."
            );
            Assert.AreEqual(40, game.NumberOfTurns, "The mock must preserve the selected turn count.");
            Assert.AreEqual(creator.UserId, game.HomeTeam.Id, "The authenticated creator owns the home team.");
            Assert.AreEqual(ShirtColorEnum.Blue, game.HomeTeam.Color, "The home color must match the options.");
            Assert.AreEqual(ShirtColorEnum.Red, game.AwayTeam.Color, "The away color must match the options.");
            Assert.AreEqual(FoobbuteoGameType.PvP, game.MatchType, "The mock must create a PvP game.");
            var creatorPlayer = game.HomeTeam.GetPlayerAtIdx(1);
            Assert.AreEqual(creator.UserId, creatorPlayer.UserId, "The creator must occupy the default GK slot.");
            Assert.AreEqual(creator.PlayerId, creatorPlayer.PlayerId, "The GK must use the creator's player ID.");
            Assert.IsTrue(creatorPlayer.IsGk, "The default slot must remain a goalkeeper.");
            Assert.AreEqual(
                PlayerModeEnum.GkSave,
                creatorPlayer.PlayerMode,
                "Assigning the creator must preserve the goalkeeper's initial mode."
            );
            Assert.IsFalse(creator.GameSlot.HasValue, "Persistence must leave live slot restoration to the join flow.");
            for (byte slot = 1; slot <= NewGameOptionsRules.MaxPlayerPosition; slot++)
            {
                if (slot != 1)
                    Assert.AreEqual(
                        0,
                        game.HomeTeam.GetPlayerAtIdx(slot).UserId,
                        $"Unselected home slot {slot} must remain unclaimed."
                    );
                Assert.AreEqual(
                    0,
                    game.AwayTeam.GetPlayerAtIdx(slot).UserId,
                    $"Away slot {slot} must remain unclaimed."
                );
            }
        }

        [Test]
        public async Task GetGameListAsync_ReturnsFilteredDescendingMockGames()
        {
            var registry = new UserRegistry();
            DateTime now = DateTime.UtcNow;
            using var repository = new MockGamesRepository(
                () => DevServerMockData.ForEditorTest(loadInitialGames: true).CreateStateGameList(registry),
                () => now
            );

            var games = await repository.GetGameListAsync(240, 94001);

            Assert.IsNotEmpty(games);
            Assert.IsTrue(games.All(game => game.GameId > 94001));
            CollectionAssert.AreEqual(
                games.Select(game => game.GameId).OrderByDescending(gameId => gameId),
                games.Select(game => game.GameId)
            );
        }

        [Test]
        public void CreateStateGameList_LoadInitialGamesDisabled_ReturnsEmptyList()
        {
            var games = DevServerMockData
                .ForEditorTest(loadInitialGames: false)
                .CreateStateGameList(new UserRegistry());

            Assert.IsEmpty(games);
        }

        [Test]
        public void CreateStateGameList_LoadInitialGamesEnabled_ReturnsSeedGames()
        {
            var games = DevServerMockData.ForEditorTest(loadInitialGames: true).CreateStateGameList(new UserRegistry());

            Assert.IsNotEmpty(games);
        }

        [Test]
        public void DataAccessConfig_LoadInitialGamesDefaultsToFalse()
        {
            Assert.IsFalse(new DataAccessConfig().LoadInitialGames);
        }
    }

    public class SqlGamesRepositoryTests
    {
        [Test]
        public void CreateGameCommand_UsesProcedureAndSelectedOptionsWithAuthenticatedIdentity()
        {
            const int creatorUserId = 201;
            const int creatorPlayerId = 301;
            var creator = new ServerUser(1, creatorUserId, creatorPlayerId, "Creator", UserRoles.Player, 0);
            var options = new NewGameOptions(15, 60, ShirtColorEnum.Green, ShirtColorEnum.Purple, 11);
            using var repository = new SqlGamesRepository("Server=unused;Database=unused;", 30);
            using var command = repository.CreateGameCommand(options, creator);

            Assert.AreEqual(
                "foobbuteo_api.create_game",
                command.CommandText,
                "Creation must call the existing authoritative stored procedure."
            );
            Assert.AreEqual(
                System.Data.CommandType.StoredProcedure,
                command.CommandType,
                "The game creation command must execute as a stored procedure."
            );
            var expected = new (string Name, System.Data.DbType Type, object Value)[]
            {
                ("@start_in_minutes", System.Data.DbType.Int32, options.StartInMinutes),
                ("@turn_count", System.Data.DbType.Int32, options.NumberOfTurns),
                ("@home_color", System.Data.DbType.Byte, (byte)options.HomeTeamColor),
                ("@away_color", System.Data.DbType.Byte, (byte)options.AwayTeamColor),
                ("@creator_user_id", System.Data.DbType.Int32, creatorUserId),
                ("@creator_player_id", System.Data.DbType.Int32, creatorPlayerId),
                ("@creator_position", System.Data.DbType.Int32, options.PlayerPosition),
                ("@game_type", System.Data.DbType.Int32, (int)options.GameType),
            };
            Assert.AreEqual(
                expected.Length,
                command.Parameters.Count,
                "All eight creation arguments must be supplied explicitly."
            );
            foreach (var parameter in expected)
            {
                Assert.AreEqual(
                    parameter.Type,
                    command.Parameters[parameter.Name].DbType,
                    $"{parameter.Name} must use the SQL type expected by create_game."
                );
                Assert.AreEqual(
                    parameter.Value,
                    command.Parameters[parameter.Name].Value,
                    $"{parameter.Name} must preserve the selection or authenticated creator identity."
                );
            }
        }

        [Test]
        public void ValidateLoadedGame_CompleteTeams_DoesNotThrow()
        {
            ServerGameState game = CreateGame();

            Assert.DoesNotThrow(() => SqlGamesRepository.ValidateLoadedGame(game));
        }

        [Test]
        public void ValidateLoadedGame_MissingPlayer_ThrowsWithGameAndSlotDetails()
        {
            const int gameId = 42;
            ServerPlayerState[] homePlayers = CreatePlayers(gameId, TeamIndex.Home);
            homePlayers[4] = null;
            ServerGameState game = CreateGame(gameId, homePlayers);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                SqlGamesRepository.ValidateLoadedGame(game)
            );

            StringAssert.Contains("Game #42", exception.Message);
            StringAssert.Contains("Home", exception.Message);
            StringAssert.Contains("player #5", exception.Message);
        }

        private static ServerGameState CreateGame(int gameId = 42, ServerPlayerState[] homePlayers = null) =>
            ServerGameState.CreateForTest(
                gameId,
                DateTime.UtcNow,
                new ServerTeamState(
                    TeamIndex.Home,
                    1,
                    "Home",
                    default,
                    homePlayers ?? CreatePlayers(gameId, TeamIndex.Home)
                ),
                new ServerTeamState(TeamIndex.Away, 2, "Away", default, CreatePlayers(gameId, TeamIndex.Away))
            );

        private static ServerPlayerState[] CreatePlayers(int gameId, TeamIndex teamIndex)
        {
            var players = new ServerPlayerState[11];
            for (byte playerIdx = 1; playerIdx <= players.Length; playerIdx++)
                players[playerIdx - 1] = ServerPlayerState.CreateForTest(gameId, teamIndex, playerIdx);
            return players;
        }
    }

    public class GameLoadProgressTests
    {
        [Test]
        public void SuccessfulEmptyQuery_MarksLobbyReady()
        {
            var progress = new GameLoadProgress();

            bool becameReady = progress.RecordSuccessfulQuery(Array.Empty<int>(), true);

            Assert.IsTrue(becameReady);
            Assert.IsTrue(progress.IsLobbyReady);
            Assert.AreEqual(0, progress.LastGameId);
        }

        [Test]
        public void SuccessfulCompleteBatch_AdvancesHighWaterMark()
        {
            var progress = new GameLoadProgress();

            progress.RecordSuccessfulQuery(new[] { 12, 10, 11 }, true);
            bool becameReadyAgain = progress.RecordSuccessfulQuery(new[] { 14, 13 }, true);

            Assert.IsFalse(becameReadyAgain);
            Assert.AreEqual(14, progress.LastGameId);
        }

        [Test]
        public void PartiallyInitializedBatch_DoesNotAdvanceHighWaterMark()
        {
            var progress = new GameLoadProgress();
            progress.RecordSuccessfulQuery(new[] { 10 }, true);

            progress.RecordSuccessfulQuery(new[] { 11, 12 }, false);

            Assert.IsTrue(progress.IsLobbyReady);
            Assert.AreEqual(10, progress.LastGameId);
        }
    }
}
