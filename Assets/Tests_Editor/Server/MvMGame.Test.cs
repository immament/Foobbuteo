// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Imma.Common;
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

namespace Tests.Editor.Server
{
    [Category("MvM")]
    public class MvMGameTests
    {
        private const int HomeUserId = 8101;
        private const int AwayUserId = 8102;
        private const ulong HomeClientId = 101;
        private const ulong AwayClientId = 102;
        private const int HomeTeamId = 224;
        private const int AwayTeamId = 225;
        private static readonly TeamKey HomeKey = new(SourceGame.Footstar, TeamType.Club, HomeTeamId);
        private static readonly TeamKey AwayKey = new(
            SourceGame.Footstar,
            TeamType.NationalTeam,
            AwayTeamId,
            AgeCategory.U18
        );

        private UserRegistry m_users;
        private MockGamesRepository m_repository;
        private ServerUser m_home;
        private ServerUser m_away;
        private ServerGameState m_game;
        private FoobbuteoGameEngine m_engine;
        private FoobbuteoServerState m_server;
        private TestRoom m_room;

        [SetUp]
        public async Task SetUp()
        {
            ServiceLocator.Initialize();
            m_users = new UserRegistry();
            ServiceLocator.Current.Register<IUserRegistry>(m_users);
            ServiceLocator.Current.Register<IGamePersistenceRepository>(new MockGamePersistenceRepository());
            m_home = CreateUser(HomeClientId, HomeUserId, HomeKey);
            m_away = CreateUser(AwayClientId, AwayUserId, AwayKey);
            Assert.IsNull(m_users.RegisterUser(m_home), "The home manager must authenticate.");
            Assert.IsNull(m_users.RegisterUser(m_away), "The away manager must authenticate.");
            m_repository = new MockGamesRepository(
                () => new List<ServerGameState>(),
                () => DateTime.UtcNow.AddHours(-1),
                new[] { CreateRoster(HomeKey), CreateRoster(AwayKey) }
            );
            ServiceLocator.Current.Register<IGamesRepository>(m_repository);
            int gameId = await m_repository.CreateGameAsync(
                new NewGameOptions(gameType: FoobbuteoGameType.MvM, team: HomeKey),
                m_home
            );
            m_game = (await m_repository.GetGameListAsync(240, 0)).Single();
            m_room = new TestRoom(gameId);
            m_engine = new FoobbuteoGameEngine(null, m_room, m_game);
            m_server = new FoobbuteoServerState(m_users);
            m_server.AddGame(m_engine, notify: false);
            m_server.AddClientToGame(HomeClientId, gameId, m_home);
            m_server.AddClientToGame(AwayClientId, gameId, m_away);
        }

        [TearDown]
        public void TearDown()
        {
            m_server?.Dispose();
            m_engine?.Dispose();
            m_repository?.Dispose();
            ServiceLocator.ClearAll();
        }

        [Test]
        public void Creation_LoadsConfiguredRosterWithoutClaimingManagerPlayer()
        {
            Assert.AreEqual(
                GameSlot.ForManager(m_game.GameId, TeamIndex.Home),
                m_home.GameSlot.Value,
                "The creator must receive a manager slot even without a personal player ID."
            );
            Assert.AreEqual(HomeKey, m_game.HomeTeam.TeamKey, "Creation must retain the complete selected key.");
            Assert.AreEqual("Team (224)", m_game.HomeTeam.Name, "The match must display the selected team.");
            Assert.IsTrue(
                m_game.HomeTeam.Players.All(player => player.UserId == 0 && !player.IsCaptain),
                "The actual roster must contain no claimed manager or pitch captain."
            );
            Assert.AreEqual(
                CreateRoster(HomeKey).Players[0].PlayerId,
                m_game.HomeTeam.Players[0].PlayerId,
                "Roster identities must come from configured source snapshots."
            );
            Assert.AreEqual(
                CreateRoster(HomeKey).Players[0].ShootingSkill,
                m_game.HomeTeam.Players[0].ShootingSkill,
                "Roster skills must come from configured source snapshots."
            );
        }

