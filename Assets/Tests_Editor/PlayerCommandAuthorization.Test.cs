// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using Imma.Foobbuteo.Shared.Game.NetState;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor
{
    public class PlayerCommandAuthorizationTests
    {
        private const int k_gameId = 701;
        private const int k_currentTurn = 7;
        private const int k_captainUserId = 801;
        private const int k_otherUserId = 802;
        private const int k_nonCaptainUserId = 803;

        private TestGameState m_state;

        [SetUp]
        public void SetUp()
        {
            m_state = new TestGameState(
                k_gameId,
                k_currentTurn,
                CreatePlayers(TeamIndex.Home),
                CreatePlayers(TeamIndex.Away)
            );
        }

        [Test]
        public void GetPlayerForCommand_OwnPlayer_ReturnsPlayerForBothCommandTypes()
        {
            TestGameUser user = CreateUser(k_captainUserId, TeamIndex.Home, 1);
            TestPlayerState expected = m_state.GetPlayer(TeamIndex.Home, 1);
            var moveCommand = new PlayerCommand(
                k_currentTurn,
                TeamIndex.Home,
                1,
                expected.PlayerId,
                PlayerActionEnum.Move,
                Vector2.zero
            );
            var modeCommand = new PlayerModeCommand(
                k_currentTurn,
                TeamIndex.Home,
                1,
                expected.PlayerId,
                PlayerModeEnum.GkSave
            );

            AssertAllowed(moveCommand, user, expected);
            AssertAllowed(modeCommand, user, expected);
        }

        [Test]
        public void GetPlayerForCommand_CaptainControlsTeamBot_ReturnsBot()
        {
            TestGameUser user = CreateUser(k_captainUserId, TeamIndex.Home, 1);
            TestPlayerState expected = m_state.GetPlayer(TeamIndex.Home, 2);

            TestPlayerState result = m_state.GetPlayerForCommand(expected.Slot.PlayerKey, user, out string reason);

            Assert.AreSame(expected, result);
            Assert.IsNull(reason);
        }

        [Test]
        public void GetPlayerForCommand_CaptainControlsOtherUsersPlayer_IsRejected()
        {
            TestGameUser user = CreateUser(k_captainUserId, TeamIndex.Home, 1);

            AssertRejected(new PlayerKey(TeamIndex.Home, 3), user, "other user");
        }

        [Test]
        public void GetPlayerForCommand_NonCaptainControlsTeamBot_IsRejected()
        {
            TestGameUser user = CreateUser(k_nonCaptainUserId, TeamIndex.Home, 4);

            AssertRejected(new PlayerKey(TeamIndex.Home, 2), user, "Only captain");
        }

        [Test]
        public void GetPlayerForCommand_OpponentPlayer_IsRejected()
        {
            TestGameUser user = CreateUser(k_captainUserId, TeamIndex.Home, 1);

            AssertRejected(new PlayerKey(TeamIndex.Away, 2), user, "opponent team");
        }

        [Test]
        public void GetPlayerForCommand_OutsidePlayerMoveStage_IsRejected()
        {
            TestGameUser user = CreateUser(k_captainUserId, TeamIndex.Home, 1);
            m_state.SetStage(TurnStageEnum.Processing);

            AssertRejected(new PlayerKey(TeamIndex.Home, 1), user, "Player Move");
        }

        [Test]
        public void GetPlayerForCommand_MissingUser_IsRejected()
        {
            AssertRejected(new PlayerKey(TeamIndex.Home, 1), null, "User not found");
        }

        [Test]
        public void GetPlayerForCommand_UserWithoutSlot_IsRejected()
        {
            var user = new TestGameUser(k_captainUserId, k_gameId, null);

            AssertRejected(new PlayerKey(TeamIndex.Home, 1), user, "not control any player");
        }

        [Test]
        public void GetPlayerForCommand_UserInWrongRoom_IsRejected()
        {
            var user = new TestGameUser(k_captainUserId, k_gameId + 1, new GameSlot(k_gameId, TeamIndex.Home, 1));

            AssertRejected(new PlayerKey(TeamIndex.Home, 1), user, "not in room");
        }

        [Test]
        public void GetPlayerForCommand_UserSlotFromWrongGame_IsRejected()
        {
            var user = new TestGameUser(k_captainUserId, k_gameId, new GameSlot(k_gameId + 1, TeamIndex.Home, 1));

            AssertRejected(new PlayerKey(TeamIndex.Home, 1), user, "other game");
        }

        [Test]
        public void CanClaimPlayer_TeamCaptainClaimsOwnTeam_IsAllowed()
        {
            var user = new TestGameUser(901, k_gameId, null);

            bool result = m_state.CanClaimPlayer(user, new PlayerKey(TeamIndex.Home, 2), out string reason);

            Assert.IsTrue(result);
            Assert.IsNull(reason);
        }

        [Test]
        public void CanClaimPlayer_TeamCaptainClaimsOpponent_IsRejected()
        {
            var user = new TestGameUser(901, k_gameId, null);

            bool result = m_state.CanClaimPlayer(user, new PlayerKey(TeamIndex.Away, 2), out string reason);

            Assert.IsFalse(result);
            StringAssert.Contains("captain of a team", reason);
        }

        [Test]
        public void ClaimPlayer_TeamCaptainClaimsOpponent_ThrowsSharedReason()
        {
            var user = new TestGameUser(901, k_gameId, null);

            AppException exception = Assert.Throws<AppException>(() =>
                m_state.ClaimPlayer(user, new PlayerKey(TeamIndex.Away, 2))
            );

            StringAssert.Contains("captain of a team", exception.Message);
        }

        [Test]
        public void CanClaimPlayer_NonCaptainClaimsEitherTeam_IsAllowed()
        {
            var user = new TestGameUser(999, k_gameId, null);

            Assert.IsTrue(
                m_state.CanClaimPlayer(user, new PlayerKey(TeamIndex.Home, 2), out string homeReason),
                homeReason
            );
            Assert.IsTrue(
                m_state.CanClaimPlayer(user, new PlayerKey(TeamIndex.Away, 2), out string awayReason),
                awayReason
            );
        }

        [Test]
        public void ClaimPlayer_TeamWithoutCaptain_AssignsClaimingUserAsCaptain()
        {
            m_state = new TestGameState(
                k_gameId,
                k_currentTurn,
                CreatePlayers(TeamIndex.Home),
                CreatePlayers(TeamIndex.Away),
                homeTeamId: 0
            );
            var user = new TestGameUser(999, k_gameId, null);

            TestPlayerState player = m_state.ClaimPlayer(user, new PlayerKey(TeamIndex.Home, 2));

            Assert.IsTrue(player.IsCaptain);
            Assert.AreEqual(user.UserId, m_state.HomeTeam.Id);
            Assert.AreEqual(user.Name, m_state.HomeTeam.Name);
            Assert.AreEqual(player.Slot, user.GameSlot.Value);
        }

        private void AssertAllowed(IPlayerCommand command, GameUser user, TestPlayerState expected)
        {
            TestPlayerState result = m_state.GetPlayerForCommand(command, user, out string reason);

            Assert.AreSame(expected, result);
            Assert.IsNull(reason);
        }

        private void AssertRejected(PlayerKey playerKey, GameUser user, string reasonPart)
        {
            TestPlayerState result = m_state.GetPlayerForCommand(playerKey, user, out string reason);

            Assert.IsNull(result);
            StringAssert.Contains(reasonPart, reason);
        }

        private static TestGameUser CreateUser(int userId, TeamIndex teamIdx, byte playerIdx) =>
            new TestGameUser(userId, k_gameId, new GameSlot(k_gameId, teamIdx, playerIdx));

        private static TestPlayerState[] CreatePlayers(TeamIndex teamIdx)
        {
            var players = new TestPlayerState[11];
            for (byte playerIdx = 1; playerIdx <= players.Length; playerIdx++)
            {
                int userId = 0;
                bool isCaptain = false;
                if (teamIdx == TeamIndex.Home)
                {
                    if (playerIdx == 1)
                    {
                        userId = k_captainUserId;
                        isCaptain = true;
                    }
                    else if (playerIdx == 3)
                    {
                        userId = k_otherUserId;
                    }
                    else if (playerIdx == 4)
                    {
                        userId = k_nonCaptainUserId;
                    }
                }

                players[playerIdx - 1] = new TestPlayerState(
                    new GameSlot(k_gameId, teamIdx, playerIdx),
                    playerIdx,
                    userId,
                    isCaptain
                );
            }

            return players;
        }

        private sealed class TestGameState : FoobbuteoGameState<TestPlayerState>
        {
            public TestGameState(
                int gameId,
                int currentTurn,
                TestPlayerState[] homePlayers,
                TestPlayerState[] awayPlayers,
                int homeTeamId = 901,
                int awayTeamId = 902
            )
                : base(gameId, FoobbuteoGameType.PvP)
            {
                m_currentTurn = currentTurn;
                TurnStage = TurnStageEnum.PlayerMove;
                m_homeTeam = new FoobbuteoTeamState<TestPlayerState>(
                    TeamIndex.Home,
                    homeTeamId,
                    "Home",
                    ShirtColorEnum.Blue,
                    homePlayers
                );
                m_awayTeam = new FoobbuteoTeamState<TestPlayerState>(
                    TeamIndex.Away,
                    awayTeamId,
                    "Away",
                    ShirtColorEnum.Red,
                    awayPlayers
                );
            }

            public void SetStage(TurnStageEnum stage)
            {
                TurnStage = stage;
            }
        }

        private sealed class TestPlayerState : FoobbuteoPlayerState
        {
            public TestPlayerState(GameSlot slot, int playerId, int userId, bool isCaptain)
                : base(slot)
            {
                PlayerId = playerId;
                UserId = userId;
                IsCaptain = isCaptain;
            }
        }

        private sealed class TestGameUser : GameUser
        {
            public TestGameUser(int userId, int roomId, GameSlot? gameSlot)
            {
                m_clientId = (ulong)userId;
                m_userId = userId;
                m_name = $"User {userId}";
                m_inGameRoomId = roomId;
                m_gameSlot = gameSlot;
            }
        }
    }
}
