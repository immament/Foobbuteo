// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Tests.Editor.Client
{
    [Category("PlayerInactivity")]
    public class PlayerControlStateTests
    {
        private const int GameId = 7601;
        private const int UserId = 8101;

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void SnapshotSerializationAndHydration_PreserveIndependentControlFlags(bool inactive, bool connected)
        {
            GameDetails_PlayerContract expected = CreateContract(inactive, connected);
            using var writer = new FastBufferWriter(4096, Allocator.Temp);
            writer.WriteNetworkSerializable(expected);
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            reader.ReadNetworkSerializable(out GameDetails_PlayerContract actual);
            ClientPlayerState player = ClientPlayerState.FromContract(GameId, actual);

            Assert.AreEqual(
                inactive,
                player.IsInactive,
                "Snapshots must preserve inactivity independently of connection."
            );
            Assert.AreEqual(
                connected,
                player.IsConnected,
                "Snapshots must preserve connection independently of inactivity."
            );
            Assert.AreEqual(
                expected.LastActivityTurn,
                player.LastActivityTurn,
                "Snapshots must preserve the activity baseline."
            );
            Assert.AreEqual(
                UserId,
                player.UserId,
                "Disconnected and inactive players must retain their owner identity."
            );
            Assert.IsTrue(player.IsCaptain, "Snapshots must preserve captain status.");
        }

        [Test]
        public void LiveControlUpdate_ChangesFlagsAndModeWithoutMovingPitchPlayer()
        {
            ClientPlayerState player = ClientPlayerState.FromContract(GameId, CreateContract(false, true));
            Vector2 position = player.Position;
            GameDetails_PlayerContract update = CreateContract(true, false);
            update.Position = Vector2.zero;
            update.IsCaptain = false;
            update.PlayerMode = PlayerModeEnum.Confrontation;
            update.ActionPoints = 25f;

            player.OnPlayerControlClaimed(update);

            Assert.IsTrue(player.IsInactive, "Live updates must apply inactivity.");
            Assert.IsFalse(player.IsConnected, "Live updates must apply disconnection.");
            Assert.IsFalse(player.IsCaptain, "Live updates must clear former captain flags.");
            Assert.AreEqual(position, player.Position, "Control updates must preserve pitch position during playback.");
            Assert.AreEqual(25f, player.ActionPoints, "Control updates must synchronize remaining action points.");
            Assert.AreEqual(
                PlayerModeEnum.Confrontation,
                player.PlayerMode,
                "Accepted mode changes must reach observers."
            );
        }

        private static GameDetails_PlayerContract CreateContract(bool inactive, bool connected) =>
            new()
            {
                TeamIdx = TeamIndex.Home,
                PlayerIdx = 3,
                PlayerId = 8301,
                UserId = UserId,
                Name = "Assigned player",
                IsCaptain = true,
                IsInactive = inactive,
                IsConnected = connected,
                LastActivityTurn = 2,
                Position = new Vector2(150f, 100f),
                ActionPoints = 100f,
            };
    }
}
