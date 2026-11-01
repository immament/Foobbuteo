// SPDX-License-Identifier: AGPL-3.0-only
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Imma.Foobbuteo.Server;
using Imma.Foobbuteo.Server.Game.Model;
using Imma.Foobbuteo.Server.Repos;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using Imma.Foobbuteo.Shared.Lobby;
using NUnit.Framework;
using Tests.Editor.Utils;

namespace Tests.Editor.Server
{
    [Category("FoobbuteoSkills")]
    public class FoobbuteoSkillsTests
    {
        private static readonly string[] SkillColumns =
        {
            "move_skill",
            "confrontation_skill",
            "low_pass_skill",
            "long_pass_skill",
            "gk_pass_skill",
            "crosses_skill",
            "shooting_skill",
            "long_shots_skill",
            "gk_saves_skill",
            "out_of_area_skill",
        };

        [Test]
        public void SqlRow_WithOnlyFoobbuteoColumns_PreservesEveryRatingThroughServerState()
        {
            using var table = PlayerRow();
            using var reader = table.CreateDataReader();
            reader.Read();
            var contract = SqlGamesRepository.ReadPlayerContract(reader);
            var player = ServerPlayerState.FromContract(FoobbuteoSkillTestData.GameId, contract);

            CollectionAssert.AreEqual(
                FoobbuteoSkillTestData.Skills(FoobbuteoSkillTestData.Player()),
                FoobbuteoSkillTestData.Skills(player.ToContract()),
                "SQL loading and server state export must retain every direct skill without Footstar columns."
            );
        }

        [TestCaseSource(nameof(SkillColumns))]
        public void SqlRow_MissingRequiredRating_IsRejected(string missingColumn)
        {
            using var table = PlayerRow();
            table.Columns.Remove(missingColumn);
            using var reader = table.CreateDataReader();
            reader.Read();

            Assert.Catch(
                () => SqlGamesRepository.ReadPlayerContract(reader),
                "Missing " + missingColumn + " must fail instead of silently using a legacy field or zero."
            );
        }

        [TestCaseSource(nameof(SkillColumns))]
        public void SqlRow_NullRating_IsRejected(string nullColumn)
        {
            using var table = PlayerRow();
            table.Rows[0][nullColumn] = System.DBNull.Value;
            using var reader = table.CreateDataReader();
            reader.Read();

            Assert.Catch(
                () => SqlGamesRepository.ReadPlayerContract(reader),
                "Null " + nullColumn + " must fail rather than silently supplying a playable rating."
            );
        }

        [Test]
        public async Task SavedLineup_MovingReserveToGoalkeeper_PreservesEveryRatingOnReload()
        {
            var team = new TeamKey(SourceGame.Footstar, TeamType.Club, 224);
            var expected = FoobbuteoSkillTestData.Player();
            expected.PlayerId = 112;
            var roster = new MockTeamRoster
            {
                Team = team,
                Players = Enumerable
                    .Range(1, 11)
                    .Select(index => new GameDetails_PlayerContract
                    {
                        PlayerId = 100 + index,
                        Name = "Starter " + index,
                    })
                    .ToArray(),
                Reserves = new[] { expected },
            };
            var manager = new ServerUser(
                101,
                8101,
                0,
                "Manager",
                UserRoles.Player,
                teams: new[] { new ManagedTeam(team, "Team") }
            );
            using var repository = new MockGamesRepository(
                () => new List<ServerGameState>(),
                rosters: new[] { roster }
            );
            int gameId = await repository.CreateGameAsync(
                new NewGameOptions(gameType: FoobbuteoGameType.MvM, team: team),
                manager
            );
            var original = roster.Players.Select(player => player.PlayerId).ToArray();
            var desired = original.ToArray();
            desired[0] = expected.PlayerId;

            await repository.SaveMvMLineupAsync(gameId, TeamIndex.Home, manager.UserId, original, desired);

            var game = (await repository.GetGameListAsync(240, 0)).Single();
            var goalkeeper = game.HomeTeam.GetGk();
            var squad = await repository.LoadMvMSquadAsync(gameId, TeamIndex.Home);
            Assert.AreEqual(
                expected.PlayerId,
                goalkeeper.PlayerId,
                "The saved reserve must occupy the goalkeeper slot after reload."
            );
            CollectionAssert.AreEqual(
                FoobbuteoSkillTestData.Skills(expected),
                FoobbuteoSkillTestData.Skills(goalkeeper.ToContract()),
                "Server lineup persistence must carry every reserve rating into the goalkeeper state."
            );
            CollectionAssert.AreEqual(
                FoobbuteoSkillTestData.Skills(expected),
                FoobbuteoSkillTestData.Skills(squad.Single(player => player.PlayerId == expected.PlayerId)),
                "The immutable squad snapshot must retain every rating after saving the active lineup."
            );
        }

        private static DataTable PlayerRow()
        {
            var table = new DataTable();
            table.Columns.Add("team_index", typeof(byte));
            table.Columns.Add("player_index", typeof(byte));
            table.Columns.Add("player_id", typeof(int));
            table.Columns.Add("player_name", typeof(string));
            table.Columns.Add("country_id", typeof(int));
            table.Columns.Add("player_level", typeof(int));
            table.Columns.Add("position_x", typeof(int));
            table.Columns.Add("position_y", typeof(int));
            table.Columns.Add("has_ball", typeof(bool));
            table.Columns.Add("user_id", typeof(int));
            table.Columns.Add("player_mode", typeof(byte));
            table.Columns.Add("action_points", typeof(int));
            foreach (var column in SkillColumns)
                table.Columns.Add(column, typeof(int));
            table.Rows.Add(
                (byte)0,
                (byte)2,
                101,
                "Skill test player",
                1,
                5,
                300,
                210,
                false,
                0,
                (byte)0,
                100,
                2,
                3,
                4,
                5,
                6,
                7,
                8,
                9,
                10,
                11
            );
            return table;
        }
    }
}
