// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Imma.Foobbuteo.Common;
using Imma.Foobbuteo.Server;
using Imma.Foobbuteo.Server.Game;
using Imma.Foobbuteo.Server.Game.Model;
using Imma.Foobbuteo.Server.Model;
using Imma.Foobbuteo.Server.Repos;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using Imma.Foobbuteo.Shared.Lobby;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor
{
    public class FoobbuteoServerStateTests
    {
        private const int k_gameId = 94001;
        private const int k_otherGameId = 94002;
        private const int k_userId = 92001;
        private const ulong k_clientId = 91001;

        private UserRegistry m_userRegistry;
        private FoobbuteoServerState m_serverState;
        private readonly List<FoobbuteoGameEngine> m_gameEngines = new();

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Initialize();
            m_userRegistry = new UserRegistry();
            ServiceLocator.Current.Register<IUserRegistry>(m_userRegistry);
            ServiceLocator.Current.Register<IGamePersistenceRepository>(new MockGamePersistenceRepository());
            m_serverState = new FoobbuteoServerState(m_userRegistry);
        }

        [TearDown]
        public void TearDown()
        {
            m_serverState.Dispose();
            foreach (FoobbuteoGameEngine gameEngine in m_gameEngines)
                gameEngine.Dispose();
            m_gameEngines.Clear();
            ServiceLocator.ClearAll();
        }

        [Test]
        public void GameEnded_ClearsMatchingSlotsAndNotifiesConnectedUsers()
        {
            ServerUser connectedUser = RegisterUser(k_clientId, k_userId, k_gameId);
            ServerUser disconnectedUser = RegisterUser(null, k_userId + 1, k_gameId);
            ServerUser otherGameUser = RegisterUser(k_clientId + 1, k_userId + 2, k_otherGameId);
            var notifications = new List<(ulong ClientId, ServerUser User)>();
            m_serverState.UserChanged += (clientId, user) => notifications.Add((clientId, user));
            TestGameRoom gameRoom = AddGame(FoobbuteoGameStatus.Ongoing, turn: 1, turns: 1);

            Assert.IsFalse(gameRoom.GameLoop.MoveNext());

            Assert.IsFalse(connectedUser.GameSlot.HasValue);
            Assert.IsFalse(disconnectedUser.GameSlot.HasValue);
            Assert.AreEqual(k_otherGameId, otherGameUser.GameSlot.Value.GameId);
            Assert.AreEqual(1, notifications.Count);
            Assert.AreEqual(k_clientId, notifications[0].ClientId);
            Assert.AreSame(connectedUser, notifications[0].User);
            Assert.IsFalse(notifications[0].User.GameSlot.HasValue);
        }

        [Test]
        public void AddEndedGame_ClearsRestoredSlotAndNotifiesConnectedUser()
        {
            ServerUser user = RegisterUser(k_clientId, k_userId, k_gameId);
            int notificationCount = 0;
            m_serverState.UserChanged += (_, changedUser) =>
            {
                notificationCount++;
                Assert.AreSame(user, changedUser);
                Assert.IsFalse(changedUser.GameSlot.HasValue);
            };

            AddGame(FoobbuteoGameStatus.Ended);

            Assert.IsFalse(user.GameSlot.HasValue);
            Assert.AreEqual(1, notificationCount);
        }

        [Test]
        public void EnterEndedGame_DoesNotRestoreHistoricalPlayerSlot()
        {
            ServerUser user = RegisterUser(k_clientId, k_userId);
            TestGameRoom gameRoom = AddGame(FoobbuteoGameStatus.Ended, playerUserId: k_userId);
            int notificationCount = 0;
            m_serverState.UserChanged += (_, _) => notificationCount++;

            Assert.IsTrue(
                m_serverState.AddClientToGame(k_clientId, k_gameId, user),
                "A user must be able to enter an ended game as a spectator."
            );

            Assert.AreEqual(k_gameId, user.InGameRoomId, "Entering must update the viewed room.");
            Assert.IsFalse(
                user.GameSlot.HasValue,
                "An ended game's historical roster must not restore player control."
            );
            Assert.AreEqual(1, notificationCount, "Entering an ended room must notify the room change.");
            Assert.AreEqual(1, gameRoom.AddedClients.Count, "Entering must open the room once.");
            Assert.AreEqual(k_clientId, gameRoom.AddedClients[0], "The room must be opened for the requesting client.");
        }

        [Test]
        public void EnterEndedGame_ClearsMatchingStaleSlotAndNotifiesClient()
        {
            ServerUser user = RegisterUser(k_clientId, k_userId);
            AddGame(FoobbuteoGameStatus.Ended, playerUserId: k_userId);
            user.RestoreGameSlot(new GameSlot(k_gameId, TeamIndex.Home, 1));
            int notificationCount = 0;
            m_serverState.UserChanged += (clientId, changedUser) =>
            {
                notificationCount++;
                Assert.AreEqual(k_clientId, clientId);
                Assert.AreSame(user, changedUser);
            };

            Assert.IsTrue(m_serverState.AddClientToGame(k_clientId, k_gameId, user));

            Assert.AreEqual(k_gameId, user.InGameRoomId);
            Assert.IsFalse(user.GameSlot.HasValue);
            Assert.AreEqual(1, notificationCount);
        }

        [Test]
        public void EnterEndedGame_PreservesSlotForDifferentGame()
        {
            ServerUser user = RegisterUser(k_clientId, k_userId, k_otherGameId);
            AddGame(FoobbuteoGameStatus.Ended, playerUserId: k_userId);
            int notificationCount = 0;
            m_serverState.UserChanged += (_, _) => notificationCount++;

            Assert.IsTrue(
                m_serverState.AddClientToGame(k_clientId, k_gameId, user),
                "An assignment in another game must not prevent spectating an ended room."
            );

            Assert.AreEqual(k_gameId, user.InGameRoomId, "Spectating must update the viewed room.");
            Assert.AreEqual(k_otherGameId, user.GameSlot.Value.GameId, "Spectating must retain the other assignment.");
            Assert.AreEqual(
                1,
                notificationCount,
                "Entering another room must notify without replacing its player slot."
            );
        }

        [TestCase(FoobbuteoGameStatus.Pending)]
        [TestCase(FoobbuteoGameStatus.Ongoing)]
        public void EnterPlayableGame_RestoresPlayerSlotAndNotifiesClient(FoobbuteoGameStatus status)
        {
            ServerUser user = RegisterUser(k_clientId, k_userId);
            AddGame(status, playerUserId: k_userId);
            int notificationCount = 0;
            m_serverState.UserChanged += (_, _) => notificationCount++;

            Assert.IsTrue(m_serverState.AddClientToGame(k_clientId, k_gameId, user));

            Assert.AreEqual(k_gameId, user.InGameRoomId);
            Assert.AreEqual(new GameSlot(k_gameId, TeamIndex.Home, 1), user.GameSlot.Value);
            Assert.AreEqual(1, notificationCount);
        }

        [TestCase(1)]
        [TestCase(11)]
        public async System.Threading.Tasks.Task CreatedMockGame_JoiningRestoresSelectedHomeSlot(int position)
        {
            ServerUser user = RegisterUser(k_clientId, k_userId);
            using var repository = new MockGamesRepository(() => new());
            var options = new NewGameOptions(playerPosition: position);
            int gameId = await repository.CreateGameAsync(options, user);
            var gameState = (await repository.GetGameListAsync(240, gameId - 1)).Single();
            var room = new TestGameRoom(gameId);
            var engine = new FoobbuteoGameEngine(null, room, gameState);
            m_gameEngines.Add(engine);
            m_serverState.AddGame(engine, notify: false);
            int notificationCount = 0;
            m_serverState.UserChanged += (_, _) => notificationCount++;

            Assert.IsFalse(user.GameSlot.HasValue, "Mock persistence must not mutate the live user's slot.");
            bool joined = m_serverState.AddClientToGame(k_clientId, gameId, user);

            Assert.IsTrue(joined, "The creator must be able to enter the loaded game.");
            Assert.AreEqual(
                new GameSlot(gameId, TeamIndex.Home, (byte)position),
                user.GameSlot.Value,
                "Joining must restore the creator's selected home-team position."
            );
            Assert.AreEqual(1, notificationCount, "Slot restoration must notify the connected creator.");
            Assert.That(room.AddedClients, Is.EqualTo(new[] { k_clientId }), "The creator must join the game room.");
        }

        [TestCase(FoobbuteoGameStatus.Pending, UserRoles.Player, true, true)]
        [TestCase(FoobbuteoGameStatus.Ongoing, UserRoles.Player, true, false)]
        [TestCase(FoobbuteoGameStatus.Ended, UserRoles.Player, true, true)]
        [TestCase(FoobbuteoGameStatus.Ongoing, UserRoles.Player, false, true)]
        [TestCase(FoobbuteoGameStatus.Ongoing, UserRoles.Admin, true, true)]
        public void LeaveRoom_EnforcesRoleAssignmentAndStatus(
            FoobbuteoGameStatus status,
            UserRoles role,
            bool assigned,
            bool allowed
        )
        {
            var user = RegisterUser(k_clientId, k_userId, assigned ? k_gameId : 0, role);
            AddGame(status);
            user.EnterRoom(k_gameId);
            int notifications = 0;
            m_serverState.UserChanged += (_, _) => notifications++;

            if (allowed)
            {
                Assert.IsTrue(
                    m_serverState.RemoveClientFromGameRoom(k_clientId, k_gameId),
                    "An unlocked user must be able to leave the room."
                );
                Assert.AreEqual(0, user.InGameRoomId, "Leaving must clear the viewed room.");
                Assert.AreEqual(1, notifications, "Leaving must publish the updated user once.");
                Assert.AreEqual(
                    assigned && status != FoobbuteoGameStatus.Ended,
                    user.GameSlot.HasValue,
                    "Leaving must preserve an active player assignment."
                );
            }
            else
            {
                Assert.Throws<AppException>(
                    () => m_serverState.RemoveClientFromGameRoom(k_clientId, k_gameId),
                    "An ongoing non-admin player must receive a rejected leave request."
                );
                Assert.AreEqual(k_gameId, user.InGameRoomId, "Rejected leave must retain membership.");
                Assert.AreEqual(0, notifications, "Rejected leave must not publish a state change.");
            }
        }

        [Test]
        public void JoinOtherRoom_LockedPlayerRetainsMembershipAndVisibility()
        {
            var user = RegisterUser(k_clientId, k_userId, k_gameId);
            var current = AddGame(FoobbuteoGameStatus.Ongoing);
            var destination = AddGame(FoobbuteoGameStatus.Pending, gameId: k_otherGameId);
            user.EnterRoom(k_gameId);

            Assert.Throws<AppException>(
                () => m_serverState.AddClientToGame(k_clientId, k_otherGameId, user),
                "Switching rooms must enforce the same ongoing-player lock as leaving."
            );

            Assert.AreEqual(k_gameId, user.InGameRoomId, "Rejected switch must preserve the current room.");
            Assert.IsEmpty(current.ClosedClients, "Rejected switch must not hide the current room.");
            Assert.IsEmpty(destination.AddedClients, "Rejected switch must not show the destination.");
        }

        [Test]
        public void JoinMissingRoom_DoesNotCloseCurrentRoom()
        {
            var user = RegisterUser(k_clientId, k_userId);
            var current = AddGame(FoobbuteoGameStatus.Pending);
            user.EnterRoom(k_gameId);

            Assert.Throws<AppException>(
                () => m_serverState.AddClientToGame(k_clientId, k_otherGameId, user),
                "A missing destination must be rejected before leaving the current room."
            );

            Assert.AreEqual(k_gameId, user.InGameRoomId, "Missing destination must preserve membership.");
            Assert.IsEmpty(current.ClosedClients, "Missing destination must preserve room visibility.");
        }

        [Test]
        public void SwitchRoom_PublishesOneCompleteStateAndPreservesPlayerSlot()
        {
            var user = RegisterUser(k_clientId, k_userId, k_gameId);
            var current = AddGame(FoobbuteoGameStatus.Pending);
            var destination = AddGame(FoobbuteoGameStatus.Pending, gameId: k_otherGameId);
            user.EnterRoom(k_gameId);
            int notifications = 0;
            m_serverState.UserChanged += (_, changed) =>
            {
                notifications++;
                Assert.AreEqual(k_otherGameId, changed.InGameRoomId, "Notification must contain the new room.");
                Assert.AreEqual(k_gameId, changed.GameSlot.Value.GameId, "Notification must retain the assignment.");
            };

            m_serverState.AddClientToGame(k_clientId, k_otherGameId, user);

            Assert.AreEqual(1, notifications, "A switch must publish one complete user update.");
            Assert.That(current.ClosedClients, Is.EqualTo(new[] { k_clientId }), "Switch must hide the previous room.");
            Assert.That(
                destination.AddedClients,
                Is.EqualTo(new[] { k_clientId }),
                "Switch must show the destination."
            );
        }

        [TestCase(FoobbuteoGameStatus.Pending, UserRoles.Player)]
        [TestCase(FoobbuteoGameStatus.Ongoing, UserRoles.Player)]
        [TestCase(FoobbuteoGameStatus.Pending, UserRoles.Admin)]
        [TestCase(FoobbuteoGameStatus.Ongoing, UserRoles.Admin)]
        public void FirstHandshake_RestoresRosterAssignmentAndOpensRoom(FoobbuteoGameStatus status, UserRoles role)
        {
            var user = RegisterUser(k_clientId, k_userId, role: role);
            var room = AddGame(status, playerUserId: k_userId);
            int notifications = 0;
            m_serverState.UserChanged += (_, _) => notifications++;

            m_serverState.OnLobbyHandshake(k_clientId, gamesReady: true);
            m_serverState.OnLobbyHandshake(k_clientId, gamesReady: true);

            Assert.AreEqual(k_gameId, user.InGameRoomId, "First handshake must open the assigned game.");
            Assert.AreEqual(k_gameId, user.GameSlot.Value.GameId, "Roster assignment must be restored.");
            Assert.That(
                room.AddedClients,
                Is.EqualTo(new[] { k_clientId }),
                "Repeated handshakes must not reopen the room."
            );
            Assert.AreEqual(1, notifications, "Room and slot restoration must publish one complete update.");
        }

        [Test]
        public void PendingRoom_LeftDeliberately_IsNotReopenedByHandshakeRefresh()
        {
            var user = RegisterUser(k_clientId, k_userId);
            var room = AddGame(FoobbuteoGameStatus.Pending, playerUserId: k_userId);
            m_serverState.OnLobbyHandshake(k_clientId, gamesReady: true);
            m_serverState.RemoveClientFromGameRoom(k_clientId, k_gameId);

            m_serverState.OnLobbyHandshake(k_clientId, gamesReady: true);

            Assert.AreEqual(
                0,
                user.InGameRoomId,
                "A lobby refresh must respect deliberate departure from a pending room."
            );
            Assert.AreEqual(1, room.AddedClients.Count, "Refresh must not reopen a pending room.");
        }

        [Test]
        public void HandshakeBeforeLoading_DefersUntilRoomsAreReady()
        {
            var user = RegisterUser(k_clientId, k_userId);
            m_serverState.OnLobbyHandshake(k_clientId, gamesReady: false);
            var room = AddGame(FoobbuteoGameStatus.Pending, playerUserId: k_userId);
            Assert.IsEmpty(room.AddedClients, "Loading must not open a room before its network object is ready.");

            m_serverState.CompletePendingHandshakes();
            m_serverState.CompletePendingHandshakes();

            Assert.AreEqual(k_gameId, user.InGameRoomId, "Deferred handshake must restore the room after loading.");
            Assert.AreEqual(1, room.AddedClients.Count, "Deferred processing must be idempotent.");
        }

        [Test]
        public void DeferredHandshake_DisconnectedSessionIsDiscarded()
        {
            RegisterUser(k_clientId, k_userId, k_gameId);
            m_serverState.OnLobbyHandshake(k_clientId, gamesReady: false);
            m_userRegistry.UnregisterUserOnDisconnect(k_clientId);
            m_serverState.ForgetClient(k_clientId);
            var room = AddGame(FoobbuteoGameStatus.Pending, playerUserId: k_userId);

            m_serverState.CompletePendingHandshakes();

            Assert.IsEmpty(room.AddedClients, "A disconnected session must not receive deferred room opening.");
        }

        [Test]
        public void TakeoverHandshake_ReopensSameRoomForReplacementClient()
        {
            RegisterUser(k_clientId, k_userId, k_gameId).EnterRoom(k_gameId);
            var room = AddGame(FoobbuteoGameStatus.Ongoing, playerUserId: k_userId);
            var incoming = new ServerUser(k_clientId + 1, k_userId, k_userId + 1000, "Replacement", UserRoles.Player);
            Assert.IsNull(
                m_userRegistry.RegisterUser(incoming, out _),
                "Replacement must take over the remote session."
            );

            m_serverState.OnLobbyHandshake(k_clientId + 1, gamesReady: true);
            m_serverState.OnLobbyHandshake(k_clientId, gamesReady: true);

            Assert.That(
                room.AddedClients,
                Is.EqualTo(new[] { k_clientId + 1 }),
                "Only the replacement connection must receive the retained room."
            );
            Assert.AreEqual(k_gameId, incoming.GameSlot.Value.GameId, "Takeover must retain player control.");
        }

        [TestCase(UserRoles.Player)]
        [TestCase(UserRoles.Admin)]
        public void GameStart_MovesAssignedUserFromAnotherRoom(UserRoles role)
        {
            var user = RegisterUser(k_clientId, k_userId, role: role);
            var assigned = AddGame(FoobbuteoGameStatus.Pending, playerUserId: k_userId);
            var other = AddGame(FoobbuteoGameStatus.Ongoing, gameId: k_otherGameId);
            m_serverState.OnLobbyHandshake(k_clientId, gamesReady: true);
            m_serverState.AddClientToGame(k_clientId, k_otherGameId, user);
            var engine = m_gameEngines.Single(game => game.GameId == k_gameId);
            engine.GameState.StartGame();

            RaiseGameStarted(engine);
            RaiseGameStarted(engine);

            Assert.AreEqual(k_gameId, user.InGameRoomId, "Game start must return its assigned player to the room.");
            Assert.That(
                other.ClosedClients,
                Is.EqualTo(new[] { k_clientId }),
                "Automatic return must close the other room once."
            );
            Assert.AreEqual(2, assigned.AddedClients.Count, "Repeated start events must not duplicate entry.");
        }

        private static void RaiseGameStarted(FoobbuteoGameEngine engine)
        {
            var eventField = typeof(FoobbuteoGameEngine).GetField(
                "GameStarted",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            );
            ((Action)eventField.GetValue(engine))?.Invoke();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void GameStart_DoesNotReopenPresentOrDisconnectedPlayer(bool connected)
        {
            var user = RegisterUser(connected ? k_clientId : null, k_userId, k_gameId);
            var room = AddGame(FoobbuteoGameStatus.Pending, playerUserId: k_userId);
            if (connected)
                m_serverState.OnLobbyHandshake(k_clientId, gamesReady: true);
            var engine = m_gameEngines.Single();
            engine.GameState.StartGame();

            RaiseGameStarted(engine);

            Assert.AreEqual(
                connected ? 1 : 0,
                room.AddedClients.Count,
                "Start must neither duplicate an existing room entry nor open a room for a disconnected player."
            );
            Assert.AreEqual(k_gameId, user.GameSlot.Value.GameId, "Start must preserve player assignment.");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MultipleAssignments_PrefersValidStoredSlotOtherwiseOngoing(bool retainStoredSlot)
        {
            var user = RegisterUser(k_clientId, k_userId, retainStoredSlot ? k_gameId : 0);
            AddGame(FoobbuteoGameStatus.Pending, playerUserId: k_userId);
            AddGame(FoobbuteoGameStatus.Ongoing, playerUserId: k_userId, gameId: k_otherGameId);

            m_serverState.OnLobbyHandshake(k_clientId, gamesReady: true);

            int expected = retainStoredSlot ? k_gameId : k_otherGameId;
            Assert.AreEqual(expected, user.InGameRoomId, "Valid stored assignment precedes ongoing status.");
            Assert.AreEqual(expected, user.GameSlot.Value.GameId, "Room and slot must use the same selected game.");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MultiplePendingAssignments_PrefersEarliestStartThenLowestId(bool sameStart)
        {
            var user = RegisterUser(k_clientId, k_userId);
            var firstStart = new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
            AddGame(
                FoobbuteoGameStatus.Pending,
                playerUserId: k_userId,
                startDateUtc: sameStart ? firstStart : firstStart.AddMinutes(1)
            );
            AddGame(
                FoobbuteoGameStatus.Pending,
                playerUserId: k_userId,
                gameId: k_otherGameId,
                startDateUtc: firstStart
            );

            m_serverState.OnLobbyHandshake(k_clientId, gamesReady: true);

            Assert.AreEqual(
                sameStart ? k_gameId : k_otherGameId,
                user.InGameRoomId,
                "Ambiguous pending assignments must resolve by earliest start, then lowest game ID."
            );
        }

        [Test]
        public void DeleteOngoingGame_ClearsRoomAndSlotInOneNotificationAndRemovesSubscriptions()
        {
            var user = RegisterUser(k_clientId, k_userId, k_gameId);
            var room = AddGame(FoobbuteoGameStatus.Ongoing, playerUserId: k_userId);
            user.EnterRoom(k_gameId);
            int notifications = 0;
            m_serverState.UserChanged += (_, changed) =>
            {
                notifications++;
                Assert.AreEqual(0, changed.InGameRoomId, "Deletion must publish the cleared room.");
                Assert.IsFalse(changed.GameSlot.HasValue, "Deletion must publish the cleared player slot.");
            };

            Assert.IsTrue(
                m_serverState.DeleteGame(k_gameId),
                "Administrative deletion must bypass the player room lock."
            );
            RaiseGameStarted(m_gameEngines.Single());

            Assert.AreEqual(1, notifications, "Deletion must publish one complete user update.");
            Assert.That(room.ClosedClients, Is.EqualTo(new[] { k_clientId }), "Deletion must close the current room.");
            Assert.IsEmpty(room.AddedClients, "Deleted games must not retain start subscriptions.");
        }

        [Test]
        public void Dispose_RemovesStartSubscriptionAndDeferredHandshake()
        {
            RegisterUser(k_clientId, k_userId);
            var room = AddGame(FoobbuteoGameStatus.Pending, playerUserId: k_userId);
            m_serverState.OnLobbyHandshake(k_clientId, gamesReady: false);

            m_serverState.Dispose();
            RaiseGameStarted(m_gameEngines.Single());
            m_serverState.CompletePendingHandshakes();

            Assert.IsEmpty(room.AddedClients, "Disposed state must not react to game start or deferred loading.");
        }

        private ServerUser RegisterUser(
            ulong? clientId,
            int userId,
            int gameSlotId = 0,
            UserRoles role = UserRoles.Player
        )
        {
            GameSlot? gameSlot = gameSlotId > 0 ? new GameSlot(gameSlotId, TeamIndex.Home, 1) : null;
            var user = new ServerUser(clientId, userId, userId + 1000, $"User {userId}", role, gameSlot: gameSlot);
            Assert.IsNull(
                m_userRegistry.RegisterUser(user, validateClientId: clientId.HasValue),
                "Test users must be registered before performing room operations."
            );
            return user;
        }

        private TestGameRoom AddGame(
            FoobbuteoGameStatus status,
            int playerUserId = 0,
            int turn = 0,
            int turns = 30,
            int gameId = k_gameId,
            DateTime? startDateUtc = null
        )
        {
            var room = new TestGameRoom(gameId);
            ServerGameState gameState = CreateGameState(status, playerUserId, turn, turns, gameId, startDateUtc);
            var gameEngine = new FoobbuteoGameEngine(null, room, gameState);
            m_gameEngines.Add(gameEngine);
            m_serverState.AddGame(gameEngine, notify: false);
            gameEngine.InitGame();
            return room;
        }

        private static ServerGameState CreateGameState(
            FoobbuteoGameStatus status,
            int playerUserId,
            int turn,
            int turns,
            int gameId = k_gameId,
            DateTime? startDateUtc = null
        )
        {
            ServerPlayerState player = ServerPlayerState.CreateForTest(
                gameId,
                TeamIndex.Home,
                1,
                playerId: 93001,
                name: "Test Player",
                position: Vector2.zero,
                userId: playerUserId
            );
            var homeTeam = new ServerTeamState(TeamIndex.Home, 1, "Home", ShirtColorEnum.Red, new[] { player });
            var awayTeam = new ServerTeamState(
                TeamIndex.Away,
                2,
                "Away",
                ShirtColorEnum.Blue,
                Array.Empty<ServerPlayerState>()
            );
            return ServerGameState.CreateForTest(
                gameId,
                startDateUtc ?? DateTime.UtcNow,
                homeTeam,
                awayTeam,
                status,
                turns,
                turn,
                endDate: status == FoobbuteoGameStatus.Ended ? DateTime.UtcNow : null
            );
        }

        private sealed class TestGameRoom : IServerGameRoomNB
        {
            public TestGameRoom(int gameId)
            {
                GameId = gameId;
            }

            public int GameId { get; }
            public IEnumerator GameLoop { get; private set; }
            public List<ulong> AddedClients { get; } = new();
            public List<ulong> ClosedClients { get; } = new();

            public void InitServer(IGameEngine gameEngine) { }

            public Coroutine StartGameCoroutine(IEnumerator routine)
            {
                GameLoop = routine;
                return null;
            }

            public void AddClientToGameRoom(ulong clientId) => AddedClients.Add(clientId);

            public void UpdateStartTime() { }

            public void UpdateTeamInfo(TeamIndex teamIdx) { }

            public void UpdatePlayerControlState(GameDetails_PlayerContract player) { }

            public void CloseGameRoom(ulong clientId) => ClosedClients.Add(clientId);

            public void Close() { }

            public void Dispose() { }
        }
    }
}