        [Test]
        public void PendingGame_PastScheduledTime_WaitsAndRemainsDiscoverable()
        {
            int started = 0;
            m_engine.GameStarted += () => started++;
            m_engine.InitGame();
            Assert.IsTrue(m_room.Routine.MoveNext(), "A pending MvM loop must wait for the opponent.");
            Assert.AreEqual(0, started, "No start event may fire while the away manager is missing.");
            Assert.IsFalse(m_game.CanStart, "One manager is insufficient to start MvM.");
            Assert.AreEqual(FoobbuteoGameStatus.Pending, m_game.Status, "Waiting must preserve pending status.");
            Assert.AreEqual(
                1,
                m_repository.GetGameListAsync(0, 0).Result.Count,
                "A waiting match must survive the scheduled-time display cutoff."
            );
        }

        [Test]
        public async Task Join_LoadsMixedTypeAndAgeTeamAndRestoresManagerSlot()
        {
            await m_engine.JoinMvMGameAsync(m_away, AwayKey, m_repository);
            m_server.AddClientToGame(AwayClientId, m_game.GameId, m_away);

            Assert.AreEqual(AwayKey, m_game.AwayTeam.TeamKey, "Joining must preserve a different team type and age.");
            Assert.AreEqual("Team U18 (NT)", m_game.AwayTeam.Name, "Away team labels must include age and type.");
            Assert.AreEqual(
                GameSlot.ForManager(m_game.GameId, TeamIndex.Away),
                m_away.GameSlot.Value,
                "Joining must assign the away manager without claiming a player."
            );
            Assert.IsTrue(m_game.CanStart, "Both assigned managers must make the scheduled match ready.");
            Assert.AreEqual(1, m_room.TeamUpdates, "Successful joining must publish the new away identity.");
        }

        [Test]
        public async Task ConcurrentJoins_OnlyOneManagerWins()
        {
            var challenger = CreateUser(103, AwayUserId + 1, AwayKey);
            challenger.EnterRoom(m_game.GameId);
            Task<bool> first = TryJoin(m_away);
            Task<bool> second = TryJoin(challenger);
            bool[] results = await Task.WhenAll(first, second);
            Assert.AreEqual(1, results.Count(success => success), "Competing joins must produce exactly one winner.");
            Assert.AreEqual(AwayUserId, m_game.AwayTeam.Id, "The first persisted manager must retain the away side.");

            async Task<bool> TryJoin(ServerUser user)
            {
                try
                {
                    await m_engine.JoinMvMGameAsync(user, AwayKey, m_repository);
                    return true;
                }
                catch (AppException)
                {
                    return false;
                }
            }
        }

        [Test]
        public void Join_UnownedMirrorOrUnsupportedTeam_IsRejected()
        {
            Assert.IsFalse(
                m_game.CanJoinAsManager(m_away, HomeKey, out _),
                "A user must not submit another manager's team."
            );
            m_away.RefreshTeams(new[] { new ManagedTeam(HomeKey, "Shared club") });
            Assert.IsFalse(
                m_game.CanJoinAsManager(m_away, HomeKey, out _),
                "Even an eligible manager must not use the same complete key as the home team."
            );
            var future = new TeamKey((SourceGame)2, TeamType.Club, AwayTeamId);
            m_away.RefreshTeams(new[] { new ManagedTeam(future, "Future team") });
            Assert.IsFalse(
                m_game.CanJoinAsManager(m_away, future, out _),
                "The future source must remain unavailable until its implementation exists."
            );
        }

        [Test]
        public void Join_FailedPersistence_DoesNotPublishOrAssignAwaySide()
        {
            Assert.ThrowsAsync<RepositoryUnavailableException>(
                () => m_engine.JoinMvMGameAsync(m_away, AwayKey, new FailingRepository()),
                "Repository failure must propagate as a failed join."
            );
            Assert.AreEqual(0, m_game.AwayTeam.Id, "Failed joining must leave the away side unassigned.");
            Assert.IsFalse(m_away.GameSlot.HasValue, "Failure must not assign a manager slot.");
            Assert.AreEqual(0, m_room.TeamUpdates, "Failure must not publish new team identity.");
        }

