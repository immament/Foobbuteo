// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using System.Linq;
using Imma.Foobbuteo.Server.Game;
using Imma.Foobbuteo.Server.Game.Model;
using Imma.Foobbuteo.Shared.Game;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor
{
    [Category("MatchReport")]
    public class MatchReportServerTests
    {
        private ServerGameState m_game;
        private UnityEngine.Random.State m_random;

        [SetUp]
        public void SetUp()
        {
            m_random = UnityEngine.Random.state;
            UnityEngine.Random.InitState(42);
            var home = new ServerTeamState(TeamIndex.Home, 1, "Home", default, Players(TeamIndex.Home));
            var away = new ServerTeamState(TeamIndex.Away, 2, "Away", default, Players(TeamIndex.Away));
            m_game = ServerGameState.CreateForTest(1, DateTime.UtcNow, home, away, turn: 9);
            m_game.ProcessingStarted(0);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Random.state = m_random;

        [TestCase(0, MatchCommentaryKind.AdvanceUnopposed, 0)]
        [TestCase(5, MatchCommentaryKind.DribbleAgainstOpponents, 3)]
        public void Dribble_ReportsActorAndCappedNearbyOpponents(int opponents, MatchCommentaryKind kind, int count)
        {
            var actor = Holder(new Vector2(200, 100));
            var target = new Vector2(205, 100);
            for (byte i = 2; i < opponents + 2; i++)
                m_game.AwayTeam.GetPlayerAtIdx(i).Server_PlayerMove(target + Vector2.one);
            var result = Process(actor, PlayerActionEnum.Move, target);
            var cue = result.Commentary.Single(c => !c.AfterEvent);
            Assert.AreEqual(kind, cue.Kind);
            Assert.AreEqual(count, cue.Opponents);
            Assert.AreEqual(actor.Slot.PlayerKey, cue.Player);
            Assert.IsTrue(((PlayerMoveEvent)result.Events[cue.EventIndex]).WithBall);
        }

        [Test]
        public void Goal_ReportsShooterBeforeResetPositions()
        {
            var actor = Holder(new Vector2(600, 210));
            var result = Process(actor, PlayerActionEnum.Shot, new Vector2(610, 210));
            Assert.AreEqual(TurnResultTypeEnum.Goal, result.TurnResultType);
            var shot = result.Commentary.Single(c => c.Kind == MatchCommentaryKind.Shoot);
            var goal = result.Commentary.Single(c => c.Kind == MatchCommentaryKind.ScoreGoal);
            Assert.AreEqual(actor.Slot.PlayerKey, goal.Player);
            Assert.AreEqual(shot.EventIndex, goal.EventIndex);
            Assert.IsFalse(shot.AfterEvent);
            Assert.IsTrue(goal.AfterEvent);
            Assert.AreEqual(MatchEventType.FixPosition, result.Events[goal.EventIndex + 1].EventType);
        }

        [TestCase(PlayerActionEnum.LowPass, MatchCommentaryKind.InterceptPass)]
        [TestCase(PlayerActionEnum.Shot, MatchCommentaryKind.GetBall)]
        public void Interception_DistinguishesPassFromShot(PlayerActionEnum action, MatchCommentaryKind outcome)
        {
            var actor = Holder(new Vector2(200, 210));
            var interceptor = m_game.AwayTeam.GetPlayerAtIdx(action == PlayerActionEnum.Shot ? (byte)1 : (byte)2);
            interceptor.Server_PlayerMove(new Vector2(205, 210));
            var result = Process(actor, action, new Vector2(250, 210));
            Assert.AreEqual(TurnResultTypeEnum.Intercept, result.TurnResultType);
            var cue = result.Commentary.Single(c => c.AfterEvent);
            Assert.AreEqual(outcome, cue.Kind);
            Assert.AreEqual(interceptor.Slot.PlayerKey, cue.Player);
        }

        [Test]
        public void PassConfrontation_CapsCountsAndUsesWinnerIdentity()
        {
            var actor = Holder(new Vector2(100, 100));
            var target = PitchUtils.GetZoneCenter(5);
            for (byte i = 3; i <= 7; i++)
            {
                m_game.HomeTeam.GetPlayerAtIdx(i).Server_PlayerMove(target);
                m_game.AwayTeam.GetPlayerAtIdx(i).Server_PlayerMove(target);
            }
            // High passes do not use pass-interception resolution.
            var result = Process(actor, PlayerActionEnum.HighPass, target);
            var cue = result.Commentary.Single(c => c.Kind == MatchCommentaryKind.WinBallConfrontation);
            Assert.AreEqual(result.PlayerWithBall, cue.Player);
            Assert.AreEqual(2, cue.Teammates);
            Assert.AreEqual(3, cue.Opponents);
            Assert.AreEqual(5, cue.Zone);
        }

        private ServerPlayerState Holder(Vector2 position)
        {
            var player = m_game.HomeTeam.GetPlayerAtIdx(2);
            player.Server_PlayerMove(position);
            m_game.SetPlayerWithBall(player);
            m_game.MoveBall(player.BallSlotPosition);
            return player;
        }

        private TurnResult Process(ServerPlayerState player, PlayerActionEnum action, Vector2 target)
        {
            var commands = new List<PlayerCommand>
            {
                new(9, player.Slot.TeamIdx, player.Slot.PlayerIdx, player.PlayerId, action, target),
            };
            return new TurnProcessor(m_game, commands).ProcessTurn();
        }

        private static ServerPlayerState[] Players(TeamIndex team)
        {
            return Enumerable
                .Range(1, 11)
                .Select(i =>
                    ServerPlayerState.CreateForTest(
                        1,
                        team,
                        (byte)i,
                        i + 100 * (int)team,
                        $"Player {i}",
                        new Vector2(50, 380),
                        userId: i + 100 * (int)team,
                        shootingSkill: 336,
                        longShotsSkill: 336
                    )
                )
                .ToArray();
        }
    }
}
