// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Data;
using Imma.Foobbuteo.Server;
using Imma.Foobbuteo.Server.Repos;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using Imma.Foobbuteo.Shared.Lobby;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;

namespace Tests.Editor.SharedAssembly
{
    [Category("MvM")]
    public class MvMContractsTests
    {
        private const int GameId = 7601;
        private const int TeamId = 224;
        private const int UserId = 8101;
        private const int BufferBytes = 4096;

        [TestCase(TeamIndex.Home)]
        [TestCase(TeamIndex.Away)]
        public void ManagerSlot_RoundTrip_HasNoPitchPlayerKey(TeamIndex side)
        {
            var expected = GameSlot.ForManager(GameId, side);
            using var writer = new FastBufferWriter(BufferBytes, Allocator.Temp);
            writer.WriteNetworkSerializable(expected);
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            reader.ReadNetworkSerializable(out GameSlot actual);

            Assert.AreEqual(expected, actual, "Serialization must preserve the manager's game and side.");
            Assert.AreEqual(expected.GetHashCode(), actual.GetHashCode(), "Equal slots must have equal hashes.");
            Assert.IsTrue(actual.IsManager, "Index zero must identify a manager assignment.");
            Assert.IsFalse(actual.IsPlayer, "A manager must not be treated as a pitch player.");
            Assert.IsFalse(actual.TryGetPlayerKey(out _), "Manager slots must not produce invalid PlayerKeys.");
            Assert.Throws<InvalidOperationException>(
                () => _ = actual.PlayerKey,
                "Unguarded pitch-player access must fail clearly for manager slots."
            );
        }

        [TestCase(1)]
        [TestCase(11)]
        public void PlayerSlot_RoundTrip_PreservesPitchKey(int index)
        {
            var key = new PlayerKey(TeamIndex.Away, (byte)index);
            var slot = GameSlot.ForPlayer(GameId, key);
            Assert.IsTrue(slot.IsPlayer, "Valid pitch indices must identify player assignments.");
            Assert.IsFalse(slot.IsManager, "A pitch player must not be interpreted as a manager.");
            Assert.IsTrue(slot.TryGetPlayerKey(out var actual), "A player slot must expose its pitch key.");
            Assert.AreEqual(key, actual, "The pitch key must preserve side and index.");
            Assert.AreNotEqual(
                GameSlot.ForManager(GameId, TeamIndex.Away),
                slot,
                "A manager and pitch player on the same side are different assignments."
            );
        }

        [TestCase(0, TeamIndex.Home)]
        [TestCase(-1, TeamIndex.Home)]
        [TestCase(GameId, (TeamIndex)2)]
        public void ManagerSlot_InvalidGameOrSide_IsRejected(int gameId, TeamIndex side) =>
            Assert.Throws<ArgumentException>(
                () => GameSlot.ForManager(gameId, side),
                "Manager factories must reject non-positive games and unknown sides."
            );

        [TestCase(1, 0, TeamId, AgeCategory.Main, true)]
        [TestCase(1, 0, TeamId, AgeCategory.U19, true)]
        [TestCase(1, 0, TeamId, AgeCategory.U18, false)]
        [TestCase(1, 1, TeamId, AgeCategory.U21, true)]
        [TestCase(1, 2, TeamId, AgeCategory.U18, true)]
        [TestCase(1, 2, TeamId, AgeCategory.U19, false)]
        [TestCase(2, 0, TeamId, AgeCategory.Main, false)]
        [TestCase(1, 0, 0, AgeCategory.Main, false)]
        [TestCase(1, 1, TeamId, (AgeCategory)22, false)]
        public void TeamKey_Support_RespectsSourceTypeAndAge(
            int source,
            int type,
            int id,
            AgeCategory age,
            bool supported
        )
        {
            var key = new TeamKey((SourceGame)source, (TeamType)type, id, age);
            Assert.AreEqual(
                supported,
                key.IsSupported,
                "Only supported Footstar team/category combinations are eligible."
            );
        }

        [Test]
        public void TeamKey_Equality_UsesEveryIdentityField()
        {
            var key = new TeamKey(SourceGame.Footstar, TeamType.Club, TeamId);
            Assert.AreNotEqual(
                key,
                new TeamKey((SourceGame)2, TeamType.Club, TeamId),
                "Identical external IDs from different source games must remain distinct."
            );
            Assert.AreNotEqual(
                key,
                new TeamKey(SourceGame.Footstar, TeamType.NationalTeam, TeamId),
                "Team type is part of identity."
            );
            Assert.AreNotEqual(
                key,
                new TeamKey(SourceGame.Footstar, TeamType.Club, TeamId + 1),
                "External team ID is part of identity."
            );
            Assert.AreNotEqual(
                key,
                new TeamKey(SourceGame.Footstar, TeamType.Club, TeamId, AgeCategory.U19),
                "Main and youth teams must remain distinct."
            );
        }

