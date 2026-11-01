// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using System.Linq;
using Imma.Common;
using Imma.Foobbuteo.Server;
using Imma.Foobbuteo.Server.Users;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game.Model;
using Imma.Foobbuteo.Shared.Users;
using NUnit.Framework;
using Unity.Netcode;

namespace Tests.Editor
{
    public class UserPresenceTests
    {
        private const ulong k_RequesterClient = 101;
        private const ulong k_RemoteClient = 102;
        private const int k_RequesterUser = 201;
        private const int k_RemoteUser = 202;
        private const int k_Room = 301;
        private const int k_OtherRoom = 302;
        private const byte k_ShirtNumber = 7;
        private UserRegistry m_registry;
        private UserPresenceService m_service;
        private HashSet<ulong> m_connected;

        [SetUp]
        public void SetUp()
        {
            m_registry = new UserRegistry();
            m_service = new UserPresenceService(m_registry);
            m_connected = new HashSet<ulong>();
        }

        [TearDown]
        public void TearDown() => m_registry.Dispose();

        [Test]
        public void GlobalList_OnlyActiveAuthenticatedConnections_AreReturnedInStableOrder()
        {
            Add(k_RequesterClient, k_RequesterUser, "Zulu");
            Add(NetworkManager.ServerClientId, k_RemoteUser, "alpha");
            Add(k_RemoteClient, k_RemoteUser + 1, "Alpha");
            Add(null, k_RemoteUser + 2, "Disconnected", slot: Slot());
            Add(k_RemoteClient + 1, k_RemoteUser + 3, "Transport gone");
            m_connected.Remove(k_RemoteClient + 1);
            m_connected.Add(k_RemoteClient + 2);

            var rows = m_service.GetUsers(k_RequesterClient, 0, m_connected.Contains);

            Assert.That(
                rows.Select(row => row.UserId),
                Is.EqualTo(new[] { k_RemoteUser, k_RemoteUser + 1, k_RequesterUser }),
                "Only authenticated active connections, including the host, should appear in name/user order."
            );
        }

        [Test]
        public void RoomRoster_IncludesSpectatorsAndAssignedUsersOnce_WithConnectionAndLocation()
        {
            Add(k_RequesterClient, k_RequesterUser, "Spectator", room: k_Room);
            Add(k_RemoteClient, k_RemoteUser, "Playing here", room: k_Room, slot: Slot());
            Add(k_RemoteClient + 1, k_RemoteUser + 1, "Playing elsewhere", room: k_OtherRoom, slot: Slot());
            Add(null, k_RemoteUser + 2, "Offline player", room: k_OtherRoom, slot: Slot());
            Add(k_RemoteClient + 2, k_RemoteUser + 3, "Other room", room: k_OtherRoom);

            var rows = m_service.GetUsers(k_RequesterClient, k_Room, m_connected.Contains);

            Assert.AreEqual(4, rows.Length, "The roster must union occupants and assigned players without duplicates.");
            Assert.AreEqual(
                k_OtherRoom,
                rows.Single(row => row.UserId == k_RemoteUser + 1).InRoomId,
                "An assigned player's actual viewing room must remain separate from their game slot."
            );
            var offline = rows.Single(row => row.UserId == k_RemoteUser + 2);
            Assert.IsFalse(offline.IsConnected, "Retained offline players must be marked disconnected.");
            Assert.AreEqual(Slot(), offline.GameSlot, "Offline players must retain their assigned shirt and team.");
        }

        [TestCase(0)]
        [TestCase(k_Room)]
        public void List_UnauthenticatedRequester_IsRejected(int roomId)
        {
            m_connected.Add(k_RequesterClient);

            Assert.Throws<AppException>(
                () => m_service.GetUsers(k_RequesterClient, roomId, m_connected.Contains),
                "A transport connection alone must not authorize presence queries."
            );
        }

        [TestCase(-1)]
        [TestCase(k_OtherRoom)]
        public void RoomList_InvalidScopeOrOtherRoom_IsRejected(int roomId)
        {
            Add(
                k_RequesterClient,
                k_RequesterUser,
                "Requester",
                room: k_Room,
                slot: new GameSlot(k_OtherRoom, TeamIndex.Home, k_ShirtNumber)
            );

            Assert.Throws<AppException>(
                () => m_service.GetUsers(k_RequesterClient, roomId, m_connected.Contains),
                "A claimed slot in another room must not authorize viewing that room's roster."
            );
        }

        [Test]
        public void GlobalList_DisconnectedRequester_IsRejected()
        {
            Add(k_RequesterClient, k_RequesterUser, "Requester");
            m_connected.Remove(k_RequesterClient);

            Assert.Throws<AppException>(
                () => m_service.GetUsers(k_RequesterClient, 0, m_connected.Contains),
                "A stale registry mapping must not authorize a disconnected requester."
            );
        }

