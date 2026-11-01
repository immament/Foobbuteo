// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Data;
using Imma.Foobbuteo.Server;
using Imma.Foobbuteo.Server.Game.Model;
using Imma.Foobbuteo.Server.Repos;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.Server
{
    public class SqlGameLoadingTests
    {
        private const int GameId = 7601;
        private const int HomeUserId = 8101;
        private const int AwayUserId = 8102;
        private const int HomeTeamId = 224;
        private const int AwayTeamId = 225;
        private static readonly DateTime StartTime = new(2026, 10, 14, 12, 0, 0);

        [TestCase(false)]
        [TestCase(true)]
        public void LoadGameState_PvP_IgnoresSourceTeamFields(bool includeSourceFields)
        {
            using DataTable table = CreateGameRow(FoobbuteoGameType.PvP);
            if (includeSourceFields)
            {
                AddTeamKey(table, "home", HomeTeamId);
                AddTeamKey(table, "away", AwayTeamId);
                table.Rows[0]["home_source_game_id"] = DBNull.Value;
            }

            ServerGameState game = Load(table);

            Assert.AreEqual(
                default(TeamKey),
                game.HomeTeam.TeamKey,
                "PvP must not adopt a managed identity from unrelated SQL fields."
            );
            Assert.AreEqual(
                default(TeamKey),
                game.AwayTeam.TeamKey,
                "Neither PvP side may require or adopt a source-team identity."
            );
            Assert.AreEqual("Home", game.HomeTeam.Name, "PvP must retain the captain's raw display name.");
            Assert.AreEqual(FoobbuteoGameType.PvP, game.MatchType, "The stored mode must select PvP loading.");
            Assert.AreEqual(
                new DateTimeOffset(StartTime, TimeSpan.Zero),
                game.StartDateTime,
                "Loading must preserve the scheduled match time in UTC."
            );
            Assert.AreEqual(TimeSpan.Zero, game.StartDateTime.Offset, "SQL match times must have a UTC offset.");
            Assert.AreEqual(new Vector2(10, 20), game.BallPosition, "Both loaders must preserve ball position.");
            Assert.AreEqual(2, game.HomeTeam.Score, "The common mapping must preserve the home score.");
        }

        [Test]
        public void LoadGameState_MvM_LoadsBothManagedIdentitiesAndFormatsNames()
        {
            using DataTable table = CreateGameRow(FoobbuteoGameType.MvM);
            AddTeamKey(table, "home", HomeTeamId);
            AddTeamKey(table, "away", AwayTeamId, TeamType.NationalTeam, AgeCategory.U18);

            ServerGameState game = Load(table);

            Assert.AreEqual(
                new TeamKey(SourceGame.Footstar, TeamType.Club, HomeTeamId),
                game.HomeTeam.TeamKey,
                "The home MvM side must retain the source club identity."
            );
            Assert.AreEqual(
                new TeamKey(SourceGame.Footstar, TeamType.NationalTeam, AwayTeamId, AgeCategory.U18),
                game.AwayTeam.TeamKey,
                "The away MvM side must retain source, type, ID and age."
            );
            Assert.AreEqual("Home (224)", game.HomeTeam.Name, "Managed club names must include their team ID.");
            Assert.AreEqual("Away U18 (NT)", game.AwayTeam.Name, "Managed national names must include type and age.");
            Assert.AreEqual(HomeUserId, game.HomeTeam.Id, "The manager ID must remain separate from the team ID.");
        }

        [Test]
        public void LoadGameState_MvM_UnassignedOpponentDoesNotReadSourceFields()
        {
            using DataTable table = CreateGameRow(FoobbuteoGameType.MvM);
            AddTeamKey(table, "home", HomeTeamId);
            table.Rows[0]["away_user_id"] = 0;
            table.Rows[0]["away_name"] = DBNull.Value;

            ServerGameState game = Load(table);

            Assert.IsTrue(game.IsWaitingForOpponent, "A pending MvM game with no away manager must wait.");
            Assert.AreEqual(
                default(TeamKey),
                game.AwayTeam.TeamKey,
                "An unassigned MvM side must load without source-team fields."
            );
            Assert.IsFalse(game.CanStart, "Loading an unassigned side must not make the match ready.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LoadGameState_MvM_AssignedTeamWithMissingIdentityFails(bool includeNullSource)
        {
            using DataTable table = CreateGameRow(FoobbuteoGameType.MvM);
            AddTeamKey(table, "away", AwayTeamId);
            if (includeNullSource)
            {
                AddTeamKey(table, "home", HomeTeamId);
                table.Rows[0]["home_source_game_id"] = DBNull.Value;
            }

            Assert.Catch(
                () => Load(table),
                "An assigned MvM side must fail on absent or null identity rather than use a PvP fallback."
            );
        }

        [TestCase(FoobbuteoGameType.PvP)]
        [TestCase(FoobbuteoGameType.MvM)]
        public void ValidateLoadedGame_CompleteRostersAcceptBothModes(FoobbuteoGameType matchType)
        {
            ServerGameState game = CreatePopulatedGame(matchType);

            Assert.DoesNotThrow(
                () => SqlGamesRepository.ValidateLoadedGame(game),
                $"A complete {matchType} match must pass its mode-specific validation."
            );
        }

        [TestCase(FoobbuteoGameType.PvP)]
        [TestCase(FoobbuteoGameType.MvM)]
        public void ValidateLoadedGame_ClaimedPlayerIsOnlyValidForPvP(FoobbuteoGameType matchType)
        {
            ServerGameState game = CreatePopulatedGame(matchType);
            game.HomeTeam.GetPlayerAtIdx(1)
                .InitializeCreator(new ServerUser(101, HomeUserId, HomeTeamId, "Captain", UserRoles.Player, GameId));

            if (matchType == FoobbuteoGameType.MvM)
                Assert.Throws<InvalidOperationException>(
                    () => SqlGamesRepository.ValidateLoadedGame(game),
                    "An MvM roster must reject individual player claims."
                );
            else
                Assert.DoesNotThrow(
                    () => SqlGamesRepository.ValidateLoadedGame(game),
                    "PvP validation must allow individually claimed players without managed-team keys."
                );
        }

        [TestCase(HomeUserId, AwayTeamId)]
        [TestCase(AwayUserId, HomeTeamId)]
        [TestCase(AwayUserId, 0)]
        public void ValidateLoadedGame_MvM_InvalidAwayAssignmentIsRejected(int managerId, int teamId)
        {
            ServerGameState game = CreatePopulatedGame(FoobbuteoGameType.MvM);
            game.SetManagedTeam(
                new ServerTeamState(
                    TeamIndex.Away,
                    managerId,
                    "Away",
                    game.AwayTeam.Color,
                    CreatePlayers(TeamIndex.Away),
                    teamKey: new TeamKey(SourceGame.Footstar, TeamType.Club, teamId)
                )
            );

            Assert.Throws<InvalidOperationException>(
                () => SqlGamesRepository.ValidateLoadedGame(game),
                $"MvM must reject a duplicate manager, duplicate team or unsupported key: manager {managerId}, team {teamId}."
            );
        }

        [Test]
        public void ValidateLoadedGame_MvM_UnassignedOpponentWithPlaceholdersIsAccepted()
        {
            ServerGameState game = CreatePopulatedGame(FoobbuteoGameType.MvM);
            game.SetManagedTeam(ServerGameState.CreateUnassignedTeam(GameId, TeamIndex.Away, game.AwayTeam.Color));

            Assert.DoesNotThrow(
                () => SqlGamesRepository.ValidateLoadedGame(game),
                "An unassigned opponent's placeholders must not require a managed identity or source roster."
            );
        }

        private static ServerGameState CreatePopulatedGame(FoobbuteoGameType matchType)
        {
            using DataTable table = CreateGameRow(matchType);
            if (matchType == FoobbuteoGameType.MvM)
            {
                AddTeamKey(table, "home", HomeTeamId);
                AddTeamKey(table, "away", AwayTeamId);
            }
            ServerGameState game = Load(table);
            foreach (TeamIndex side in new[] { TeamIndex.Home, TeamIndex.Away })
            foreach (ServerPlayerState player in CreatePlayers(side))
                game.LoadPlayer(player);
            return game;
        }

        private static ServerPlayerState[] CreatePlayers(TeamIndex side)
        {
            var players = new ServerPlayerState[11];
            int firstPlayerId = (side == TeamIndex.Home ? HomeTeamId : AwayTeamId) * 100;
            for (byte index = 1; index <= players.Length; index++)
                players[index - 1] = ServerPlayerState.FromContract(
                    GameId,
                    new GameDetails_PlayerContract
                    {
                        TeamIdx = side,
                        PlayerIdx = index,
                        PlayerId = firstPlayerId + index,
                        Name = $"Player {index}",
                    }
                );
            return players;
        }

        private static ServerGameState Load(DataTable table)
        {
            using DataTableReader reader = table.CreateDataReader();
            Assert.IsTrue(reader.Read(), "The test SQL result must contain one match row.");
            return SqlGamesRepository.LoadGameState(reader);
        }

        private static DataTable CreateGameRow(FoobbuteoGameType matchType)
        {
            var table = new DataTable();
            table.Columns.Add("game_id", typeof(int));
            table.Columns.Add("game_type", typeof(byte));
            table.Columns.Add("status_id", typeof(byte));
            table.Columns.Add("current_turn", typeof(int));
            table.Columns.Add("turn_count", typeof(int));
            table.Columns.Add("start_time_utc", typeof(DateTime));
            foreach (string prefix in new[] { "home", "away" })
            {
                table.Columns.Add($"{prefix}_user_id", typeof(int));
                table.Columns.Add($"{prefix}_name", typeof(string));
                table.Columns.Add($"{prefix}_color", typeof(byte));
                table.Columns.Add($"{prefix}_score", typeof(byte));
            }
            table.Columns.Add("ball_x", typeof(int));
            table.Columns.Add("ball_y", typeof(int));
            table.Rows.Add(
                GameId,
                (byte)matchType,
                (byte)FoobbuteoGameStatus.Pending,
                0,
                30,
                StartTime,
                HomeUserId,
                "Home",
                (byte)0,
                (byte)2,
                AwayUserId,
                "Away",
                (byte)1,
                (byte)1,
                10,
                20
            );
            return table;
        }

        private static void AddTeamKey(
            DataTable table,
            string prefix,
            int teamId,
            TeamType teamType = TeamType.Club,
            AgeCategory ageCategory = AgeCategory.Main
        )
        {
            foreach (string field in new[] { "source_game_id", "team_type", "team_id", "age_category" })
                table.Columns.Add($"{prefix}_{field}", typeof(int));
            table.Rows[0][$"{prefix}_source_game_id"] = (int)SourceGame.Footstar;
            table.Rows[0][$"{prefix}_team_type"] = (int)teamType;
            table.Rows[0][$"{prefix}_team_id"] = teamId;
            table.Rows[0][$"{prefix}_age_category"] = (int)ageCategory;
        }
    }
}
