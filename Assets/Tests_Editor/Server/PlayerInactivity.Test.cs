// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Imma.Common;
using Imma.Foobbuteo.Common;
using Imma.Foobbuteo.Server;
using Imma.Foobbuteo.Server.Game;
using Imma.Foobbuteo.Server.Game.Model;
using Imma.Foobbuteo.Server.Game.Persistence;
using Imma.Foobbuteo.Server.Model;
using Imma.Foobbuteo.Server.Repos;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.Server
{
    [Category("PlayerInactivity")]
    public class PlayerInactivityTests
    {
        private const int GameId = 7601;
        private const int CaptainUserId = 8101;
        private const int TeammateUserId = 8102;
        private const ulong CaptainClientId = 9101;
        private const int CurrentTurn = 3;
        private const int Threshold = 3;
        private const byte CaptainNumber = 9;
        private const byte TeammateNumber = 3;
        private UserRegistry m_users;
        private ServerGameState m_game;
        private FoobbuteoGameEngine m_engine;
        private FoobbuteoServerState m_server;
        private RecordingRoom m_room;
        private RecordingRepository m_repository;
        private PlayerActivityTracker m_activity;
        private ServerUser m_captain;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Initialize();
            m_users = new UserRegistry();
            m_repository = new RecordingRepository();
            ServiceLocator.Current.Register<IUserRegistry>(m_users);
            ServiceLocator.Current.Register<IGamePersistenceRepository>(m_repository);
            var home = new ServerTeamState(
                TeamIndex.Home,
                CaptainUserId,
                "Original captain",
                default,
                CreatePlayers(TeamIndex.Home)
            );
            var away = new ServerTeamState(TeamIndex.Away, 0, "Away", default, CreatePlayers(TeamIndex.Away));
            m_game = ServerGameState.CreateForTest(
                GameId,
                new DateTime(2026, 10, 13),
                home,
                away,
                FoobbuteoGameStatus.Ongoing,
                turn: CurrentTurn
            );
            m_game.MoveBall(PitchUtils.PitchBounds.Center);
            m_game.PlayerMoveStage(0f, 20f);
            m_captain = AddUser(CaptainNumber, CaptainUserId, CaptainClientId, isCaptain: true);
            m_room = new RecordingRoom();
            m_engine = new FoobbuteoGameEngine(null, m_room, m_game, TimeSpan.Zero);
            Captain.RegisterActivity(0);
            m_server = new FoobbuteoServerState(m_users);
            m_server.AddGame(m_engine, notify: false);
            m_activity = new PlayerActivityTracker(m_game, m_users, Threshold);
        }

        [TearDown]
        public void TearDown()
        {
            m_server.Dispose();
            m_engine.Dispose();
            ServiceLocator.ClearAll();
        }

        [TestCase(2, false)]
        [TestCase(3, true)]
        [TestCase(4, true)]
        public void CloseInput_ConsecutiveMissedTurns_UsesConfiguredThreshold(int turn, bool expectedInactive)
        {
            ServerPlayerState player = Captain;
            player.CloseInput(turn, Threshold);

            Assert.AreEqual(
                expectedInactive,
                player.IsInactive,
                $"Inactivity must match the threshold at turn {turn}."
            );
            Assert.AreEqual(CaptainUserId, player.UserId, "Inactivity must preserve player ownership.");
            Assert.IsTrue(m_captain.GameSlot.HasValue, "Inactivity must preserve the owner's assignment.");
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Configuration_NonPositiveThreshold_IsRejected(int threshold)
        {
            Assert.Throws<InvalidOperationException>(
                () => new ServerConfig { InactiveAfterTurns = threshold }.GetInactiveAfterTurns(),
                "A nonpositive threshold must be rejected."
            );
        }

        [Test]
        public void Configuration_DefaultThreshold_IsThreeTurns()
        {
            Assert.AreEqual(
                Threshold,
                new ServerConfig().GetInactiveAfterTurns(),
                "Missing configuration must allow three consecutive missed input turns."
            );
        }

        [Test]
        public void ActivityBaseline_RequiresConsecutiveMissedTurnsAfterLatestAction()
        {
            Captain.RegisterActivity(2);
            Captain.CloseInput(4, Threshold);
            Assert.IsFalse(Captain.IsInactive, "Two missed turns after an accepted action must remain active.");
            Captain.CloseInput(5, Threshold);
            Assert.IsTrue(Captain.IsInactive, "The third consecutive missed turn must become inactive.");
        }

        [TestCase(PlayerActionEnum.Move)]
        [TestCase(PlayerActionEnum.LowPass)]
        [TestCase(PlayerActionEnum.HighPass)]
        [TestCase(PlayerActionEnum.Shot)]
        public async Task AcceptedOwnAction_ReactivatesSender(PlayerActionEnum action)
        {
            Captain.CloseInput(CurrentTurn, Threshold);
            Captain.Server_PlayerMove(new Vector2(450f, 210f));
            Captain.HasBall = action != PlayerActionEnum.Move;
            Vector2 target = action == PlayerActionEnum.HighPass ? new Vector2(220f, 210f) : new Vector2(460f, 210f);

            PlayerCommandResponse response = await m_engine.AddPlayerMoveAsync(
                Command(Captain, action, target),
                CaptainClientId
            );

            Assert.IsTrue(response.IsAccepted, $"The valid {action} command must be accepted: {response.Reason}");
            Assert.IsFalse(Captain.IsInactive, "Any accepted own action must restore activity immediately.");
            Assert.AreEqual(CurrentTurn, Captain.LastActivityTurn, "Activity must record the accepted command turn.");
        }

        [Test]
        public async Task CaptainCommandsInactiveTeammate_OnlySenderBecomesActive()
        {
            ServerUser teammate = AddUser(TeammateNumber, TeammateUserId, CaptainClientId + 1);
            ServerPlayerState player = m_game.GetPlayer(TeamIndex.Home, TeammateNumber);
            Captain.CloseInput(CurrentTurn, Threshold);
            player.CloseInput(CurrentTurn, Threshold);

            PlayerCommandResponse response = await m_engine.AddPlayerMoveAsync(
                Command(player, PlayerActionEnum.Move, player.Position + Vector2.right),
                CaptainClientId
            );

            Assert.IsTrue(response.IsAccepted, "A captain must be allowed to command an inactive teammate.");
            Assert.IsFalse(Captain.IsInactive, "Steering a teammate must count as the captain's activity.");
            Assert.IsTrue(player.IsInactive, "Another user's command must not reactivate the target owner.");
            Assert.AreEqual(0, player.LastActivityTurn, "The target owner's activity baseline must remain unchanged.");
            Assert.IsTrue(teammate.GameSlot.HasValue, "Captain control must preserve the target user's slot.");
        }

        [Test]
        public async Task CaptainChangesBotMode_CountsAsUserActivity()
        {
            Captain.CloseInput(CurrentTurn, Threshold);
            ServerPlayerState bot = m_game.GetPlayer(TeamIndex.Home, 2);

            await m_engine.ChangePlayerModeAsync(
                new PlayerModeCommand(CurrentTurn, TeamIndex.Home, 2, bot.PlayerId, PlayerModeEnum.Confrontation),
                CaptainClientId
            );

            Assert.IsFalse(Captain.IsInactive, "An accepted defence-mode change must reactivate its sender.");
            Assert.AreEqual(CurrentTurn, Captain.LastActivityTurn, "Mode changes must record user activity.");
        }

        [Test]
        public async Task RejectedCommand_DoesNotResetActivity()
        {
            Captain.CloseInput(CurrentTurn, Threshold);
            PlayerCommandResponse response = await m_engine.AddPlayerMoveAsync(
                Command(Captain, PlayerActionEnum.Move, new Vector2(-100f, -100f)),
                CaptainClientId
            );

            Assert.IsFalse(response.IsAccepted, "An outside-pitch move must be rejected.");
            Assert.IsTrue(Captain.IsInactive, "Rejected commands must not restore activity.");
            Assert.AreEqual(0, m_repository.CommandCalls, "Rejected commands must not reach persistence.");
        }

        [Test]
        public async Task PersistenceFailure_RestoresActionPointsAndPreservesInactivity()
        {
            Captain.CloseInput(CurrentTurn, Threshold);
            float points = Captain.ActionPoints;
            m_repository.Fail = true;

            PlayerCommandResponse response = await m_engine.AddPlayerMoveAsync(
                Command(Captain, PlayerActionEnum.Move, Captain.Position + Vector2.right),
                CaptainClientId
            );

            Assert.IsFalse(response.IsAccepted, "A command must fail when its transaction cannot be saved.");
            Assert.AreEqual(points, Captain.ActionPoints, "Failed persistence must restore the command's action cost.");
            Assert.IsTrue(Captain.IsInactive, "Failed persistence must not reactivate the sender.");
            Assert.AreEqual(0, m_room.PlayerUpdates.Count, "A rejected transaction must not publish activity changes.");
        }

        [Test]
        public void DisconnectAndReconnect_UpdatePitchStateWithoutChangingActivity()
        {
            m_users.UnregisterUserOnDisconnect(CaptainClientId);
            Assert.IsFalse(Captain.IsConnected, "Disconnect must immediately reach pitch state.");
            Assert.IsFalse(Captain.IsInactive, "Disconnect alone must not mark a user inactive.");
            Captain.CloseInput(CurrentTurn, Threshold);
            var reconnect = new ServerUser(
                CaptainClientId + 10,
                CaptainUserId,
                Captain.PlayerId,
                "Captain",
                UserRoles.Player
            );
            Assert.IsNull(m_users.RegisterUser(reconnect), "Reconnection must restore the registered user.");

            Assert.IsTrue(Captain.IsConnected, "Reconnection must immediately reach pitch state.");
            Assert.IsTrue(Captain.IsInactive, "Reconnection alone must preserve inactivity.");
            Assert.AreEqual(CaptainClientId + 10, Captain.ClientId, "Pitch state must use the new connection ID.");
            Assert.AreEqual(2, m_room.PlayerUpdates.Count, "Disconnect and reconnect must each publish one update.");
        }

        [Test]
        public void ConnectionTakeover_OldDisconnectCannotClearNewConnection()
        {
            var replacement = new ServerUser(
                CaptainClientId + 20,
                CaptainUserId,
                Captain.PlayerId,
                "Captain",
                UserRoles.Player
            );
            Assert.IsNull(m_users.RegisterUser(replacement), "An authorized takeover must replace the session.");
            m_users.UnregisterUserOnDisconnect(CaptainClientId);

            Assert.IsTrue(Captain.IsConnected, "The replaced session's disconnect must not disconnect the new owner.");
            Assert.AreEqual(
                CaptainClientId + 20,
                Captain.ClientId,
                "The replacement client ID must remain authoritative."
            );
        }

        [Test]
        public void DeletedGame_DetachesConnectionUpdates()
        {
            Assert.IsTrue(m_server.DeleteGame(GameId), "The test game must be removed from server state.");
            int updates = m_room.PlayerUpdates.Count;

            m_users.UnregisterUserOnDisconnect(CaptainClientId);

            Assert.AreEqual(
                updates,
                m_room.PlayerUpdates.Count,
                "Deleted games must not retain registry subscriptions or send pitch updates."
            );
        }

        [Test]
        public void CloseInput_InactiveCaptain_SelectsLowestConnectedActiveTeammate()
        {
            AddUser(2, TeammateUserId + 1, null);
            AddUser(TeammateNumber, TeammateUserId, CaptainClientId + 1);
            AddUser(6, TeammateUserId + 2, CaptainClientId + 2);
            m_game.GetPlayer(TeamIndex.Home, 2).RegisterActivity(CurrentTurn);
            m_game.GetPlayer(TeamIndex.Home, TeammateNumber).RegisterActivity(CurrentTurn);
            m_game.GetPlayer(TeamIndex.Home, 6).RegisterActivity(CurrentTurn);

            CloseInputAndReplaceCaptain();

            Assert.AreEqual(TeammateUserId, m_game.HomeTeam.Id, "Disconnected candidates must be skipped.");
            Assert.AreEqual($"User {TeammateUserId}", m_game.HomeTeam.Name, "Team name must follow the new captain.");
            Assert.IsTrue(
                m_game.GetPlayer(TeamIndex.Home, TeammateNumber).IsCaptain,
                "The lowest eligible shirt number must receive the captain flag."
            );
            Assert.IsFalse(Captain.IsCaptain, "The old captain flag must be cleared.");
        }

        [Test]
        public void CloseInput_NoEligibleTeammate_RetainsInactiveCaptain()
        {
            CloseInputAndReplaceCaptain();

            Assert.IsTrue(Captain.IsInactive, "Missing a replacement must not suppress inactivity.");
            Assert.IsTrue(Captain.IsCaptain, "Captaincy must remain when no eligible replacement exists.");
            Assert.AreEqual(CaptainUserId, m_game.HomeTeam.Id, "Team identity must remain until a replacement exists.");
        }

        [Test]
        public async Task RetainedInactiveCaptain_TeammateChangesMode_DoesNotReplaceCaptain()
        {
            AddUser(TeammateNumber, TeammateUserId, CaptainClientId + 1);
            CloseInputAndReplaceCaptain();
            ServerPlayerState teammate = m_game.GetPlayer(TeamIndex.Home, TeammateNumber);

            await m_engine.ChangePlayerModeAsync(
                new PlayerModeCommand(
                    CurrentTurn,
                    TeamIndex.Home,
                    TeammateNumber,
                    teammate.PlayerId,
                    PlayerModeEnum.Interception
                ),
                CaptainClientId + 1
            );

            Assert.AreEqual(0, m_repository.TeamSaves.Count, "Mode changes must not persist a captain change.");
            Assert.IsFalse(teammate.IsCaptain, "Mode changes must leave captain selection until turn closure.");
            Assert.IsTrue(Captain.IsCaptain, "The inactive captain must remain until a permitted replacement event.");
        }

        [Test]
        public async Task FormerCaptain_Returns_DoesNotRegainCaptaincy()
        {
            AddUser(TeammateNumber, TeammateUserId, CaptainClientId + 1);
            m_game.GetPlayer(TeamIndex.Home, TeammateNumber).RegisterActivity(CurrentTurn);
            CloseInputAndReplaceCaptain();

            await m_engine.ChangePlayerModeAsync(
                new PlayerModeCommand(
                    CurrentTurn,
                    TeamIndex.Home,
                    CaptainNumber,
                    Captain.PlayerId,
                    PlayerModeEnum.Interception
                ),
                CaptainClientId
            );

            Assert.IsFalse(Captain.IsInactive, "The former captain's accepted command must restore activity.");
            Assert.IsFalse(Captain.IsCaptain, "Returning former captains must not regain captaincy automatically.");
            Assert.AreEqual(TeammateUserId, m_game.HomeTeam.Id, "The current captain's identity must remain.");
        }

        [Test]
        public async Task Reclaim_ClearsOnlyOldSlotAndPreservesCommandsAndActionCosts()
        {
            ServerUser teammate = AddUser(TeammateNumber, TeammateUserId, CaptainClientId + 1);
            ServerPlayerState player = m_game.GetPlayer(TeamIndex.Home, TeammateNumber);
            player.CloseInput(CurrentTurn, Threshold);
            await m_engine.AddPlayerMoveAsync(
                Command(player, PlayerActionEnum.Move, player.Position + Vector2.right),
                CaptainClientId
            );
            float points = player.ActionPoints;
            const int claimantUserId = 8201;
            const ulong claimantClientId = 9201;
            var claimant = new ServerUser(
                claimantClientId,
                claimantUserId,
                8301,
                "New owner",
                UserRoles.Player,
                GameId
            );
            Assert.IsNull(m_users.RegisterUser(claimant), "The claimant must be registered in the room.");
            var notifications = new List<ServerUser>();
            m_server.UserChanged += (_, user) => notifications.Add(user);

            await m_engine.UserClaimPlayerAsync(claimantClientId, player.Slot.PlayerKey);

            Assert.IsFalse(teammate.GameSlot.HasValue, "Reclaim must release the previous owner's matching slot.");
            Assert.AreEqual(
                GameId,
                teammate.InGameRoomId,
                "The previous owner must remain in the room as a spectator."
            );
            Assert.AreEqual(player.Slot, claimant.GameSlot.Value, "The claimant must receive the existing pitch slot.");
            Assert.AreEqual(
                points,
                player.ActionPoints,
                "A reclaim must preserve action points already spent this turn."
            );
            Assert.IsFalse(player.IsInactive, "The new assignment must start active.");
            Assert.AreEqual(CurrentTurn, player.LastActivityTurn, "The claim turn must establish a fresh baseline.");
            Assert.That(
                notifications,
                Is.EqualTo(new[] { teammate }),
                "The old owner must receive one complete slot update."
            );
            var commands =
                (List<PlayerCommand>)
                    typeof(FoobbuteoGameEngine)
                        .GetField(
                            "m_commands",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
                        )
                        .GetValue(m_engine);
            Assert.AreEqual(1, commands.Count, "Reclaim must preserve the previously accepted command.");
            Assert.AreEqual(
                claimant.PlayerId,
                commands[0].PlayerId,
                "The queued command must follow the current slot identity."
            );
        }

        [Test]
        public async Task FailedReclaim_LeavesBothAssignmentsAndCaptainUnchanged()
        {
            Captain.CloseInput(CurrentTurn, Threshold);
            const ulong claimantClientId = 9201;
            var claimant = new ServerUser(claimantClientId, 8201, 8301, "New owner", UserRoles.Player, GameId);
            Assert.IsNull(m_users.RegisterUser(claimant), "The claimant must be registered.");
            m_repository.Fail = true;

            try
            {
                await m_engine.UserClaimPlayerAsync(claimantClientId, Captain.Slot.PlayerKey);
                Assert.Fail("A failed claim transaction must reject the claim.");
            }
            catch (AppException) { }

            Assert.AreEqual(CaptainUserId, Captain.UserId, "Failed persistence must preserve ownership.");
            Assert.IsTrue(m_captain.GameSlot.HasValue, "Failed persistence must preserve the previous owner's slot.");
            Assert.IsFalse(claimant.GameSlot.HasValue, "Failed persistence must not assign the claimant.");
            Assert.AreEqual(CaptainUserId, m_game.HomeTeam.Id, "Failed persistence must preserve captain identity.");
        }

        [Test]
        public async Task ReclaimedInactiveCaptain_NewOwnerUpdatesTeamIdentityAndFlags()
        {
            Captain.CloseInput(CurrentTurn, Threshold);
            const ulong claimantClientId = 9201;
            const int claimantUserId = 8201;
            var claimant = new ServerUser(
                claimantClientId,
                claimantUserId,
                8301,
                "New captain",
                UserRoles.Player,
                GameId
            );
            Assert.IsNull(m_users.RegisterUser(claimant), "The new captain must be registered in the room.");

            await m_engine.UserClaimPlayerAsync(claimantClientId, Captain.Slot.PlayerKey);

            Assert.AreEqual(
                claimantUserId,
                m_game.HomeTeam.Id,
                "Reclaiming the retained captain must update team identity."
            );
            Assert.AreEqual(claimant.Name, m_game.HomeTeam.Name, "Team name must follow the new captain.");
            Assert.IsTrue(Captain.IsCaptain, "The new owner must receive captaincy when no other candidate exists.");
            Assert.IsFalse(m_captain.GameSlot.HasValue, "The previous captain's slot must be released.");
            Assert.AreEqual(
                claimantUserId,
                m_repository.TeamSaves.Single().UserId,
                "The captain reassignment must be persisted after the claim succeeds."
            );
        }

        [Test]
        public void AutomaticPlanner_InactivePlayerWithoutCommand_GetsAiWithoutActivityCredit()
        {
            Captain.CloseInput(CurrentTurn, Threshold);
            Captain.Server_PlayerMove(new Vector2(100f, 200f));
            var commands = new List<PlayerCommand>();

            new AutomaticTurnPlanner(m_game, commands).AddAutomaticCommands();

            Assert.IsTrue(
                commands.Any(command => command.PlayerIdx == CaptainNumber && command.TeamIdx == TeamIndex.Home),
                "An inactive player without a queued human command must receive an automatic move."
            );
            Assert.IsTrue(Captain.IsInactive, "Automatic moves must not restore the owner's activity.");
            Assert.AreEqual(0, Captain.LastActivityTurn, "AI must not advance the user's activity baseline.");
        }

        [Test]
        public void AutomaticPlanner_QueuedInactivePlayerCommand_SkipsAiForThatPlayer()
        {
            Captain.CloseInput(CurrentTurn, Threshold);
            var commands = new List<PlayerCommand> { Command(Captain, PlayerActionEnum.Move, Captain.Position) };

            new AutomaticTurnPlanner(m_game, commands).AddAutomaticCommands();

            Assert.AreEqual(
                1,
                commands.Count(command => command.PlayerIdx == CaptainNumber && command.TeamIdx == TeamIndex.Home),
                "A queued human command must suppress automatic moves for that same inactive player."
            );
        }

        [Test]
        public void Permissions_OnlyCaptainCanCommandAnotherInactiveUser()
        {
            ServerUser teammate = AddUser(TeammateNumber, TeammateUserId, CaptainClientId + 1);
            Captain.CloseInput(CurrentTurn, Threshold);

            var result = m_game.GetPlayerForCommand(Captain.Slot.PlayerKey, teammate, out string reason);

            Assert.IsNull(result, "A noncaptain must not command another user's inactive player.");
            Assert.IsNotNull(reason, "Rejected control must explain the permission failure.");
        }

        [Test]
        public async Task CaptainCommandingActiveTeammate_IsRejectedWithoutActivityCredit()
        {
            AddUser(TeammateNumber, TeammateUserId, CaptainClientId + 1);
            Captain.CloseInput(CurrentTurn, Threshold);
            ServerPlayerState player = m_game.GetPlayer(TeamIndex.Home, TeammateNumber);

            PlayerCommandResponse response = await m_engine.AddPlayerMoveAsync(
                Command(player, PlayerActionEnum.Move, player.Position),
                CaptainClientId
            );

            Assert.IsFalse(response.IsAccepted, "Captains must not command active teammates.");
            Assert.IsTrue(Captain.IsInactive, "An unauthorized command must not reactivate its sender.");
        }

        [Test]
        public async Task ClaimingActivePlayer_IsRejectedBeforePersistence()
        {
            const ulong claimantClientId = 9201;
            var claimant = new ServerUser(claimantClientId, 8201, 8301, "Claimant", UserRoles.Player, GameId);
            Assert.IsNull(m_users.RegisterUser(claimant), "The claimant must be registered in the room.");

            try
            {
                await m_engine.UserClaimPlayerAsync(claimantClientId, Captain.Slot.PlayerKey);
                Assert.Fail("Active ownership must reject a takeover.");
            }
            catch (AppException) { }

            Assert.AreEqual(0, m_repository.ClaimCalls, "Active-player claims must be rejected before persistence.");
            Assert.IsFalse(claimant.GameSlot.HasValue, "Rejected claims must leave the claimant unassigned.");
        }

        [Test]
        public async Task ClaimingEmptyTeam_AssignsFirstCaptainInClaimTransaction()
        {
            const ulong claimantClientId = 9201;
            const int claimantUserId = 8201;
            var claimant = new ServerUser(
                claimantClientId,
                claimantUserId,
                8301,
                "Away captain",
                UserRoles.Player,
                GameId
            );
            Assert.IsNull(m_users.RegisterUser(claimant), "The claimant must be registered in the room.");

            await m_engine.UserClaimPlayerAsync(claimantClientId, new PlayerKey(TeamIndex.Away, 3));

            Assert.AreEqual(
                claimantUserId,
                m_game.AwayTeam.Id,
                "The first owner must become captain of an empty team."
            );
            Assert.IsTrue(
                m_game.GetPlayer(TeamIndex.Away, 3).IsCaptain,
                "The claimed slot must receive the captain flag."
            );
            Assert.AreEqual(
                claimantUserId,
                m_repository.TeamSaves.Single().UserId,
                "The first captain must be persisted in the team update."
            );
        }

        [TestCase(0)]
        [TestCase(8801)]
        public async Task TurnClosure_MissingCaptain_AssignsConnectedActiveTeammate(int captainUserId)
        {
            var info = m_game.HomeTeam.ToNetFoobbuteoTeam();
            info.Id = captainUserId;
            m_game.HomeTeam.UpdateInfo(info);
            AddUser(TeammateNumber, TeammateUserId, CaptainClientId + 1);
            var teammate = m_game.GetPlayer(TeamIndex.Home, TeammateNumber);
            teammate.RegisterActivity(CurrentTurn);
            m_game.ProcessingStarted(0f);

            await m_engine.ProcessAndPersistTurnAsync();

            Assert.AreEqual(
                TeammateUserId,
                m_game.HomeTeam.Id,
                $"Missing captain ID {captainUserId} must not prevent selecting an active teammate."
            );
            Assert.IsTrue(teammate.IsCaptain, "The selected teammate must receive captain permissions.");
            Assert.IsFalse(Captain.IsCaptain, "Any stale captain flag must be cleared.");
            Assert.AreEqual(
                TeammateUserId,
                m_repository.TeamSaves.Single().UserId,
                "The new captain identity must be persisted."
            );
        }

        [TestCase(0)]
        [TestCase(8801)]
        public void MissingCaptain_WithoutEligiblePlayer_DoesNotAssignCaptain(int captainUserId)
        {
            var team = new ServerTeamState(
                TeamIndex.Away,
                captainUserId,
                "Away",
                default,
                CreatePlayers(TeamIndex.Away)
            );

            CaptainAssignment? replacement = m_activity.FindCaptainReplacement(team);

            Assert.IsFalse(replacement.HasValue, "A team with only bots must not receive a human captain.");
            Assert.AreEqual(captainUserId, team.Id, "Selection without a candidate must preserve team identity.");
        }

        [Test]
        public async Task ClaimingPlayer_WithMissingCaptain_AssignsClaimant()
        {
            const int missingCaptainUserId = 8801;
            var info = m_game.HomeTeam.ToNetFoobbuteoTeam();
            info.Id = missingCaptainUserId;
            m_game.HomeTeam.UpdateInfo(info);
            var claimant = new ServerUser(9201, 8201, 8301, "Claimant", UserRoles.Player, GameId);
            Assert.IsNull(m_users.RegisterUser(claimant), "The claimant must be registered in the room.");

            await m_engine.UserClaimPlayerAsync(claimant.ClientId.Value, new PlayerKey(TeamIndex.Home, 2));

            Assert.AreEqual(
                claimant.UserId,
                m_game.HomeTeam.Id,
                "The incoming owner in the lowest eligible slot must replace the missing captain identity."
            );
            Assert.IsTrue(
                m_game.GetPlayer(TeamIndex.Home, 2).IsCaptain,
                "The claimant must receive captain permissions after the claim succeeds."
            );
            Assert.IsFalse(Captain.IsCaptain, "A stale captain flag must not survive the new assignment.");
            Assert.AreEqual(
                claimant.UserId,
                m_repository.TeamSaves.Single().UserId,
                "The selected claimant must be persisted as captain."
            );
        }

        [Test]
        public async Task FailedModeSave_DoesNotChangeModeActivityOrCaptain()
        {
            AddUser(TeammateNumber, TeammateUserId, CaptainClientId + 1);
            CloseInputAndReplaceCaptain();
            ServerPlayerState player = m_game.GetPlayer(TeamIndex.Home, TeammateNumber);
            PlayerModeEnum mode = player.PlayerMode;
            m_repository.Fail = true;

            try
            {
                await m_engine.ChangePlayerModeAsync(
                    new PlayerModeCommand(
                        CurrentTurn,
                        TeamIndex.Home,
                        TeammateNumber,
                        player.PlayerId,
                        PlayerModeEnum.Confrontation
                    ),
                    CaptainClientId + 1
                );
                Assert.Fail("A mode change must fail when its transaction cannot be saved.");
            }
            catch (AppException) { }

            Assert.AreEqual(mode, player.PlayerMode, "Failed persistence must preserve the prior defence mode.");
            Assert.IsTrue(player.IsInactive, "Failed persistence must not reactivate the user.");
            Assert.AreEqual(CaptainUserId, m_game.HomeTeam.Id, "Failed persistence must not replace captaincy.");
        }

        [Test]
        public void InitializeAfterLoad_UsesCompletedTurnAndClearsInactivity()
        {
            Captain.CloseInput(CurrentTurn, Threshold);
            AddUser(TeammateNumber, TeammateUserId, null);

            m_engine.InitializeAfterLoad();

            Assert.AreEqual(
                CurrentTurn,
                Captain.LastActivityTurn,
                "Loading must use the completed turn as the baseline."
            );
            Assert.IsFalse(Captain.IsInactive, "Loading must clear the assigned player's previous inactivity.");
            Assert.IsTrue(Captain.IsConnected, "Loading must refresh connected users from the registry.");
            var teammate = m_game.GetPlayer(TeamIndex.Home, TeammateNumber);
            Assert.AreEqual(
                CurrentTurn,
                teammate.LastActivityTurn,
                "Disconnected assignments also need a fresh baseline."
            );
            Assert.IsFalse(teammate.IsConnected, "Loading must preserve a disconnected assignment's connection state.");
            var bot = m_game.GetPlayer(TeamIndex.Home, 2);
            Assert.AreEqual(0, bot.LastActivityTurn, "Loading must not initialize activity for bots.");
            Assert.AreEqual(0, m_room.PlayerUpdates.Count, "Load initialization precedes network publication.");
        }

        [Test]
        public void InitializeAfterLoad_FirstIncrementCountsOneMissedTurn()
        {
            m_engine.InitializeAfterLoad();

            Assert.IsTrue(m_game.NextTurn(), "A loaded ongoing game must advance to the next input turn.");
            Captain.CloseInput(m_game.CurrentTurn, Threshold);

            Assert.AreEqual(CurrentTurn + 1, m_game.CurrentTurn, "The input turn must follow the completed turn.");
            Assert.IsFalse(Captain.IsInactive, "One missed input turn after loading must remain active.");
            Captain.CloseInput(CurrentTurn + Threshold, Threshold);
            Assert.IsTrue(Captain.IsInactive, "Three missed turns after loading must trigger inactivity.");
        }

        [Test]
        public void CloseInput_BotNeverBecomesInactive()
        {
            var bot = m_game.GetPlayer(TeamIndex.Home, 2);

            bot.CloseInput(CurrentTurn + Threshold, Threshold);

            Assert.IsFalse(bot.IsInactive, "Bots must not acquire user inactivity state.");
        }

        [Test]
        public async Task RetainedInactiveCaptain_TeammateMoves_DoesNotReplaceCaptain()
        {
            AddUser(TeammateNumber, TeammateUserId, CaptainClientId + 1);
            CloseInputAndReplaceCaptain();
            var teammate = m_game.GetPlayer(TeamIndex.Home, TeammateNumber);

            var response = await m_engine.AddPlayerMoveAsync(
                Command(teammate, PlayerActionEnum.Move, teammate.Position + Vector2.right),
                CaptainClientId + 1
            );

            Assert.IsTrue(response.IsAccepted, "The teammate's valid own move must be accepted.");
            Assert.IsFalse(teammate.IsInactive, "The move must reactivate the sender.");
            Assert.AreEqual(CaptainUserId, m_game.HomeTeam.Id, "A move must not replace the inactive captain.");
            Assert.AreEqual(0, m_repository.TeamSaves.Count, "Moves must not persist captain changes.");
        }

        [Test]
        public async Task ReclaimedCaptain_LowerActiveTeammateBecomesCaptain()
        {
            AddUser(TeammateNumber, TeammateUserId, CaptainClientId + 1);
            var teammate = m_game.GetPlayer(TeamIndex.Home, TeammateNumber);
            teammate.RegisterActivity(CurrentTurn);
            Captain.CloseInput(CurrentTurn, Threshold);
            var claimant = new ServerUser(9201, 8201, 8301, "Claimant", UserRoles.Player, GameId);
            Assert.IsNull(m_users.RegisterUser(claimant), "The claimant must be connected in the room.");

            await m_engine.UserClaimPlayerAsync(claimant.ClientId.Value, Captain.Slot.PlayerKey);

            Assert.AreEqual(TeammateUserId, m_game.HomeTeam.Id, "The lower eligible slot must beat the claimant.");
            Assert.IsTrue(teammate.IsCaptain, "The selected teammate must receive captaincy.");
            Assert.IsFalse(Captain.IsCaptain, "The claimant must not receive captaincy when a lower slot qualifies.");
            Assert.That(
                m_repository.TeamSaves.Single(),
                Is.EqualTo((GameId, TeamIndex.Home, TeammateUserId, teammate.PlayerId, teammate.Name)),
                "Team persistence must use the selected teammate's database identity."
            );
        }

        [Test]
        public async Task UnrelatedClaim_DoesNotReplaceInactiveCaptain()
        {
            Captain.CloseInput(CurrentTurn, Threshold);
            var claimant = new ServerUser(9201, 8201, 8301, "Claimant", UserRoles.Player, GameId);
            Assert.IsNull(m_users.RegisterUser(claimant), "The claimant must be registered in the room.");

            await m_engine.UserClaimPlayerAsync(claimant.ClientId.Value, new PlayerKey(TeamIndex.Home, 2));

            Assert.AreEqual(CaptainUserId, m_game.HomeTeam.Id, "Claiming a different slot must retain team identity.");
            Assert.IsTrue(Captain.IsCaptain, "An unrelated claim must leave captain flags unchanged.");
            Assert.AreEqual(0, m_repository.TeamSaves.Count, "An unrelated claim must not save a captain change.");
        }

        [Test]
        public async Task ClaimTeamSaveFailure_RetriesWithoutRepeatingClaim()
        {
            Captain.CloseInput(CurrentTurn, Threshold);
            var claimant = new ServerUser(9201, 8201, 8301, "Claimant", UserRoles.Player, GameId);
            Assert.IsNull(m_users.RegisterUser(claimant), "The claimant must be registered in the room.");
            m_repository.TeamFailuresRemaining = 1;

            await m_engine.UserClaimPlayerAsync(claimant.ClientId.Value, Captain.Slot.PlayerKey);

            Assert.AreEqual(1, m_repository.ClaimCalls, "A team-save retry must not repeat a successful claim.");
            Assert.AreEqual(2, m_repository.TeamSaves.Count, "A transient team-save failure must retry once.");
            Assert.That(
                m_repository.TeamSaves[1],
                Is.EqualTo(m_repository.TeamSaves[0]),
                "Retries must reuse the originally selected captain identities."
            );
        }

        [Test]
        public async Task TurnClosure_PersistsChangedCaptainAndPublishesOnlyChangedTeam()
        {
            AddUser(TeammateNumber, TeammateUserId, CaptainClientId + 1);
            var teammate = m_game.GetPlayer(TeamIndex.Home, TeammateNumber);
            teammate.RegisterActivity(CurrentTurn);
            m_game.ProcessingStarted(0f);

            await m_engine.ProcessAndPersistTurnAsync();

            Assert.AreEqual(TeammateUserId, m_game.HomeTeam.Id, "Turn closure must select the active teammate.");
            Assert.That(
                m_repository.TeamSaves.Single(),
                Is.EqualTo((GameId, TeamIndex.Home, TeammateUserId, teammate.PlayerId, teammate.Name)),
                "Turn closure must persist the database player ID of the new captain."
            );
            Assert.That(
                m_room.TeamUpdates,
                Is.EqualTo(new[] { TeamIndex.Home }),
                "Only the changed team's identity needs publication."
            );
            Assert.AreEqual(1, m_repository.TurnRequests.Count, "Processing must save the completed turn once.");
        }

        [Test]
        public async Task TurnClosure_NoEligibleCaptain_PreservesIdentityWithoutTeamSave()
        {
            string name = m_game.HomeTeam.Name;
            m_game.ProcessingStarted(0f);

            await m_engine.ProcessAndPersistTurnAsync();

            Assert.AreEqual(CaptainUserId, m_game.HomeTeam.Id, "No replacement must preserve team identity.");
            Assert.AreEqual(name, m_game.HomeTeam.Name, "No replacement must preserve team name.");
            Assert.IsTrue(Captain.IsCaptain, "No replacement must retain the old captain flag.");
            Assert.AreEqual(0, m_repository.TeamSaves.Count, "Unchanged captain assignments must not be saved.");
        }

        [Test]
        public async Task TurnTeamSaveFailure_DoesNotRepeatCompletedTurn()
        {
            AddUser(TeammateNumber, TeammateUserId, CaptainClientId + 1);
            m_game.GetPlayer(TeamIndex.Home, TeammateNumber).RegisterActivity(CurrentTurn);
            m_repository.TeamFailuresRemaining = 1;
            m_game.ProcessingStarted(0f);

            await m_engine.ProcessAndPersistTurnAsync();

            Assert.AreEqual(
                1,
                m_repository.TurnRequests.Count,
                "Team failures must not repeat the completed-turn save."
            );
            Assert.AreEqual(2, m_repository.TeamSaves.Count, "The failed team save must be retried once.");
            Assert.That(
                m_repository.TeamSaves[1],
                Is.EqualTo(m_repository.TeamSaves[0]),
                "The retry must retain the selected captain snapshot."
            );
            Assert.AreEqual(1, m_room.TeamUpdates.Count, "A retry must publish team identity only once.");
        }

        [Test]
        public async Task TurnSaveFailure_RetriesSameSnapshotBeforeSavingCaptain()
        {
            AddUser(TeammateNumber, TeammateUserId, CaptainClientId + 1);
            m_game.GetPlayer(TeamIndex.Home, TeammateNumber).RegisterActivity(CurrentTurn);
            m_repository.TurnFailuresRemaining = 1;
            m_game.ProcessingStarted(0f);

            await m_engine.ProcessAndPersistTurnAsync();

            Assert.AreEqual(2, m_repository.TurnRequests.Count, "A transient turn-save failure must retry once.");
            Assert.AreSame(
                m_repository.TurnRequests[0],
                m_repository.TurnRequests[1],
                "Turn retries must reuse the same completed-turn snapshot."
            );
            Assert.AreEqual(1, m_repository.TeamSaves.Count, "Captain persistence must follow a successful turn save.");
        }

        [Test]
        public async Task SecondTeamSaveFailure_DoesNotRepeatFirstTeamSave()
        {
            AddUser(TeammateNumber, TeammateUserId, CaptainClientId + 1);
            m_game.GetPlayer(TeamIndex.Home, TeammateNumber).RegisterActivity(CurrentTurn);
            const int awayUserId = 8501;
            AddUser(TeammateNumber, awayUserId, CaptainClientId + 2, teamIndex: TeamIndex.Away);
            m_game.GetPlayer(TeamIndex.Away, TeammateNumber).RegisterActivity(CurrentTurn);
            m_repository.FailureTeam = TeamIndex.Away;
            m_repository.TeamFailuresRemaining = 1;
            m_game.ProcessingStarted(0f);

            await m_engine.ProcessAndPersistTurnAsync();

            Assert.AreEqual(1, m_repository.TurnRequests.Count, "A second team failure must not repeat the turn save.");
            Assert.AreEqual(
                1,
                m_repository.TeamSaves.Count(save => save.Team == TeamIndex.Home),
                "The first team's successful save must not be repeated."
            );
            Assert.AreEqual(
                2,
                m_repository.TeamSaves.Count(save => save.Team == TeamIndex.Away),
                "Only the failed second team's save must be retried."
            );
        }

        [Test]
        public async Task PendingTeamSave_BlocksCommandsAndDefersPlayerAndTeamPublication()
        {
            AddUser(TeammateNumber, TeammateUserId, CaptainClientId + 1);
            var teammate = m_game.GetPlayer(TeamIndex.Home, TeammateNumber);
            teammate.RegisterActivity(CurrentTurn);
            m_repository.TeamSaveBlock = new TaskCompletionSource<bool>();
            m_game.ProcessingStarted(0f);

            Task processing = m_engine.ProcessAndPersistTurnAsync();
            Task<PlayerCommandResponse> command = m_engine.AddPlayerMoveAsync(
                Command(teammate, PlayerActionEnum.Move, teammate.Position),
                CaptainClientId + 1
            );
            try
            {
                Assert.IsFalse(processing.IsCompleted, "Turn processing must wait for the required team save.");
                Assert.IsFalse(
                    command.IsCompleted,
                    "The persistence gate must hold commands until processing completes."
                );
                Assert.AreEqual(
                    0,
                    m_room.PlayerUpdates.Count,
                    "Control-state publication must wait for team persistence."
                );
                Assert.AreEqual(0, m_room.TeamUpdates.Count, "Team publication must wait for team persistence.");
            }
            finally
            {
                m_repository.TeamSaveBlock.SetResult(true);
                await processing;
                await command;
            }
            Assert.AreEqual(1, m_room.TeamUpdates.Count, "The changed team must publish after persistence completes.");
            Assert.IsTrue(
                m_room.PlayerUpdates.Count > 0,
                "Changed control states must publish after persistence completes."
            );
        }

        [Test]
        public async Task TurnClosure_ActiveCaptain_DoesNotSaveOrChangeTeamIdentity()
        {
            Captain.RegisterActivity(CurrentTurn);
            AddUser(TeammateNumber, TeammateUserId, CaptainClientId + 1);
            m_game.GetPlayer(TeamIndex.Home, TeammateNumber).RegisterActivity(CurrentTurn);
            m_game.ProcessingStarted(0f);

            await m_engine.ProcessAndPersistTurnAsync();

            Assert.AreEqual(
                CaptainUserId,
                m_game.HomeTeam.Id,
                "An active captain must remain despite a lower active slot."
            );
            Assert.IsTrue(Captain.IsCaptain, "The active captain's flag must remain set.");
            Assert.AreEqual(0, m_repository.TeamSaves.Count, "An unchanged active captain must not be saved again.");
            Assert.AreEqual(
                0,
                m_room.TeamUpdates.Count,
                "An unchanged team must not publish redundant identity updates."
            );
        }

        [Test]
        public void SaveTeamInfoCommand_UsesExpectedSqlParameters()
        {
            const int captainPlayerId = 8301;
            const string captainName = "Captain 8301";
            using var command = new System.Data.SqlClient.SqlCommand();

            SqlGamePersistenceRepository.ConfigureTeamInfoCommand(
                command,
                GameId,
                TeamIndex.Home,
                CaptainUserId,
                captainPlayerId,
                captainName
            );

            Assert.AreEqual(
                "foobbuteo_api.save_team_info",
                SqlGamePersistenceRepository.k_SaveTeamInfoProcedure,
                "The SQL repository must target the documented future procedure."
            );
            Assert.AreEqual(
                5,
                command.Parameters.Count,
                "The team update contract must contain exactly four parameters."
            );
            Assert.AreEqual(GameId, command.Parameters["@GameId"].Value, "The command must identify the game.");
            Assert.AreEqual(
                (byte)TeamIndex.Home,
                command.Parameters["@TeamIndex"].Value,
                "The team index must be tinyint."
            );
            Assert.AreEqual(
                CaptainUserId,
                command.Parameters["@CaptainUserId"].Value,
                "The command must identify the user."
            );
            Assert.AreEqual(
                captainPlayerId,
                command.Parameters["@CaptainPlayerId"].Value,
                "The command must use the database identity instead of a slot number."
            );
        }

        private void CloseInputAndReplaceCaptain()
        {
            m_activity.CloseInput(_ => { });
            m_activity.ApplyCaptain(m_activity.FindCaptainReplacement(m_game.HomeTeam), _ => { });
        }

        private ServerPlayerState Captain => m_game.GetPlayer(TeamIndex.Home, CaptainNumber);

        private ServerUser AddUser(
            byte number,
            int userId,
            ulong? clientId,
            bool isCaptain = false,
            TeamIndex teamIndex = TeamIndex.Home
        )
        {
            ServerPlayerState player = m_game.GetPlayer(teamIndex, number);
            GameDetails_PlayerContract contract = player.ToContract();
            contract.UserId = userId;
            contract.Name = $"User {userId}";
            contract.IsCaptain = isCaptain;
            contract.IsConnected = clientId.HasValue;
            contract.ClientId = clientId;
            player.OnPlayerControlClaimed(contract);
            var user = new ServerUser(
                clientId,
                userId,
                player.PlayerId,
                contract.Name,
                UserRoles.Player,
                GameId,
                player.Slot
            );
            Assert.IsNull(
                m_users.RegisterUser(user, validateClientId: false),
                "Test users must have unique assignments."
            );
            return user;
        }

        private static ServerPlayerState[] CreatePlayers(TeamIndex team)
        {
            var players = new ServerPlayerState[11];
            for (byte index = 1; index <= 11; index++)
                players[index - 1] = ServerPlayerState.CreateForTest(GameId, team, index);
            return players;
        }

        private static PlayerCommand Command(ServerPlayerState player, PlayerActionEnum action, Vector2 target) =>
            new(CurrentTurn, player.Slot.TeamIdx, player.Slot.PlayerIdx, player.PlayerId, action, target);

        private sealed class RecordingRoom : IServerGameRoomNB
        {
            public int GameId => PlayerInactivityTests.GameId;
            public List<GameDetails_PlayerContract> PlayerUpdates { get; } = new();
            public List<TeamIndex> TeamUpdates { get; } = new();

            public void InitServer(IGameEngine engine) { }

            public Coroutine StartGameCoroutine(IEnumerator routine) => null;

            public void AddClientToGameRoom(ulong clientId) { }

            public void UpdateStartTime() { }

            public void UpdateTeamInfo(TeamIndex teamIdx) => TeamUpdates.Add(teamIdx);

            public void UpdatePlayerControlState(GameDetails_PlayerContract player) => PlayerUpdates.Add(player);

            public void CloseGameRoom(ulong clientId) { }

            public void Close() { }

            public void Dispose() { }
        }

        private sealed class RecordingRepository : IGamePersistenceRepository
        {
            public bool Fail { get; set; }
            public int TeamFailuresRemaining { get; set; }
            public TeamIndex? FailureTeam { get; set; }
            public TaskCompletionSource<bool> TeamSaveBlock { get; set; }
            public int TurnFailuresRemaining { get; set; }
            public int CommandCalls { get; private set; }
            public int ClaimCalls { get; private set; }
            public List<(int GameId, TeamIndex Team, int UserId, int PlayerId, string Name)> TeamSaves { get; } = new();
            public List<TurnPersistenceRequest> TurnRequests { get; } = new();

            public Task SavePlayerCommandAsync(int gameId, PlayerCommand command, bool hadBall)
            {
                CommandCalls++;
                return Complete();
            }

            public Task SavePlayerModeAsync(int gameId, PlayerModeCommand command) => Complete();

            public Task<GameDetails_PlayerContract?> SavePlayerClaimAsync(
                int gameId,
                PlayerKey key,
                ServerUser user,
                bool isCaptain
            )
            {
                ClaimCalls++;
                return Fail
                    ? Task.FromException<GameDetails_PlayerContract?>(
                        new RepositoryUnavailableException("Test failure")
                    )
                    : Task.FromResult<GameDetails_PlayerContract?>(null);
            }

            public Task SaveTurnAsync(TurnPersistenceRequest request)
            {
                TurnRequests.Add(request);
                return TurnFailuresRemaining-- > 0 ? FailedTask() : Complete();
            }

            public Task SaveTeamInfoAsync(
                int gameId,
                TeamIndex teamIndex,
                int captainUserId,
                int captainPlayerId,
                string captainName
            )
            {
                TeamSaves.Add((gameId, teamIndex, captainUserId, captainPlayerId, captainName));
                if ((!FailureTeam.HasValue || FailureTeam.Value == teamIndex) && TeamFailuresRemaining-- > 0)
                    return FailedTask();
                return TeamSaveBlock?.Task ?? Complete();
            }

            private static Task FailedTask() => Task.FromException(new RepositoryUnavailableException("Test failure"));

            private Task Complete() => Fail ? FailedTask() : Task.CompletedTask;

            public void Dispose() { }
        }
    }
}