        [TestCase(UserRoles.Player)]
        [TestCase(UserRoles.Admin)]
        public void Disconnect_RequiresAdministrator(UserRoles role)
        {
            Add(k_RequesterClient, k_RequesterUser, "Requester", role: role);
            Add(k_RemoteClient, k_RemoteUser, "Target");

            if (role == UserRoles.Admin)
                Assert.DoesNotThrow(
                    () => Authorize(k_RemoteUser, k_RemoteClient),
                    "An authenticated administrator should be allowed to disconnect a current remote connection."
                );
            else
                Assert.Throws<AppException>(
                    () => Authorize(k_RemoteUser, k_RemoteClient),
                    "A regular player must not be allowed to disconnect users."
                );
        }

        [Test]
        public void Disconnect_Host_IsProtected()
        {
            Add(k_RequesterClient, k_RequesterUser, "Admin", role: UserRoles.Admin);
            Add(NetworkManager.ServerClientId, k_RemoteUser, "Host");

            Assert.Throws<AppException>(
                () => Authorize(k_RemoteUser, NetworkManager.ServerClientId),
                "Even administrators must not disconnect the host connection."
            );
        }

        [Test]
        public void Disconnect_StaleRowAfterTakeover_IsRejectedAndReplacementRetainsSlot()
        {
            Add(k_RequesterClient, k_RequesterUser, "Admin", role: UserRoles.Admin);
            Add(k_RemoteClient, k_RemoteUser, "Target", room: k_Room, slot: Slot());
            const ulong replacementClient = 103;
            var replacement = new ServerUser(replacementClient, k_RemoteUser, k_RemoteUser, "Target", UserRoles.Player);
            Assert.IsNull(m_registry.RegisterUser(replacement), "The replacement session must register successfully.");
            m_connected.Add(replacementClient);

            Assert.Throws<AppException>(
                () => Authorize(k_RemoteUser, k_RemoteClient),
                "A row for the displaced session must not authorize kicking the replacement."
            );
            Assert.DoesNotThrow(
                () => Authorize(k_RemoteUser, replacementClient),
                "The refreshed row should authorize disconnecting the current session."
            );
            m_registry.UnregisterUserOnDisconnect(replacementClient);
            Assert.AreEqual(
                Slot(),
                m_registry.GetUsersInRoom(k_Room).Single().GameSlot.Value,
                "Normal disconnect cleanup must preserve the claimed-player reservation."
            );
        }

        [Test]
        public void Disconnect_WrongUserOrInactiveTarget_IsRejected()
        {
            Add(k_RequesterClient, k_RequesterUser, "Admin", role: UserRoles.Admin);
            Add(k_RemoteClient, k_RemoteUser, "Target");

            Assert.Throws<AppException>(
                () => Authorize(k_RemoteUser + 1, k_RemoteClient),
                "The target client must still belong to the user shown in the row."
            );
            m_connected.Remove(k_RemoteClient);
            Assert.Throws<AppException>(
                () => Authorize(k_RemoteUser, k_RemoteClient),
                "An already-disconnected connection must not be a valid target."
            );
            m_connected.Remove(k_RequesterClient);
            Assert.Throws<AppException>(
                () => Authorize(k_RemoteUser, k_RemoteClient),
                "An administrator's stale connection must not authorize actions."
            );
        }

        [Test]
        public void Configuration_OmittedRefreshInterval_DefaultsToTenSeconds()
        {
            var config = Newtonsoft.Json.JsonConvert.DeserializeObject<ServerConfig>("{}");

            Assert.AreEqual(
                10,
                config.GetClientRuntimeSettings().UserListRefreshIntervalSeconds,
                "Existing configurations without the new setting should use the approved ten-second default."
            );
        }

        [TestCase(0)]
        [TestCase(-10)]
        public void Configuration_NonPositiveInterval_IsRejected(int intervalSeconds)
        {
            var config = new ServerConfig { UserListRefreshIntervalSeconds = intervalSeconds };

            Assert.Throws<ArgumentOutOfRangeException>(
                () => config.GetClientRuntimeSettings(),
                "Invalid polling intervals must fail validation rather than start rapid polling."
            );
        }

        private void Authorize(int userId, ulong clientId) =>
            m_service.AuthorizeDisconnect(
                k_RequesterClient,
                userId,
                clientId,
                NetworkManager.ServerClientId,
                m_connected.Contains
            );

        private static GameSlot Slot() => new(k_Room, TeamIndex.Home, k_ShirtNumber);

        private void Add(
            ulong? clientId,
            int userId,
            string name,
            int room = 0,
            GameSlot? slot = null,
            UserRoles role = UserRoles.Player
        )
        {
            var user = new ServerUser(clientId, userId, userId, name, role, room, slot);
            Assert.IsNull(
                m_registry.RegisterUser(user, validateClientId: clientId.HasValue),
                $"Presence test user {userId} should register successfully."
            );
            if (clientId.HasValue)
                m_connected.Add(clientId.Value);
        }
    }
}
