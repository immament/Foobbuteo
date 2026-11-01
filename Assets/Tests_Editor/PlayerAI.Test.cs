// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Foobbuteo.Server.Game;
using Imma.Foobbuteo.Server.Game.Model;
using Imma.Foobbuteo.Shared.dev;
using Imma.Foobbuteo.Shared.Game;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor
{
    [Category("BotAI")]
    public class PlayerAITests
    {
        [TestCase(TeamIndex.Home)]
        [TestCase(TeamIndex.Away)]
        public void FormationPosition_IsIndependentOfLoadedPosition(TeamIndex team)
        {
            var position = new Vector2(300f, 200f);
            var player = ServerPlayerState.CreateForTest(1, team, 2, 42, "Bot", position);

            Assert.AreEqual(position, player.Position);
            Assert.AreEqual(Formation442.GetPosition(team, 2), player.FormationPosition);
        }

        [Test]
        public void Decide_AtFormationPosition_DoesNotGenerateCommand()
        {
            var player = ServerPlayerState.CreateForTest(1, TeamIndex.Home, 2);
            Assert.IsNull(player.AI.Decide(7, player.FormationPosition, PitchUtils.PitchBounds.Center));
        }

        [TestCase(100f)]
        [TestCase(25f)]
        public void Decide_DistantTarget_RespectsAvailableActionPoints(float points)
        {
            var player = ServerPlayerState.CreateForTest(
                1,
                TeamIndex.Home,
                2,
                42,
                "Bot",
                new Vector2(100f, 200f),
                actionPoints: points
            );
            player.FormationPosition = new Vector2(500f, 200f);
            var originalPosition = player.Position;

            var command = player.AI.Decide(7, player.FormationPosition, PitchUtils.PitchBounds.Center).Value;
            var distance = Vector2.Distance(originalPosition, command.TargetPosition);

            Assert.Greater(distance, 0f);
            Assert.LessOrEqual(player.MoveCost(distance), points);
            Assert.AreEqual(player.AvailableMoveDistance, distance, 0.01f);
            Assert.AreEqual(originalPosition, player.Position, "Planning must not move the player.");
            Assert.AreEqual(points, player.ActionPoints, "Planning must not spend action points.");
            Assert.AreEqual(7, command.Turn);
            Assert.AreEqual(42, command.PlayerId);
            Assert.AreEqual(TeamIndex.Home, command.TeamIdx);
            Assert.AreEqual(2, command.PlayerIdx);
            Assert.AreEqual(PlayerActionEnum.Move, command.Action);
        }

        [Test]
        public void Decide_ChangedFormationTarget_UsesNewTargetWithoutOvershooting()
        {
            var player = ServerPlayerState.CreateForTest(1, TeamIndex.Home, 2);
            player.AI.Decide(1, player.FormationPosition, PitchUtils.PitchBounds.Center);
            player.FormationPosition = player.Position + new Vector2(10f, 0f);

            Assert.AreEqual(
                player.FormationPosition,
                player.AI.Decide(2, player.FormationPosition, PitchUtils.PitchBounds.Center).Value.TargetPosition
            );
        }

        [Test]
        public void Decide_WithoutActionPoints_DoesNotGenerateCommand()
        {
            var player = ServerPlayerState.CreateForTest(
                1,
                TeamIndex.Home,
                2,
                42,
                "Bot",
                new Vector2(300f, 200f),
                actionPoints: 0f
            );

            Assert.IsNull(player.AI.Decide(7, player.FormationPosition, PitchUtils.PitchBounds.Center));
        }

        [Test]
        public void Decide_PossessionChanges_SuppressAndRestoreFormationMovement()
        {
            var player = ServerPlayerState.CreateForTest(1, TeamIndex.Home, 2);
            player.FormationPosition += new Vector2(20f, 0f);
            Assert.IsNotNull(player.AI.Decide(1, player.FormationPosition, PitchUtils.PitchBounds.Center));

            player.HasBall = true;
            var withBallCommand = player.AI.Decide(2, player.FormationPosition, PitchUtils.PitchBounds.Center).Value;
            Assert.AreEqual(PlayerActionEnum.Move, withBallCommand.Action);
            Assert.AreNotEqual(player.FormationPosition, withBallCommand.TargetPosition);
            Assert.AreEqual(PlayerAIState.WithBall, player.AI.CurrentState);

            player.HasBall = false;
            Assert.IsNotNull(player.AI.Decide(3, player.FormationPosition, PitchUtils.PitchBounds.Center));
            Assert.AreEqual(PlayerAIState.MaintainFormation, player.AI.CurrentState);
        }

        [Test]
        public void Decide_GoalkeeperWithoutBall_TracksBallAcrossGoalMouth()
        {
            var player = ServerPlayerState.CreateForTest(1, TeamIndex.Home, 1);
            var command = player
                .AI.Decide(
                    7,
                    player.FormationPosition,
                    new Vector2(PitchUtils.PitchBounds.Center.x, PitchUtils.PitchBounds.Max.y)
                )
                .Value;

            Assert.AreEqual(player.FormationPosition.x, command.TargetPosition.x, 0.001f);
            Assert.AreEqual(PitchUtils.GoalRangeY.Max, command.TargetPosition.y, 0.001f);
            Assert.AreEqual(PlayerAIState.Goalkeeper, player.AI.CurrentState);
        }

        [Test]
        public void Decide_GoalkeeperWithBall_DoesNotMove()
        {
            var player = ServerPlayerState.CreateForTest(1, TeamIndex.Home, 1);
            player.HasBall = true;

            Assert.IsNull(player.AI.Decide(7, player.FormationPosition, new Vector2(305f, 420f)));
            Assert.AreEqual(PlayerAIState.Goalkeeper, player.AI.CurrentState);
        }

        [Test]
        public void Decide_GoalkeepersUseMirroredGuardedPositions()
        {
            var home = ServerPlayerState.CreateForTest(1, TeamIndex.Home, 1);
            var away = ServerPlayerState.CreateForTest(1, TeamIndex.Away, 1);
            var ball = new Vector2(100f, 70f);

            var homeTarget = home.AI.Decide(7, home.FormationPosition, ball).Value.TargetPosition;
            var awayTarget = away
                .AI.Decide(7, away.FormationPosition, PitchUtils.PitchBounds.Max - ball)
                .Value.TargetPosition;

            Assert.AreEqual(PitchUtils.PitchBounds.Max - homeTarget, awayTarget);
        }

        [TestCase(TeamIndex.Home)]
        [TestCase(TeamIndex.Away)]
        public void Decide_UserGoalkeeperWithBall_CreatesRandomLegalForwardHighPass(TeamIndex team)
        {
            var position = Formation442.GetPosition(team, 1);
            var player = ServerPlayerState.CreateForTest(
                1,
                team,
                1,
                42,
                "User goalkeeper",
                position,
                hasBall: true,
                userId: 100
            );
            var previousRandomState = Random.state;
            Random.InitState(team == TeamIndex.Home ? 123 : 456);

            try
            {
                var command = player.AI.Decide(7, player.FormationPosition, player.BallSlotPosition).Value;
                float distance = Vector2.Distance(player.Position, command.TargetPosition);

                Assert.AreEqual(PlayerActionEnum.HighPass, command.Action);
                Assert.IsTrue(command.TargetPosition.IsOnPitch());
                Assert.GreaterOrEqual(distance, player.MinHighPassDistance);
                Assert.LessOrEqual(distance, player.AvailableHighPassDistance);
                Assert.AreNotEqual(player.Position.y, command.TargetPosition.y);
                if (team == TeamIndex.Home)
                    Assert.Greater(command.TargetPosition.x, player.Position.x);
                else
                    Assert.Less(command.TargetPosition.x, player.Position.x);
            }
            finally
            {
                Random.state = previousRandomState;
            }
        }

        [TestCase(TeamIndex.Home, 13, 11)]
        [TestCase(TeamIndex.Away, 11, 13)]
        public void Decide_BotWithBall_PassesOnlyToSupportedForwardZone(
            TeamIndex team,
            int supportedZone,
            int backwardZone
        )
        {
            var ownPlayers = CreatePlayers(team);
            var opponents = CreatePlayers(team == TeamIndex.Home ? TeamIndex.Away : TeamIndex.Home);
            var position = PitchUtils.GetZoneCenter(12);
            var player = ServerPlayerState.CreateForTest(1, team, 6, 42, "Bot with ball", position, hasBall: true);
            ownPlayers[5] = player;
            MovePlayers(ownPlayers, backwardZone);
            player.Server_PlayerMove(position);
            ownPlayers[6].Server_PlayerMove(PitchUtils.GetZoneCenter(supportedZone));
            ownPlayers[7].Server_PlayerMove(PitchUtils.GetZoneCenter(supportedZone));
            MovePlayers(opponents, 25);
            var ownTeam = new ServerTeamState(team, 1, "Own", default, ownPlayers);
            var opponentTeam = new ServerTeamState(
                team == TeamIndex.Home ? TeamIndex.Away : TeamIndex.Home,
                2,
                "Opponent",
                default,
                opponents
            );
            bool selectedPass = false;
            var previousRandomState = Random.state;

            try
            {
                for (int seed = 0; seed < 100; seed++)
                {
                    Random.InitState(seed);
                    var command = player
                        .AI.Decide(7, player.FormationPosition, player.BallSlotPosition, ownTeam, opponentTeam)
                        .Value;
                    if (command.Action is not (PlayerActionEnum.LowPass or PlayerActionEnum.HighPass))
                        continue;

                    selectedPass = true;
                    Assert.AreEqual(supportedZone, command.TargetPosition.GetZone());
                    int playerColumn = PitchUtils.GetZoneColumn(player.Zone);
                    int targetColumn = PitchUtils.GetZoneColumn(command.TargetPosition.GetZone());
                    if (team == TeamIndex.Home)
                        Assert.GreaterOrEqual(targetColumn, playerColumn);
                    else
                        Assert.LessOrEqual(targetColumn, playerColumn);
                }
            }
            finally
            {
                Random.state = previousRandomState;
            }

            Assert.IsTrue(selectedPass);
        }

        [Test]
        public void Decide_BotWithBall_RandomlyChoosesShootOrMoveInsideShotDistance()
        {
            var player = ServerPlayerState.CreateForTest(
                1,
                TeamIndex.Home,
                9,
                42,
                "Bot with ball",
                new Vector2(450f, 210f),
                hasBall: true
            );
            bool selectedMove = false;
            bool selectedShot = false;
            var previousRandomState = Random.state;

            try
            {
                for (int seed = 0; seed < 100; seed++)
                {
                    Random.InitState(seed);
                    var command = player.AI.Decide(7, player.FormationPosition, player.BallSlotPosition).Value;
                    selectedMove |= command.Action == PlayerActionEnum.Move;
                    selectedShot |= command.Action == PlayerActionEnum.Shot;
                    Assert.IsTrue(command.Action is PlayerActionEnum.Move or PlayerActionEnum.Shot);
                }
            }
            finally
            {
                Random.state = previousRandomState;
            }

            Assert.IsTrue(selectedMove);
            Assert.IsTrue(selectedShot);
        }

        [Test]
        public void Decide_BotWithBall_DoesNotShootAtMaximumShotDistance()
        {
            var player = ServerPlayerState.CreateForTest(
                1,
                TeamIndex.Home,
                9,
                42,
                "Bot with ball",
                new Vector2(300f, 210f),
                hasBall: true
            );
            var previousRandomState = Random.state;

            try
            {
                for (int seed = 0; seed < 20; seed++)
                {
                    Random.InitState(seed);
                    Assert.AreEqual(
                        PlayerActionEnum.Move,
                        player.AI.Decide(7, player.FormationPosition, player.BallSlotPosition).Value.Action
                    );
                }
            }
            finally
            {
                Random.state = previousRandomState;
            }
        }

        [Test]
        public void Decide_BotWithBall_DoesNotMoveAfterTwoConsecutiveTurns()
        {
            var player = ServerPlayerState.CreateForTest(
                1,
                TeamIndex.Home,
                9,
                42,
                "Bot with ball",
                PitchUtils.PitchBounds.Center,
                hasBall: true
            );
            player.Server_RegisterTurnMovedWithBall(true);
            player.Server_RegisterTurnMovedWithBall(true);

            Assert.IsNull(player.AI.Decide(7, player.FormationPosition, player.BallSlotPosition));
        }

        private static ServerPlayerState[] CreatePlayers(TeamIndex team)
        {
            var players = new ServerPlayerState[11];
            for (byte playerIdx = 1; playerIdx <= 11; playerIdx++)
                players[playerIdx - 1] = ServerPlayerState.CreateForTest(1, team, playerIdx);
            return players;
        }

        private static void MovePlayers(ServerPlayerState[] players, int zone)
        {
            var position = PitchUtils.GetZoneCenter(zone);
            foreach (var player in players)
                player.Server_PlayerMove(position);
        }
    }
}