        [Test]
        public void ManagerCommands_ControlAllOwnPlayersButRejectOpponentAndSpectator()
        {
            m_game.StartGame();
            m_game.PlayerMoveStage(0, 20);
            for (byte index = 1; index <= 11; index++)
                Assert.AreSame(
                    m_game.GetPlayer(TeamIndex.Home, index),
                    m_game.GetPlayerForCommand(new PlayerKey(TeamIndex.Home, index), m_home, out _),
                    $"The home manager must control own pitch player {index} without a claim."
                );
            Assert.IsNull(
                m_game.GetPlayerForCommand(new PlayerKey(TeamIndex.Away, 2), m_home, out _),
                "A manager must not control the opposing team."
            );
            Assert.IsNull(
                m_game.GetPlayerForCommand(new PlayerKey(TeamIndex.Home, 2), m_away, out _),
                "An unassigned spectator must not submit commands."
            );
            m_home.RestoreGameSlot(GameSlot.ForPlayer(m_game.GameId, new PlayerKey(TeamIndex.Home, 1)));
            Assert.IsNull(
                m_game.GetPlayerForCommand(new PlayerKey(TeamIndex.Home, 2), m_home, out _),
                "MvM must reject a forged player assignment even for the manager's user ID."
            );
        }

        [TestCase(UserRoles.Player)]
        [TestCase(UserRoles.Admin)]
        public void MvMClaims_AreRejectedForEveryRole(UserRoles role)
        {
            var user = new ServerUser(103, AwayUserId + 1, 9001, "Spectator", role, m_game.GameId);
            Assert.IsFalse(
                m_game.CanClaimPlayer(user, new PlayerKey(TeamIndex.Home, 2), out _),
                "MvM claim rejection must apply to players and admins alike."
            );
            Assert.Throws<AppException>(
                () => m_game.ClaimPlayer(user, new PlayerKey(TeamIndex.Home, 2)),
                "Calling the mutation directly must enforce the same claim restriction."
            );
        }

        [Test]
        public void PendingDepartureAndTakeover_RetainAssignmentAndRefreshTeams()
        {
            Assert.IsTrue(
                m_server.RemoveClientFromGameRoom(HomeClientId, m_game.GameId),
                "A manager may leave the pending room."
            );
            Assert.IsTrue(m_home.GameSlot.Value.IsManager, "Pending departure must retain participation.");
            m_users.UnregisterUserOnDisconnect(HomeClientId);
            var replacement = CreateUser(104, HomeUserId, AwayKey);
            Assert.IsNull(m_users.RegisterUser(replacement), "Reauthentication must replace the disconnected session.");
            m_server.OnLobbyHandshake(104, gamesReady: true);
            Assert.AreEqual(m_game.GameId, m_home.InGameRoomId, "Handshake must reopen the assigned room.");
            Assert.AreEqual(
                GameSlot.ForManager(m_game.GameId, TeamIndex.Home),
                m_home.GameSlot.Value,
                "Reconnect must retain the manager's original side."
            );
            Assert.AreEqual(AwayKey, m_home.Teams.Single().Key, "Reauthentication must refresh eligible teams.");
        }

        [Test]
        public void OngoingLocksAndDeletion_RespectManagerParticipation()
        {
            m_game.StartGame();
            Assert.IsFalse(
                GameRoomLeavePolicy.CanLeave(m_home, m_game.GameId, m_game.Status),
                "An ongoing assigned manager must be locked to their room."
            );
            Assert.IsTrue(
                GameRoomLeavePolicy.CanLeave(m_away, m_game.GameId, m_game.Status),
                "An unassigned spectator must be able to leave."
            );
            Assert.IsTrue(
                m_server.HasParticipation(HomeUserId),
                "Participation must include a manager without player claims."
            );
            Assert.IsTrue(m_server.DeleteGame(m_game.GameId), "Deletion must remove the active match.");
            Assert.IsFalse(m_home.GameSlot.HasValue, "Deletion must clear manager participation.");
            Assert.AreEqual(0, m_home.InGameRoomId, "Deletion must remove room membership.");
        }

