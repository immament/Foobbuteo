// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using Imma.Foobbuteo.Shared.Lobby.Contract;
using Imma.Foobbuteo.Shared.Users;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;

namespace Tests.Editor
{
    public class UserPresenceSerializationTests
    {
        private const int k_BufferBytes = 4096;
        private const int k_RoomId = 701;
        private const ulong k_RequestId = 801;

        [Test]
        public void ServerInfo_RoundTrip_PreservesPublicSettingsAndGames()
        {
            const int refreshIntervalSeconds = 17;
            var expected = new ServerInfoContract(
                System.Array.Empty<GameInfoContract>(),
                new ClientRuntimeSettings(refreshIntervalSeconds)
            );

            var actual = RoundTrip(expected);

            Assert.AreEqual(
                refreshIntervalSeconds,
                actual.Settings.UserListRefreshIntervalSeconds,
                "Clients must receive the server-selected polling interval."
            );
            Assert.IsEmpty(actual.Games, "Adding settings must preserve game-list serialization.");
        }

        [TestCase(true, true)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        public void Roster_RoundTrip_PreservesIdentityLocationAndOptionalAssignment(bool connected, bool hasSlot)
        {
            var row = new UserPresenceRow
            {
                IsConnected = connected,
                ClientId = 901,
                UserId = 1001,
                PlayerId = 1101,
                PlayerName = "Łukasz <player>",
                InRoomId = k_RoomId + 1,
                HasGameSlot = hasSlot,
                GameSlot = new GameSlot(k_RoomId, TeamIndex.Away, 11),
            };

            var actual = RoundTrip(new UserListResponse(k_RequestId, k_RoomId, new[] { row }));

            Assert.AreEqual(
                k_RequestId,
                actual.RequestId,
                "Responses must retain the identity used to reject stale snapshots."
            );
            Assert.AreEqual(
                k_RoomId,
                actual.RoomId,
                "The requested room must remain separate from a row's actual location."
            );
            Assert.AreEqual(1, actual.Users.Length, "The roster must preserve its rows.");
            var received = actual.Users[0];
            Assert.AreEqual(row.UserId, received.UserId, "The authenticated account ID must survive serialization.");
            Assert.AreEqual(row.PlayerId, received.PlayerId, "The player ID must survive serialization.");
            Assert.AreEqual(
                row.PlayerName,
                received.PlayerName,
                "Player names must preserve Unicode and literal markup."
            );
            Assert.AreEqual(row.InRoomId, received.InRoomId, "Assigned users may be viewing another room.");
            Assert.AreEqual(connected, received.IsConnected, "Connection status must survive serialization.");
            Assert.AreEqual(
                connected ? row.ClientId : 0UL,
                received.ClientId,
                "Offline rows must not carry a stale client ID."
            );
            Assert.AreEqual(hasSlot, received.HasGameSlot, "The optional assignment flag must survive serialization.");
            Assert.AreEqual(
                hasSlot ? row.GameSlot : default,
                received.GameSlot,
                "Only assigned users should carry a game slot."
            );
        }

        [Test]
        public void FailureResults_RoundTrip_PreserveRequestAndReason()
        {
            const string error = "Connection changed";

            var list = RoundTrip(new UserListResponse(k_RequestId, k_RoomId, error: error));
            var kick = RoundTrip(new UserDisconnectResult(k_RequestId, error));

            Assert.IsEmpty(list.Users, "Failed list requests should serialize an empty snapshot.");
            Assert.AreEqual(error, list.Error, "List errors should reach the requesting user.");
            Assert.AreEqual(k_RequestId, kick.RequestId, "Administrator results must identify their action.");
            Assert.AreEqual(error, kick.Error, "Administrator rejection reasons must reach the requester.");
        }

        private static T RoundTrip<T>(T expected)
            where T : INetworkSerializable, new()
        {
            using var writer = new FastBufferWriter(k_BufferBytes, Allocator.Temp);
            writer.WriteNetworkSerializable(expected);
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            reader.ReadNetworkSerializable(out T actual);
            return actual;
        }
    }
}
