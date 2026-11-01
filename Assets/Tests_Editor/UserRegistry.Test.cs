// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Foobbuteo.Server;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game.Model;
using NUnit.Framework;
using Unity.Netcode;

namespace Tests.Editor
{
    public class UserRegistryTests
    {
        private UserRegistry m_registry;

        // [OneTimeSetUp]
        // public void OneTimeSetUp()
        // {
        //     Debug.Log("OneTimeSetUp");
        // }

        [SetUp]
        public void SetUp()
        {
            m_registry = new UserRegistry();
        }

        [Test]
        public void RegisterUserOnLogin_Success()
        {
            ServerUser user = TestUser1();

            var result = m_registry.RegisterUser(user);

            Assert.IsNull(result);
        }

        [Test]
        public void RegisterTheUserTwice_Fail()
        {
            ServerUser user = TestUser1();

            m_registry.RegisterUser(user);
            var result = m_registry.RegisterUser(user);

            StringAssert.Contains("already registered", result);
        }

        [Test]
        public void RegisterUserTwice_WithTheSameClientId_Fail()
        {
            ServerUser user1 = TestUser1();
            ServerUser user2 = TestUser1();

            m_registry.RegisterUser(user1);
            var result = m_registry.RegisterUser(user2);

            StringAssert.Contains("already registered", result);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RegisterUser_WithDifferentClientId_ReplacesConnectionAndRetainsState(bool hasPlayerSlot)
        {
            GameSlot? gameSlot = hasPlayerSlot ? new GameSlot(TH_.gameId_1, new PlayerKey(TeamIndex.Home, 1)) : null;
            ServerUser user1 = TestUser1(inGameRoomId: TH_.gameId_1, gameSlot: gameSlot);
            ServerUser user2 = TestUser1(TH_.clientId_2);
            m_registry.RegisterUser(user1);

            var result = m_registry.RegisterUser(user2, out ulong? replacedClientId);
            m_registry.UnregisterUserOnDisconnect(TH_.clientId_1);

            Assert.IsNull(result, "A different client should take over the remote session.");
            Assert.AreEqual(TH_.clientId_1, replacedClientId, "The caller must disconnect the previous client.");
            Assert.IsFalse(m_registry.TryGetUser(TH_.clientId_1, out _), "The old client must lose authorization.");
            Assert.IsTrue(
                m_registry.TryGetUser(TH_.clientId_2, out var registered),
                "The replacement must remain registered."
            );
            Assert.AreSame(user1, registered, "Takeover must preserve the existing server user instance.");
            Assert.AreEqual(
                TH_.clientId_2,
                registered.ClientId,
                "An old disconnect must not clear the new connection."
            );
            Assert.AreEqual(TH_.gameId_1, registered.InGameRoomId, "Takeover must retain the current room.");
            Assert.AreEqual(gameSlot, registered.GameSlot.NullableValue, "Takeover must retain the claimed player.");
            Assert.AreEqual(TH_.gameId_1, user2.InGameRoomId, "The incoming user must receive reconnect state.");
            Assert.AreEqual(
                gameSlot,
                user2.GameSlot.NullableValue,
                "The incoming user must receive its claimed player."
            );
        }

        [Test]
        public void RegisterUser_WhenExistingClientIsHost_RejectsWithoutChangingRegistration()
        {
            ServerUser hostUser = TestUser1(NetworkManager.ServerClientId);
            m_registry.RegisterUser(hostUser);

            string result = m_registry.RegisterUser(TestUser1(TH_.clientId_2), out ulong? replacedClientId);

            Assert.That(result, Does.Contain("already registered"), "The host account must reject another client.");
            Assert.IsNull(replacedClientId, "A rejected connection must not request host disconnection.");
            Assert.AreSame(
                hostUser,
                m_registry.GetUser(NetworkManager.ServerClientId),
                "Host registration must remain intact."
            );
            Assert.IsFalse(
                m_registry.TryGetUser(TH_.clientId_2, out _),
                "The rejected client must not gain authorization."
            );
        }

        [Test]
        public void RegisterUser_RepeatedTakeovers_OldDisconnectsDoNotClearLatestConnection()
        {
            ServerUser user = TestUser1();
            const ulong latestClientId = 91003;
            m_registry.RegisterUser(user);
            m_registry.RegisterUser(TestUser1(TH_.clientId_2), out _);

            string result = m_registry.RegisterUser(TestUser1(latestClientId), out ulong? replacedClientId);
            m_registry.UnregisterUserOnDisconnect(TH_.clientId_1);
            m_registry.UnregisterUserOnDisconnect(TH_.clientId_2);

            Assert.IsNull(result, "A third valid connection should replace the second.");
            Assert.AreEqual(
                TH_.clientId_2,
                replacedClientId,
                "Only the immediately preceding client should be displaced."
            );
            Assert.AreSame(
                user,
                m_registry.GetUser(latestClientId),
                "Delayed callbacks must preserve the latest session."
            );
            Assert.AreEqual(latestClientId, user.ClientId, "The latest client must remain connected.");
        }

        [Test]
        public void RegisterUser_WhenIncomingClientBelongsToAnotherUser_RejectsWithoutDisplacingEitherUser()
        {
            ServerUser existing = TestUser1();
            ServerUser other = TestUser1(TH_.clientId_2, TH_.userId_2);
            m_registry.RegisterUser(existing);
            m_registry.RegisterUser(other);

            string result = m_registry.RegisterUser(TestUser1(TH_.clientId_2), out ulong? replacedClientId);

            Assert.That(result, Does.Contain("already registered"), "Client IDs cannot be assigned to two users.");
            Assert.IsNull(replacedClientId, "Validation must precede displacement.");
            Assert.AreSame(existing, m_registry.GetUser(TH_.clientId_1), "The existing account must remain connected.");
            Assert.AreSame(other, m_registry.GetUser(TH_.clientId_2), "The other account must retain its client.");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RegisterUser_WithoutClientId_DoesNotDisplaceConnectedUser(bool validateClientId)
        {
            ServerUser existing = TestUser1();
            m_registry.RegisterUser(existing);
            var disconnected = new ServerUser(null, existing.UserId, existing.PlayerId, existing.Name, existing.Role);

            string result = m_registry.RegisterUser(disconnected, out ulong? replacedClientId, validateClientId);

            Assert.IsNotNull(result, "An offline user must not replace a connected session.");
            Assert.IsNull(replacedClientId, "Missing client IDs must not trigger disconnection.");
            Assert.AreSame(existing, m_registry.GetUser(TH_.clientId_1), "The existing connection must be preserved.");
        }

        [Test]
        public void UnregisterUserOnDisconnect_RemoveUserIfNotPlayGame_Success()
        {
            ServerUser user1 = TestUser1(TH_.clientId_1);

            m_registry.RegisterUser(user1);
            m_registry.UnregisterUserOnDisconnect(TH_.clientId_1);

            Assert.IsFalse(m_registry.TryGetUser(TH_.clientId_1, out _));
            Assert.IsFalse(m_registry.TryGetUser(user1.UserId, out _));
        }

        [Test]
        public void UnregisterUserOnDisconnect_CleanClientIdIfUserPlayGame_Success()
        {
            ServerUser user1 = TestUser1(
                TH_.clientId_1,
                gameSlot: new GameSlot(TH_.gameId_1, new PlayerKey(TeamIndex.Home, 1))
            );

            m_registry.RegisterUser(user1);
            m_registry.UnregisterUserOnDisconnect(TH_.clientId_1);

            Assert.IsFalse(m_registry.TryGetUser(TH_.clientId_1, out _));
            Assert.IsTrue(m_registry.TryGetUser(user1.UserId, out var registeredUser));
            Assert.IsNull(registeredUser.ClientId);
        }

        [Test]
        public void RegisterDisconnectedUser_SetNewClientId_Success()
        {
            ServerUser user1 = TestUser1(TH_.clientId_1, TH_.userId_1);
            ServerUser user2 = TestUser1(TH_.clientId_2, TH_.userId_1);

            m_registry.RegisterUser(user1);
            m_registry.UnregisterUserOnDisconnect(user1.ClientId.Value);

            var result = m_registry.RegisterUser(user2);

            Assert.IsNull(result);
            Assert.IsFalse(m_registry.TryGetUser(TH_.clientId_1, out _), "Client should be not registered.");

            Assert.IsTrue(m_registry.TryGetUser(TH_.clientId_2, out var registryUser1), "Client should be registered");
            Assert.AreEqual(TH_.userId_1, registryUser1.UserId);
            Assert.AreEqual(TH_.clientId_2, registryUser1.ClientId);

            Assert.IsTrue(m_registry.TryGetUser(TH_.userId_1, out var registryUser2), "User should be registered.");
            Assert.AreEqual(registryUser1, registryUser2);
        }

        [Test]
        public void RegisterDisconnectedUser_KeepPlayedGame_Success()
        {
            var gameSlot = new GameSlot(TH_.gameId_1, new PlayerKey(TeamIndex.Home, 1));
            ServerUser user1 = TestUser1(TH_.clientId_1, TH_.userId_1, gameSlot: gameSlot);
            ServerUser user2 = TestUser1(TH_.clientId_2, TH_.userId_1);

            m_registry.RegisterUser(user1);
            m_registry.UnregisterUserOnDisconnect(user1.ClientId.Value);

            var result = m_registry.RegisterUser(user2);

            Assert.IsNull(result);

            Assert.IsTrue(m_registry.TryGetUser(TH_.clientId_2, out var registryUser1), "Client should be registered");
            Assert.AreEqual(TH_.userId_1, registryUser1.UserId);
            Assert.AreEqual(registryUser1.GameSlot, gameSlot);
        }

        #region ++ Helpers ++++++++++++++++++++++++++++++++++++

        private static ServerUser TestUser1(
            ulong clientId = 91001,
            int userId = 92001,
            int playerId = 93001,
            string name = "Test User 1",
            UserRoles role = UserRoles.Player,
            int inGameRoomId = 0,
            GameSlot? gameSlot = null
        )
        {
            return new ServerUser(clientId, userId, playerId, name, role, inGameRoomId, gameSlot);
        }

        // private static ServerUser TestUser2(
        //     ulong clientId = 91002,
        //     int userId = 92002,
        //     int playerId = 93002,
        //     string name = "Test User 2",
        //     UserRoles role = UserRoles.Player,
        //     int inGameRoomId = 0,
        //     GameSlot? gameSlot = null
        // )
        // {
        //     return new ServerUser(clientId, userId, playerId, name, role, inGameRoomId, gameSlot);
        // }

        #endregion -- Helpers -----
    }

    internal static class TH_
    {
        public static readonly ulong clientId_1 = ClientId(1);
        public static readonly ulong clientId_2 = ClientId(2);

        public static readonly int userId_1 = UserId(1);
        public static readonly int userId_2 = UserId(2);

        public static readonly int gameId_1 = GameId(1);

        public static ulong ClientId(ulong idx = 1) => 91000 + idx;

        public static int UserId(int idx = 1) => 92000 + idx;

        public static int PlayerId(int idx = 1) => 93000 + idx;

        public static int GameId(int idx = 1) => 94000 + idx;
    }
}