        [Test]
        public void MissingMockRoster_RejectsCreationWithoutGeneratedReplacement()
        {
            using var repository = new MockGamesRepository(() => new List<ServerGameState>());
            Assert.ThrowsAsync<RepositoryUnavailableException>(
                () =>
                    repository.CreateGameAsync(
                        new NewGameOptions(gameType: FoobbuteoGameType.MvM, team: HomeKey),
                        m_home
                    ),
                "Assigned mock teams must load configured snapshots rather than generated players."
            );
            Assert.AreEqual(
                0,
                repository.GetGameListAsync(240, 0).Result.Count,
                "A rejected creation must not persist a partial match."
            );
        }

        [Test]
        public void MvMTurnClosure_NeverAssignsPitchCaptain()
        {
            var tracker = new PlayerActivityTracker(m_game, m_users, 3);
            Assert.IsNull(
                tracker.FindCaptainReplacement(m_game.HomeTeam),
                "MvM must retain manager ownership without electing a pitch captain."
            );
            Assert.IsTrue(
                m_game.HomeTeam.Players.All(player => player.UsesAutomaticControl),
                "Unclaimed source-roster players must retain automatic fallback."
            );
        }

        [Test]
        public async Task ParticipationGate_PreventsPvPClaimRacingWithManagerAssignment()
        {
            using var server = new FoobbuteoMainServer();
            typeof(FoobbuteoMainServer)
                .GetField("m_serverState", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(server, m_server);
            int pvpGameId = m_game.GameId + 1;
            var pvp = ServerGameState.CreateForTest(
                pvpGameId,
                DateTime.UtcNow,
                ServerGameState.CreateUnassignedTeam(pvpGameId, TeamIndex.Home, ShirtColorEnum.Blue),
                ServerGameState.CreateUnassignedTeam(pvpGameId, TeamIndex.Away, ShirtColorEnum.Red)
            );
            using var engine = new FoobbuteoGameEngine(server, new TestRoom(pvpGameId), pvp);
            m_away.EnterRoom(pvpGameId);
            await server.WaitForParticipationChangeAsync();
            Task<FoobbuteoPlayerState> claim = engine.UserClaimPlayerAsync(
                AwayClientId,
                new PlayerKey(TeamIndex.Home, 2)
            );
            Assert.IsFalse(claim.IsCompleted, "PvP claims must wait while a creation/join assignment is in flight.");
            m_away.RestoreGameSlot(GameSlot.ForManager(m_game.GameId, TeamIndex.Away));
            server.ReleaseParticipationChange();
            try
            {
                await claim;
                Assert.Fail("A completed manager assignment must reject the competing PvP claim.");
            }
            catch (AppException)
            {
                Assert.AreEqual(
                    0,
                    pvp.GetPlayer(TeamIndex.Home, 2).UserId,
                    "A rejected competing claim must leave the pitch player unclaimed."
                );
                Assert.IsTrue(
                    m_away.GameSlot.Value.IsManager,
                    "The competing claim must not overwrite manager participation."
                );
            }
        }

        [Test]
        public async Task Lineup_LoadsSeparateSquadAndReplacesGoalkeeperWithoutClaimingPlayer()
        {
            var loaded = await m_engine.PrepareMvMLineupAsync(HomeClientId, null, null, false);
            Assert.IsTrue(loaded.Success, "The assigned manager must load the saved squad.");
            Assert.AreEqual(13, loaded.Squad.Length, "The squad must include two reserves separately from the XI.");
            var desired = loaded.Lineup.ToArray();
            desired[0] = CreateRoster(HomeKey).Reserves[0].PlayerId;

            var saved = await m_engine.PrepareMvMLineupAsync(HomeClientId, loaded.Lineup, desired, true);

            Assert.IsTrue(saved.Success, "A reserve may take the goalkeeper slot.");
            Assert.AreEqual(
                desired[0],
                m_game.HomeTeam.GetGk().PlayerId,
                "The reserve must occupy goalkeeper slot one."
            );
            Assert.AreEqual(
                PlayerModeEnum.GkSave,
                m_game.HomeTeam.GetGk().PlayerMode,
                "The destination determines goalkeeper mode."
            );
            Assert.AreEqual(
                9,
                m_game.HomeTeam.GetGk().ShootingSkill,
                "Player skills must follow the reserve identity."
            );
            Assert.AreEqual(11, m_game.HomeTeam.Players.Count, "Reserves must never expand the active collection.");
            Assert.IsTrue(
                m_game.HomeTeam.Players.All(player => player.UserId == 0 && !player.IsCaptain),
                "Changing the XI must not introduce user claims or captaincy."
            );
            var reloaded = await m_engine.PrepareMvMLineupAsync(HomeClientId, null, null, false);
            CollectionAssert.AreEqual(desired, reloaded.Lineup, "Reopening must retain the applied lineup.");
            Assert.IsTrue(
                reloaded.Squad.Any(player => player.PlayerId == loaded.Lineup[0]),
                "The displaced goalkeeper must remain available in the squad."
            );
        }

        [Test]
        public async Task Lineup_SpectatorAndForeignPlayerAreRejectedWithoutChangingTeam()
        {
            var spectator = await m_engine.PrepareMvMLineupAsync(AwayClientId, null, null, false);
            Assert.IsFalse(spectator.Success, "A spectator must not load a manager's squad.");
            Assert.IsEmpty(spectator.Squad, "Unauthorized responses must not expose reserves.");
            var loaded = await m_engine.PrepareMvMLineupAsync(HomeClientId, null, null, false);
            var desired = loaded.Lineup.ToArray();
            desired[2] = CreateRoster(AwayKey).Players[2].PlayerId;
            var rejected = await m_engine.PrepareMvMLineupAsync(HomeClientId, loaded.Lineup, desired, true);
            Assert.IsFalse(rejected.Success, "A player from another squad must be rejected.");
            StringAssert.Contains("position 3", rejected.Error, "The error must identify the invalid assignment.");
            CollectionAssert.AreEqual(
                loaded.Lineup,
                rejected.Lineup,
                "Rejected changes must preserve the original XI."
            );
        }

        [Test]
        public async Task Lineup_StaleDraftIsRejectedAndAlreadyAppliedRetrySucceeds()
        {
            var loaded = await m_engine.PrepareMvMLineupAsync(HomeClientId, null, null, false);
            var desired = loaded.Lineup.Reverse().ToArray();
            Assert.IsTrue(
                (await m_engine.PrepareMvMLineupAsync(HomeClientId, loaded.Lineup, desired, true)).Success,
                "The first valid draft must apply."
            );
            Assert.IsTrue(
                (await m_engine.PrepareMvMLineupAsync(HomeClientId, loaded.Lineup, desired, true)).Success,
                "Retrying the accepted lineup must be idempotent."
            );
            var stale = await m_engine.PrepareMvMLineupAsync(HomeClientId, loaded.Lineup, loaded.Lineup, true);
            Assert.IsFalse(stale.Success, "A different draft based on an old XI must be rejected.");
            CollectionAssert.AreEqual(desired, stale.Lineup, "A stale response must return the authoritative XI.");
        }

        [Test]
        public async Task Lineup_SaveInFlightBlocksKickoffAndFailureLeavesRosterIntact()
        {
            await m_engine.JoinMvMGameAsync(m_away, AwayKey, m_repository);
            var loaded = await m_engine.PrepareMvMLineupAsync(HomeClientId, null, null, false);
            var blocking = new BlockingLineupRepository(m_repository);
            ServiceLocator.Current.Register<IGamesRepository>(blocking, throwIfRegistered: false);
            var desired = loaded.Lineup.Reverse().ToArray();
            var pending = m_engine.PrepareMvMLineupAsync(HomeClientId, loaded.Lineup, desired, true);
            Assert.IsFalse(pending.IsCompleted, "The repository must hold the save before publication.");
            Assert.IsFalse(
                m_engine.TryStartGame(DateTimeOffset.UtcNow.AddDays(1)),
                "Kickoff must wait while a lineup save owns the persistence gate."
            );
            CollectionAssert.AreEqual(
                loaded.Lineup,
                m_game.HomeTeam.Players.Select(player => player.PlayerId),
                "An uncommitted save must not mutate the active team."
            );
            blocking.Completion.SetException(new RepositoryUnavailableException("Offline"));
            Assert.ThrowsAsync<RepositoryUnavailableException>(
                async () => await pending,
                "Persistence failure must be surfaced to the RPC boundary."
            );
            CollectionAssert.AreEqual(
                loaded.Lineup,
                m_game.HomeTeam.Players.Select(player => player.PlayerId),
                "A failed save must leave the XI unchanged."
            );
            Assert.IsTrue(
                m_engine.TryStartGame(DateTimeOffset.UtcNow.AddDays(1)),
                "A failed save must release the gate so kickoff can proceed."
            );
            var late = await m_engine.PrepareMvMLineupAsync(HomeClientId, loaded.Lineup, desired, true);
            Assert.IsFalse(late.Success, "Once kickoff wins, further lineup changes must fail.");
        }

        [Test]
        public async Task Lineup_CompletedSavePublishesBeforeKickoff()
        {
            await m_engine.JoinMvMGameAsync(m_away, AwayKey, m_repository);
            var loaded = await m_engine.PrepareMvMLineupAsync(HomeClientId, null, null, false);
            var blocking = new BlockingLineupRepository(m_repository);
            ServiceLocator.Current.Register<IGamesRepository>(blocking, throwIfRegistered: false);
            var desired = loaded.Lineup.Reverse().ToArray();
            var pending = m_engine.PrepareMvMLineupAsync(HomeClientId, loaded.Lineup, desired, true);
            Assert.IsFalse(
                m_engine.TryStartGame(DateTimeOffset.UtcNow.AddDays(1)),
                "Kickoff must not overtake a pending successful save."
            );
            blocking.Completion.SetResult(true);
            var saved = await pending;
            Assert.IsTrue(saved.Success, "A completed persistence operation must accept the XI.");
            Assert.IsTrue(
                m_engine.TryStartGame(DateTimeOffset.UtcNow.AddDays(1)),
                "Kickoff must proceed after the accepted lineup is installed."
            );
            CollectionAssert.AreEqual(
                desired,
                m_game.HomeTeam.Players.Select(player => player.PlayerId),
                "The match must start with the successfully saved lineup."
            );
        }

        [TestCase(-20)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(10)]
        public void PreparationWindow_RetainsLaterStartsAndExtendsEarlierStarts(int remainingMinutes)
        {
            var joinedAt = new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
            var existing = joinedAt.AddMinutes(remainingMinutes);
            var expected = joinedAt.AddMinutes(
                Math.Max(remainingMinutes, MvMPreparationRules.MinimumMvMPreparationMinutes)
            );
            Assert.AreEqual(
                expected,
                MvMPreparationRules.GetStart(existing, joinedAt),
                "An opponent must receive the minimum preparation window without shortening a later start."
            );
        }

        [Test]
        public async Task Join_RetryKeepsPersistedStartAndKickoffHonorsRescheduledDeadline()
        {
            var joined = await m_repository.JoinMvMGameAsync(m_game.GameId, AwayKey, m_away);
            var repeated = await m_repository.JoinMvMGameAsync(m_game.GameId, AwayKey, m_away);
            Assert.AreEqual(
                joined.StartDateTime,
                repeated.StartDateTime,
                "Retrying a join must not extend the saved deadline."
            );
            m_game.RescheduleStart(DateTimeOffset.UtcNow.AddMinutes(MvMPreparationRules.MinimumMvMPreparationMinutes));
            Assert.IsFalse(
                m_engine.TryStartGame(m_game.StartDateTime.AddSeconds(-1)),
                "The game must wait until the rescheduled deadline."
            );
            Assert.IsTrue(
                m_engine.TryStartGame(m_game.StartDateTime),
                "Both managers may start exactly at the deadline."
            );
        }

        private sealed class BlockingLineupRepository : IGamesRepository
        {
            private readonly IGamesRepository m_inner;
            internal readonly TaskCompletionSource<bool> Completion = new();

            internal BlockingLineupRepository(IGamesRepository inner) => m_inner = inner;

            public Task<GameDetails_PlayerContract[]> LoadMvMSquadAsync(int gameId, TeamIndex team) =>
                m_inner.LoadMvMSquadAsync(gameId, team);

            public async Task SaveMvMLineupAsync(
                int gameId,
                TeamIndex team,
                int managerId,
                int[] expected,
                int[] desired
            )
            {
                await Completion.Task;
                await m_inner.SaveMvMLineupAsync(gameId, team, managerId, expected, desired);
            }

            public Task<MvMJoinState> JoinMvMGameAsync(int gameId, TeamKey team, GameUser manager) =>
                m_inner.JoinMvMGameAsync(gameId, team, manager);

            public Task<int> CreateGameAsync(NewGameOptions options, GameUser creator) =>
                m_inner.CreateGameAsync(options, creator);

            public Task<List<ServerGameState>> GetGameListAsync(int minutesToDisplay = 240, int minId = 0) =>
                m_inner.GetGameListAsync(minutesToDisplay, minId);

            public void Dispose() { }
        }

        private static ServerUser CreateUser(ulong client, int user, TeamKey key) =>
            new(client, user, 0, "Manager", UserRoles.Player, teams: new[] { new ManagedTeam(key, "Team") });

        private static MockTeamRoster CreateRoster(TeamKey key) =>
            new()
            {
                Team = key,
                Reserves = Enumerable
                    .Range(12, 2)
                    .Select(index =>
                        ServerPlayerState
                            .CreateForTest(
                                1,
                                TeamIndex.Home,
                                2,
                                playerId: key.TeamId * 100 + index,
                                name: $"Reserve player {index}",
                                position: Vector2.zero,
                                shootingSkill: 9
                            )
                            .ToContract()
                    )
                    .ToArray(),
                Players = Enumerable
                    .Range(1, 11)
                    .Select(index =>
                        ServerPlayerState
                            .CreateForTest(
                                1,
                                TeamIndex.Home,
                                (byte)index,
                                playerId: key.TeamId * 100 + index,
                                name: $"Source player {index}",
                                position: Vector2.zero,
                                shootingSkill: 9
                            )
                            .ToContract()
                    )
                    .ToArray(),
            };

        private sealed class FailingRepository : IGamesRepository
        {
            public Task<Imma.Foobbuteo.Shared.Game.Contract.GameDetails_PlayerContract[]> LoadMvMSquadAsync(
                int gameId,
                TeamIndex team
            ) => throw new System.NotSupportedException();

            public Task SaveMvMLineupAsync(int gameId, TeamIndex team, int managerId, int[] expected, int[] desired) =>
                throw new System.NotSupportedException();

            public Task<MvMJoinState> JoinMvMGameAsync(int gameId, TeamKey team, GameUser manager) =>
                Task.FromException<MvMJoinState>(new RepositoryUnavailableException("Offline"));

            public Task<int> CreateGameAsync(NewGameOptions options, GameUser creator) =>
                throw new NotSupportedException();

            public Task<List<ServerGameState>> GetGameListAsync(int minutesToDisplay = 240, int minId = 0) =>
                throw new NotSupportedException();

            public void Dispose() { }
        }

        private sealed class TestRoom : IServerGameRoomNB
        {
            public int GameId { get; }
            internal int TeamUpdates { get; private set; }
            internal IEnumerator Routine { get; private set; }

            internal TestRoom(int gameId) => GameId = gameId;

            public void InitServer(IGameEngine engine) { }

            public Coroutine StartGameCoroutine(IEnumerator routine)
            {
                Routine = routine;
                return null;
            }

            public void AddClientToGameRoom(ulong clientId) { }

            public void UpdateStartTime() { }

            public void UpdateTeamInfo(TeamIndex teamIdx) => TeamUpdates++;

            public void UpdatePlayerControlState(GameDetails_PlayerContract player) { }

            public void CloseGameRoom(ulong clientId) { }

            public void Close() { }

            public void Dispose() { }
        }
    }
}