        [TestCase(AgeCategory.Main, 0)]
        [TestCase(AgeCategory.U18, 18)]
        [TestCase(AgeCategory.U19, 19)]
        [TestCase(AgeCategory.U21, 21)]
        public void TeamKey_LegacyIntegerPayload_PreservesAgeAndSelectionIdentity(AgeCategory expected, int ageValue)
        {
            using var writer = new FastBufferWriter(BufferBytes, Allocator.Temp);
            writer.WriteValueSafe((int)SourceGame.Footstar);
            writer.WriteValueSafe((int)TeamType.Club);
            writer.WriteValueSafe(TeamId);
            writer.WriteValueSafe(ageValue);
            using var reader = new FastBufferReader(writer, Allocator.Temp);

            reader.ReadNetworkSerializable(out TeamKey actual);

            Assert.AreEqual(
                expected,
                actual.AgeCategory,
                "Existing integer payloads must deserialize to the corresponding age category."
            );
            Assert.AreEqual(
                $"1:0:{TeamId}:{ageValue}",
                actual.ToString(),
                "Changing the field to an enum must preserve numeric team-selection identifiers."
            );
        }

        [TestCase(TeamType.Club, AgeCategory.Main, "My club (224)")]
        [TestCase(TeamType.Club, AgeCategory.U19, "My club U19 (224)")]
        [TestCase(TeamType.NationalTeam, AgeCategory.Main, "My club (NT)")]
        [TestCase(TeamType.NationalTeam, AgeCategory.U18, "My club U18 (NT)")]
        [TestCase(TeamType.CityTeam, AgeCategory.U21, "My club U21 (CT)")]
        public void ManagedTeam_Label_FormatsTypeAndAge(TeamType type, AgeCategory age, string expected)
        {
            var team = new ManagedTeam(new TeamKey(SourceGame.Footstar, type, TeamId, age), "My club");
            Assert.AreEqual(expected, team.DisplayName, "Display labels must omit Main and distinguish team type.");
        }

        [TestCase(TeamType.NationalTeam, AgeCategory.Main, "Portugal (NT)")]
        [TestCase(TeamType.NationalTeam, AgeCategory.U18, "Portugal U18 (NT)")]
        [TestCase(TeamType.NationalTeam, AgeCategory.U21, "Portugal U21 (NT)")]
        [TestCase(TeamType.Club, AgeCategory.Main, "LANG_PORTUGAL (224)")]
        [TestCase(TeamType.CityTeam, AgeCategory.Main, "LANG_PORTUGAL (CT)")]
        public void ManagedTeam_CountryTag_TranslatesOnlyNationalTeamDisplayNames(
            TeamType type,
            AgeCategory age,
            string expected
        )
        {
            const string countryTag = "LANG_PORTUGAL";
            var key = new TeamKey(SourceGame.Footstar, type, TeamId, age);
            var team = new ManagedTeam(key, countryTag);

            string displayName = team.DisplayName;

            Assert.AreEqual(expected, displayName, "Only national-team display names must translate country tags.");
            Assert.AreEqual(countryTag, team.Name, "Formatting a label must preserve the raw country tag.");
            Assert.AreEqual(key, team.Key, "Country translation must preserve team identity.");
        }

        [Test]
        public void ManagedTeam_CountryTag_RoundTrip_PreservesRawNameAndTranslatedLabel()
        {
            const string countryTag = "LANG_PORTUGAL";
            var key = new TeamKey(SourceGame.Footstar, TeamType.NationalTeam, TeamId);
            var team = new ManagedTeam(key, countryTag);
            using var writer = new FastBufferWriter(BufferBytes, Allocator.Temp);

            writer.WriteNetworkSerializable(team);
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            reader.ReadNetworkSerializable(out ManagedTeam actual);

            Assert.AreEqual(countryTag, actual.Name, "Network data must retain the source country tag.");
            Assert.AreEqual(key, actual.Key, "Network data must retain national-team identity.");
            Assert.AreEqual("Portugal (NT)", actual.DisplayName, "Received country tags must display in English.");
        }

        [Test]
        public void AuthenticatedUser_RoundTrip_PreservesTeamsAndManagerSlot()
        {
            var teams = new[]
            {
                new ManagedTeam(new TeamKey(SourceGame.Footstar, TeamType.Club, TeamId), "Club"),
                new ManagedTeam(new TeamKey(SourceGame.Footstar, TeamType.Club, TeamId, AgeCategory.U19), "Club"),
            };
            var expected = new ServerUser(
                101,
                UserId,
                0,
                "Manager",
                UserRoles.Player,
                gameSlot: GameSlot.ForManager(GameId, TeamIndex.Home),
                teams: teams
            );
            using var writer = new FastBufferWriter(BufferBytes, Allocator.Temp);
            writer.WriteNetworkSerializable<GameUser>(expected);
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            reader.ReadNetworkSerializable(out GameUser actual);

            CollectionAssert.AreEqual(teams, actual.Teams, "User snapshots must preserve every eligible variant.");
            Assert.AreEqual(
                expected.GameSlot.Value,
                actual.GameSlot.Value,
                "User snapshots must preserve manager slots."
            );
        }

