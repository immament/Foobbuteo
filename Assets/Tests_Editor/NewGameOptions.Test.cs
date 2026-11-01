// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Foobbuteo.Shared.Game.Model;
using Imma.Foobbuteo.Shared.Lobby;
using Imma.Foobbuteo.Shared.Lobby.Contract;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;

namespace Tests.Editor
{
    public class NewGameOptionsTests
    {
        [Test]
        public void Default_ProvidesValidInitialFormSelections()
        {
            var options = NewGameOptions.Default;

            Assert.AreEqual(
                NewGameOptionsRules.MinStartInMinutes,
                options.StartInMinutes,
                "The initial start time must be the earliest allowed delay."
            );
            Assert.AreEqual(
                NewGameOptionsRules.TurnCounts[0],
                options.NumberOfTurns,
                "The initial turn count must be the shortest available game."
            );
            Assert.AreEqual(ShirtColorEnum.Blue, options.HomeTeamColor, "Home must initially wear blue.");
            Assert.AreEqual(ShirtColorEnum.Red, options.AwayTeamColor, "Away must initially wear red.");
            Assert.AreEqual(1, options.PlayerPosition, "The initial creator position must be goalkeeper.");
            Assert.AreEqual(FoobbuteoGameType.PvP, options.GameType, "New games must default to PvP.");
            Assert.IsTrue(NewGameOptionsRules.TryValidate(options, out _), "The default form must be valid.");
        }

        [TestCase(2, 30, 1)]
        [TestCase(15, 60, 11)]
        [TestCase(8, 40, 7)]
        [TestCase(9, 50, 9)]
        public void Validate_AllowedChoices_AcceptsOptions(int minutes, int turns, int position)
        {
            var options = new NewGameOptions(minutes, turns, playerPosition: position);

            Assert.IsTrue(
                NewGameOptionsRules.TryValidate(options, out string error),
                $"Allowed delay {minutes}, turns {turns} and position {position} must be accepted: {error}"
            );
            Assert.IsNull(error, "Valid selections must not report an error.");
        }

        [TestCase(
            NewGameOptionsRules.MinStartInMinutes - 1,
            30,
            1,
            ShirtColorEnum.Blue,
            ShirtColorEnum.Red,
            FoobbuteoGameType.PvP
        )]
        [TestCase(16, 30, 1, ShirtColorEnum.Blue, ShirtColorEnum.Red, FoobbuteoGameType.PvP)]
        [TestCase(2, 31, 1, ShirtColorEnum.Blue, ShirtColorEnum.Red, FoobbuteoGameType.PvP)]
        [TestCase(2, 30, 0, ShirtColorEnum.Blue, ShirtColorEnum.Red, FoobbuteoGameType.PvP)]
        [TestCase(2, 30, 12, ShirtColorEnum.Blue, ShirtColorEnum.Red, FoobbuteoGameType.PvP)]
        [TestCase(2, 30, 1, (ShirtColorEnum)(-1), ShirtColorEnum.Red, FoobbuteoGameType.PvP)]
        [TestCase(2, 30, 1, ShirtColorEnum.Blue, (ShirtColorEnum)8, FoobbuteoGameType.PvP)]
        [TestCase(2, 30, 1, ShirtColorEnum.Green, ShirtColorEnum.Green, FoobbuteoGameType.PvP)]
        [TestCase(2, 30, 1, ShirtColorEnum.Blue, ShirtColorEnum.Red, FoobbuteoGameType.MvM)]
        [TestCase(2, 30, 1, ShirtColorEnum.Blue, ShirtColorEnum.Red, FoobbuteoGameType.TvT)]
        [TestCase(2, 30, 1, ShirtColorEnum.Blue, ShirtColorEnum.Red, (FoobbuteoGameType)99)]
        public void Validate_InvalidSelection_RejectsOptions(
            int startInMinutes,
            int turns,
            int position,
            ShirtColorEnum home,
            ShirtColorEnum away,
            FoobbuteoGameType type
        )
        {
            var options = new NewGameOptions(startInMinutes, turns, home, away, position, type);

            Assert.IsFalse(
                NewGameOptionsRules.TryValidate(options, out string error),
                $"Invalid selection {startInMinutes}/{turns}/{position}/{home}/{away}/{type} must be rejected."
            );
            Assert.That(error, Is.Not.Null.And.Not.Empty, "Rejected options must explain the invalid selection.");
        }

        [Test]
        public void Serialization_RoundTrip_PreservesEverySelection()
        {
            const int bufferBytes = 256;
            var expected = new NewGameOptions(15, 60, ShirtColorEnum.Purple, ShirtColorEnum.White, 11);
            using var writer = new FastBufferWriter(bufferBytes, Allocator.Temp);

            writer.WriteValueSafe(expected);
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            reader.ReadValueSafe(out NewGameOptions actual);

            Assert.AreEqual(expected, actual, "The server must receive every option selected by the client.");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ResultSerialization_RoundTrip_PreservesOutcome(bool success)
        {
            const int bufferBytes = 256;
            const int createdGameId = 2001;
            const string failure = "Game could not be created.";
            var expected = new NewGameResult(success, success ? createdGameId : 0, success ? "" : failure);
            using var writer = new FastBufferWriter(bufferBytes, Allocator.Temp);

            writer.WriteNetworkSerializable(expected);
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            reader.ReadNetworkSerializable(out NewGameResult actual);

            Assert.AreEqual(expected, actual, "Creation responses must preserve success, game ID and error.");
        }
    }
}