        [Test]
        public void GameTeamAndOptions_RoundTrip_PreserveFullKey()
        {
            var key = new TeamKey(SourceGame.Footstar, TeamType.NationalTeam, TeamId, AgeCategory.U18);
            var info = new NetFoobbuteoTeam
            {
                Id = UserId,
                Name = "Poland U18 (NT)",
                Team = key,
            };
            using var writer = new FastBufferWriter(BufferBytes, Allocator.Temp);
            writer.WriteNetworkSerializable(info);
            writer.WriteValueSafe(new NewGameOptions(gameType: FoobbuteoGameType.MvM, team: key));
            writer.WriteNetworkSerializable(
                new GameDetails_TeamContract(UserId, Array.Empty<GameDetails_PlayerContract>(), 0, key)
            );
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            reader.ReadNetworkSerializable(out NetFoobbuteoTeam actual);
            reader.ReadValueSafe(out NewGameOptions options);
            reader.ReadNetworkSerializable(out GameDetails_TeamContract details);

            Assert.AreEqual(info, actual, "Team network state must preserve owner and complete source-team identity.");
            Assert.AreEqual(key, options.Team, "Creation options must carry all four team identity fields.");
            Assert.AreEqual(key, details.Team, "Roster snapshots must retain source-team identity.");
            Assert.AreEqual(FoobbuteoGameType.MvM, options.GameType, "Creation options must retain MvM.");
        }

        [Test]
        public void SqlTeamRow_ParsesCompleteKeyAndName()
        {
            using var table = new DataTable();
            foreach (string column in new[] { "source_game_id", "team_type", "team_id", "age_category" })
                table.Columns.Add(column, typeof(int));
            table.Columns.Add("team_name", typeof(string));
            table.Rows.Add(1, 1, TeamId, 18, "Poland");
            using var reader = table.CreateDataReader();
            reader.Read();
            ManagedTeam team = SqlAuthenticationRepository.ReadManagedTeam(reader);
            Assert.AreEqual(
                new TeamKey(SourceGame.Footstar, TeamType.NationalTeam, TeamId, AgeCategory.U18),
                team.Key,
                "Authentication rows must preserve all team identity fields."
            );
            Assert.AreEqual("Poland U18 (NT)", team.DisplayName, "Names must remain separate from identity.");
        }

        [Test]
        public void MvMOptions_IgnorePlayerPositionButRequireSupportedTeam()
        {
            var key = new TeamKey(SourceGame.Footstar, TeamType.Club, TeamId);
            Assert.IsTrue(
                NewGameOptionsRules.TryValidate(
                    new NewGameOptions(playerPosition: 0, gameType: FoobbuteoGameType.MvM, team: key),
                    out _
                ),
                "MvM creation must not require a pitch-player position."
            );
            Assert.IsFalse(
                NewGameOptionsRules.TryValidate(
                    new NewGameOptions(
                        gameType: FoobbuteoGameType.MvM,
                        team: new TeamKey((SourceGame)2, TeamType.Club, TeamId)
                    ),
                    out _
                ),
                "The future source must be rejected until its integration exists."
            );
        }

        [Test]
        public void MvMSqlCreation_UsesFullTeamKeyAndOmitsPersonalPlayerParameters()
        {
            var key = new TeamKey(SourceGame.Footstar, TeamType.Club, TeamId, AgeCategory.U19);
            var creator = new ServerUser(
                101,
                UserId,
                0,
                "Manager",
                UserRoles.Player,
                teams: new[] { new ManagedTeam(key, "Club") }
            );
            using var repository = new SqlGamesRepository(
                "Server=localhost;Database=unused;Integrated Security=true",
                30
            );
            using var command = repository.CreateGameCommand(
                new NewGameOptions(playerPosition: 0, gameType: FoobbuteoGameType.MvM, team: key),
                creator
            );
            Assert.AreEqual(1, command.Parameters["@source_game_id"].Value, "SQL must receive Footstar identity.");
            Assert.AreEqual(0, command.Parameters["@team_type"].Value, "SQL must receive the club type.");
            Assert.AreEqual(TeamId, command.Parameters["@team_id"].Value, "SQL must receive the external team ID.");
            Assert.AreEqual(19, command.Parameters["@age_category"].Value, "SQL must receive the youth variant.");
            Assert.IsFalse(
                command.Parameters.Contains("@creator_player_id"),
                "MvM must not require a manager's personal player ID."
            );
            Assert.IsFalse(
                command.Parameters.Contains("@creator_position"),
                "MvM must not reserve a pitch position for the manager."
            );
        }
    }
}
