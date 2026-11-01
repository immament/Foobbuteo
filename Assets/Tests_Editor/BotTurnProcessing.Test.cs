// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using System.Linq;
using Imma.Foobbuteo.Server.Game;
using Imma.Foobbuteo.Server.Game.Model;
using Imma.Foobbuteo.Shared.dev;
using Imma.Foobbuteo.Shared.Game;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using NUnit.Framework;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Tests.Editor
{
    [Category("BotAI")]
    public class BotTurnProcessingTests
    {
        private ServerPlayerState[] m_home;
        private ServerPlayerState[] m_away;
        private List<PlayerCommand> m_commands;
        private TurnProcessor m_processor;
        private ServerGameState m_game;

        [SetUp]
        public void SetUp()
        {
            m_home = CreatePlayers(TeamIndex.Home);
            m_away = CreatePlayers(TeamIndex.Away);
            var homeTeam = new ServerTeamState(TeamIndex.Home, 1, "Home", default, m_home)
            {
                FormationScale = Vector2.one,
                BallAnchorInfluence = 0f,
            };
            var awayTeam = new ServerTeamState(TeamIndex.Away, 2, "Away", default, m_away)
            {
                FormationScale = Vector2.one,
                BallAnchorInfluence = 0f,
            };
            var game = ServerGameState.CreateForTest(1, DateTime.UtcNow, homeTeam, awayTeam, turn: 7);
            m_game = game;
            game.MoveBall(new Vector2(305f, 210f));
            game.ProcessingStarted(0f);
            m_commands = new List<PlayerCommand>();
            m_processor = new TurnProcessor(game, m_commands);
        }

        [Test]
        public void ProcessTurn_UncommandedBotsOnBothTeams_MoveAndSpendActionPoints()
        {
            m_home[1].FormationPosition += new Vector2(10f, 0f);
            m_away[1].FormationPosition -= new Vector2(10f, 0f);
            float homeCost = m_home[1].MoveCost(10f);
            float awayCost = m_away[1].MoveCost(10f);

            m_processor.ProcessTurn();

            int expectedCommandsPerTeam = TeamPositioningPlanner.k_ReachBallPlayersCount + 1;
            Assert.AreEqual(
                expectedCommandsPerTeam,
                m_commands.Count(command => command.TeamIdx == TeamIndex.Home),
                "Home team should create the configured loose-ball commands and one formation-recovery command."
            );
            Assert.AreEqual(
                expectedCommandsPerTeam,
                m_commands.Count(command => command.TeamIdx == TeamIndex.Away),
                "Away team should create the configured loose-ball commands and one formation-recovery command."
            );
            Assert.AreEqual(m_home[1].FormationPosition, m_home[1].Position);
            Assert.AreEqual(m_away[1].FormationPosition, m_away[1].Position);
            Assert.AreEqual(100f - homeCost, m_home[1].ActionPoints, 0.001f);
            Assert.AreEqual(100f - awayCost, m_away[1].ActionPoints, 0.001f);
        }

        [Test]
        public void ProcessTurn_HumanWithoutCommand_IsNotMovedByAI()
        {
            var originalPosition = m_home[1].Position;
            m_home[1] = ServerPlayerState.CreateForTest(
                1,
                TeamIndex.Home,
                2,
                42,
                "Human",
                originalPosition,
                userId: 100
            );
            m_home[1].FormationPosition += new Vector2(10f, 0f);

            m_processor.ProcessTurn();

            Assert.IsFalse(m_commands.Any(command => command.TeamIdx == TeamIndex.Home && command.PlayerIdx == 2));
            Assert.AreEqual(originalPosition, m_home[1].Position);
            Assert.AreEqual(100f, m_home[1].ActionPoints);
        }

        [Test]
        public void ProcessTurn_BotWithAcceptedCaptainCommand_PreservesCommandAndRemainingPoints()
        {
            var player = m_home[1];
            player.FormationPosition += new Vector2(30f, 0f);
            var target = player.Position + new Vector2(0f, -10f);
            var command = new PlayerCommand(7, TeamIndex.Home, 2, player.PlayerId, PlayerActionEnum.Move, target);
            Assert.IsNull(player.Server_RegisterMoveCmd(ref command));
            m_commands.Add(command);
            float remainingPoints = player.ActionPoints;

            m_processor.ProcessTurn();

            Assert.AreEqual(1, m_commands.Count(item => item.CommandId == command.CommandId));
            Assert.AreEqual(target, player.Position);
            Assert.AreEqual(remainingPoints, player.ActionPoints);
        }

        [Test]
        public void ProcessTurn_BotWithoutActionPoints_RemainsStationary()
        {
            var originalPosition = new Vector2(50f, 210f);
            var player = ServerPlayerState.CreateForTest(
                1,
                TeamIndex.Home,
                2,
                42,
                "Bot",
                originalPosition,
                actionPoints: 0f
            );
            m_home[1] = player;

            m_processor.ProcessTurn();

            Assert.IsFalse(m_commands.Any(command => command.TeamIdx == TeamIndex.Home && command.PlayerIdx == 2));
            Assert.AreEqual(originalPosition, player.Position);
            Assert.AreEqual(0f, player.ActionPoints);
        }

        [Test]
        public void ProcessTurn_NoPossession_SelectsConfiguredClosestPlayersPerTeamToReachBallZone()
        {
            m_processor.ProcessTurn();

            Assert.AreEqual(
                TeamPositioningPlanner.k_ReachBallPlayersCount,
                CountPlayersInState(m_home, PlayerAIState.ReachBallZone),
                "Home team should assign the configured number of players to the loose ball."
            );
            Assert.AreEqual(
                TeamPositioningPlanner.k_ReachBallPlayersCount,
                CountPlayersInState(m_away, PlayerAIState.ReachBallZone),
                "Away team should assign the configured number of players to the loose ball."
            );
            Assert.AreEqual(
                TeamPositioningPlanner.k_ReachBallPlayersCount,
                m_commands.Count(command =>
                    command.TeamIdx == TeamIndex.Home
                    && command.TargetPosition.GetZone() == m_game.BallPosition.GetZone()
                ),
                "Each selected home player should receive a command targeting the ball zone."
            );
            Assert.AreEqual(
                TeamPositioningPlanner.k_ReachBallPlayersCount,
                m_commands.Count(command =>
                    command.TeamIdx == TeamIndex.Away
                    && command.TargetPosition.GetZone() == m_game.BallPosition.GetZone()
                ),
                "Each selected away player should receive a command targeting the ball zone."
            );
        }

        [Test]
        public void ProcessTurn_NoPossession_ClosestUserConsumesOneReachBallSelection()
        {
            var unselectedPlayersPosition = new Vector2(100f, 300f);
            MovePlayers(m_home, unselectedPlayersPosition);
            m_game.HomeTeam.GetGk().Server_PlayerMove(PitchUtils.PitchBounds.Min);
            ServerPlayerState closestUser = ReplaceWithUserPlayer(TeamIndex.Home, 2, m_game.BallPosition);

            m_processor.ProcessTurn();

            Assert.IsTrue(closestUser.IsUser, "The closest ranked player must be a user for this scenario.");
            Assert.AreEqual(
                TeamPositioningPlanner.k_ReachBallPlayersCount - 1,
                CountPlayersInState(m_home, PlayerAIState.ReachBallZone),
                "The closest user should consume one configured selection without falling back to another bot."
            );
        }

        [TestCase(TeamIndex.Home)]
        [TestCase(TeamIndex.Away)]
        public void CreateInstructions_ReachBallZone_UsesDistinctRandomTargetsInsideEveryRegularZone(
            TeamIndex teamIndex
        )
        {
            var team = teamIndex == TeamIndex.Home ? m_game.HomeTeam : m_game.AwayTeam;
            var opponent = m_game.GetOpponentTeam(teamIndex);
            var planner = new TeamPositioningPlanner(m_game, _ => false);
            var previousRandomState = Random.state;
            Random.InitState(1234);

            try
            {
                for (int zone = 1; zone <= PitchUtils.ZonesCount; zone++)
                {
                    Vector2 ballPosition = PitchUtils.GetZoneCenter(zone);
                    m_game.MoveBall(ballPosition);
                    MovePlayers(teamIndex == TeamIndex.Home ? m_home : m_away, ballPosition);
                    team.GetGk().Server_PlayerMove(PitchUtils.PitchBounds.Min);

                    var instructions = planner.CreateInstructions(team, opponent);
                    Assert.AreEqual(
                        TeamPositioningPlanner.k_ReachBallPlayersCount,
                        instructions.Count,
                        $"{teamIndex} should select both outfield players for zone {zone}."
                    );
                    Assert.AreEqual(
                        instructions.Count,
                        instructions.Values.Select(instruction => instruction.Target).Distinct().Count(),
                        $"Each selected {teamIndex} player should have a different random target in zone {zone}."
                    );
                    foreach (var instruction in instructions.Values)
                    {
                        Assert.AreEqual(
                            zone,
                            instruction.Target.GetZone(),
                            $"The random target must remain inside ball zone {zone} for {teamIndex}."
                        );
                        Assert.IsFalse(
                            instruction.Target.InAnyGkArea(),
                            $"An outfield target in zone {zone} must avoid the goalkeeper areas."
                        );
                    }

                    var nextInstructions = planner.CreateInstructions(team, opponent);
                    Assert.IsTrue(
                        instructions.Any(pair => pair.Value.Target != nextInstructions[pair.Key].Target),
                        $"Repeated planning in zone {zone} should randomize the targets for {teamIndex}."
                    );
                }
            }
            finally
            {
                Random.state = previousRandomState;
            }
        }

        [Test]
        public void ProcessTurn_OpponentHasBallOnTeamSide_SelectsConfiguredClosestPlayersToBlockShot()
        {
            var holderPosition = new Vector2(200f, 210f);
            SetUserPlayerWithBall(TeamIndex.Away, holderPosition);
            foreach (ServerPlayerState player in m_home.Skip(1))
                player.ChangePlayerMode(PlayerModeEnum.Confrontation);

            m_processor.ProcessTurn();

            var blockers = m_home.Skip(1).Where(player => player.AI.CurrentState == PlayerAIState.BlockShot).ToArray();
            Assert.AreEqual(
                TeamPositioningPlanner.k_BlockShotPlayersCount,
                blockers.Count(),
                "The defending team should assign the configured number of closest players to block the shot."
            );
            Assert.IsFalse(
                m_home.Skip(1).Any(player => player.AI.CurrentState == PlayerAIState.ReachBallZone),
                "Players should block the shot instead of chasing the ball when it is on their own side."
            );
            foreach (ServerPlayerState blocker in blockers)
            {
                var command = m_commands.Single(item =>
                    item.TeamIdx == TeamIndex.Home && item.PlayerIdx == blocker.Slot.PlayerIdx
                );
                Vector2 blockingCenter = Vector2.MoveTowards(
                    holderPosition,
                    PitchUtils.OpponentGoalCenter(TeamIndex.Away),
                    TeamPositioningPlanner.k_ShotBlockDistance
                );
                const float maximumTargetSpread = 15f;
                Assert.LessOrEqual(
                    Vector2.Distance(blockingCenter, command.TargetPosition),
                    maximumTargetSpread,
                    "Each blocker should stay near the configured shot-blocking position."
                );
                Assert.AreEqual(
                    PlayerModeEnum.Interception,
                    blocker.PlayerMode,
                    "A bot blocking a shot should switch from confrontation to interception."
                );
                Assert.Less(
                    command.TargetPosition.x,
                    holderPosition.x,
                    "A home-team blocker should move between the opponent and the home goal."
                );
            }
            Assert.AreEqual(
                blockers.Length,
                blockers
                    .Select(player =>
                        m_commands
                            .Single(command =>
                                command.TeamIdx == TeamIndex.Home && command.PlayerIdx == player.Slot.PlayerIdx
                            )
                            .TargetPosition
                    )
                    .Distinct()
                    .Count(),
                "Every blocker should receive a different target."
            );
        }

        [Test]
        public void ProcessTurn_OpponentHasBallOnTeamSide_ClosestImmobilePlayerConsumesOneBlockShotSelection()
        {
            var unselectedPlayersPosition = new Vector2(100f, 300f);
            MovePlayers(m_home, unselectedPlayersPosition);
            m_game.HomeTeam.GetGk().Server_PlayerMove(PitchUtils.PitchBounds.Min);
            var closestPlayer = ServerPlayerState.CreateForTest(
                1,
                TeamIndex.Home,
                2,
                42,
                "Immobile bot",
                new Vector2(199f, 210f),
                actionPoints: 0f
            );
            m_home[1] = closestPlayer;
            SetUserPlayerWithBall(TeamIndex.Away, new Vector2(200f, 210f));

            m_processor.ProcessTurn();

            Assert.AreEqual(
                TeamPositioningPlanner.k_BlockShotPlayersCount - 1,
                CountPlayersInState(m_home, PlayerAIState.BlockShot),
                "The closest immobile player should consume one configured selection without falling back to another bot."
            );
        }

        [Test]
        public void ProcessTurn_OpponentHasBallOnOpponentSide_SelectsConfiguredClosestPlayersToReachBallZone()
        {
            SetUserPlayerWithBall(TeamIndex.Away, new Vector2(450f, 210f));

            m_processor.ProcessTurn();

            Assert.AreEqual(
                TeamPositioningPlanner.k_ReachBallPlayersCount,
                CountPlayersInState(m_home, PlayerAIState.ReachBallZone),
                "The defending team should send the configured number of closest players toward the distant ball."
            );
            Assert.IsFalse(
                m_home.Skip(1).Any(player => player.AI.CurrentState == PlayerAIState.BlockShot),
                "Players should chase the ball instead of blocking a shot when it is on the opponent's side."
            );
            foreach (
                ServerPlayerState player in m_home
                    .Skip(1)
                    .Where(player => player.AI.CurrentState == PlayerAIState.ReachBallZone)
            )
            {
                Assert.AreEqual(
                    PlayerModeEnum.Confrontation,
                    player.PlayerMode,
                    "A bot reaching the opponent's ball zone should switch to confrontation."
                );
            }
        }

        [Test]
        public void ProcessTurn_TeamHasBall_SelectsPlayerClosestToAttackingEndToSupportAttack()
        {
            MovePlayers(m_home, new Vector2(100f, 300f));
            MovePlayers(m_away, new Vector2(300f, 300f));
            SetUserPlayerWithBall(TeamIndex.Home, new Vector2(200f, 210f));
            var supportPlayer = m_home[8];
            supportPlayer.Server_PlayerMove(new Vector2(350f, 100f));
            m_away[0].Server_PlayerMove(new Vector2(600f, 210f));
            var secondLastOpponent = m_away[1];
            secondLastOpponent.Server_PlayerMove(new Vector2(400f, 300f));
            m_commands.Add(CreateCommand(secondLastOpponent, PlayerActionEnum.Move, new Vector2(550f, 300f)));
            var previousRandomState = Random.state;
            Random.InitState(456);

            try
            {
                m_processor.ProcessTurn();
            }
            finally
            {
                Random.state = previousRandomState;
            }

            Assert.AreEqual(PlayerAIState.SupportAttack, supportPlayer.AI.CurrentState);
            Assert.AreEqual(1, CountPlayersInState(m_home, PlayerAIState.SupportAttack));
            var command = m_commands.Single(item =>
                item.TeamIdx == TeamIndex.Home && item.PlayerIdx == supportPlayer.Slot.PlayerIdx
            );
            Assert.Greater(command.TargetPosition.x, 350f);
            Assert.Less(command.TargetPosition.x, secondLastOpponent.Position.x);
            Assert.AreEqual(550f, secondLastOpponent.Position.x, 0.001f);
        }

        [Test]
        public void ProcessTurn_TeamHasBall_MostAdvancedUserConsumesOneSupportAttackSelection()
        {
            MovePlayers(m_home, new Vector2(100f, 300f));
            MovePlayers(m_away, new Vector2(300f, 300f));
            SetUserPlayerWithBall(TeamIndex.Home, new Vector2(200f, 210f));
            const byte mostAdvancedUserPlayerIdx = 3;
            const float candidateSpacing = 10f;
            ServerPlayerState mostAdvancedUser = ReplaceWithUserPlayer(
                TeamIndex.Home,
                mostAdvancedUserPlayerIdx,
                new Vector2(350f, 100f)
            );
            for (
                int selectionOffset = 1;
                selectionOffset < TeamPositioningPlanner.k_AttackingPlayersCount;
                selectionOffset++
            )
            {
                byte candidatePlayerIdx = (byte)(mostAdvancedUserPlayerIdx + selectionOffset);
                m_home[candidatePlayerIdx - 1]
                    .Server_PlayerMove(
                        mostAdvancedUser.Position + new Vector2(-candidateSpacing * selectionOffset, 0f)
                    );
            }
            m_away[0].Server_PlayerMove(new Vector2(600f, 210f));
            m_away[1].Server_PlayerMove(new Vector2(400f, 300f));

            m_processor.ProcessTurn();

            Assert.AreEqual(
                TeamPositioningPlanner.k_AttackingPlayersCount - 1,
                CountPlayersInState(m_home, PlayerAIState.SupportAttack),
                "The most advanced user should consume one configured selection without falling back to another bot."
            );
        }

        [Test]
        public void ProcessTurn_AttackingBotNoticesOffsideAndMovesBehindSafeLine()
        {
            SetUserPlayerWithBall(TeamIndex.Home, new Vector2(200f, 210f));
            var offsidePlayer = m_home[8];
            offsidePlayer.Server_PlayerMove(new Vector2(450f, 100f));
            MovePlayers(m_away, new Vector2(300f, 300f));
            m_away[0].Server_PlayerMove(new Vector2(600f, 210f));
            m_away[1].Server_PlayerMove(new Vector2(400f, 300f));
            var previousRandomState = Random.state;
            Random.InitState(123);

            try
            {
                m_processor.ProcessTurn();
            }
            finally
            {
                Random.state = previousRandomState;
            }

            Assert.AreEqual(PlayerAIState.RecoverOnside, offsidePlayer.AI.CurrentState);
            var command = m_commands.Single(item =>
                item.TeamIdx == TeamIndex.Home && item.PlayerIdx == offsidePlayer.Slot.PlayerIdx
            );
            Assert.LessOrEqual(command.TargetPosition.x, 390f);
            Assert.GreaterOrEqual(command.TargetPosition.x, 370f);
            Assert.AreEqual(offsidePlayer.Position.y, command.TargetPosition.y, 0.001f);
        }

        [Test]
        public void ProcessTurn_AttackingBotOnOwnHalf_DoesNotRecoverOnside()
        {
            SetUserPlayerWithBall(TeamIndex.Home, new Vector2(200f, 210f));
            var player = m_home[8];
            player.Server_PlayerMove(new Vector2(PitchUtils.PitchBounds.Center.x - 1f, 100f));
            MovePlayers(m_away, new Vector2(PitchUtils.PitchBounds.Center.x - 20f, 300f));
            m_away[0].Server_PlayerMove(new Vector2(600f, 210f));

            m_processor.ProcessTurn();

            Assert.AreNotEqual(PlayerAIState.RecoverOnside, player.AI.CurrentState);
        }

        [TestCase(PlayerActionEnum.LowPass)]
        [TestCase(PlayerActionEnum.HighPass)]
        public void ProcessTurn_PassToOffsidePlayer_UsesStartPositionAndAwardsFreeKick(PlayerActionEnum passAction)
        {
            MakeAllPlayersUsers();
            MovePlayers(m_home, new Vector2(100f, 300f));
            MovePlayers(m_away, new Vector2(400f, 300f));
            var passer = ReplaceWithUserPlayer(TeamIndex.Home, 2, new Vector2(100f, 100f), hasBall: true);
            var receiver = ReplaceWithUserPlayer(TeamIndex.Home, 3, new Vector2(500f, 100f));
            m_away[0].Server_PlayerMove(new Vector2(600f, 210f));
            var frreKickPlayer = m_away[2];
            frreKickPlayer.Server_PlayerMove(new Vector2(401f, 300f));
            var receiverEndPosition = new Vector2(300f, 100f);
            m_game.MoveBall(passer.BallSlotPosition);
            m_commands.Add(CreateCommand(receiver, PlayerActionEnum.Move, receiverEndPosition));
            m_commands.Add(CreateCommand(passer, passAction, receiverEndPosition));

            var result = m_processor.ProcessTurn();

            Assert.AreEqual(TurnResultTypeEnum.Offside, result.TurnResultType);
            Assert.AreEqual(1, result.Commentary.Count(cue => cue.Kind == MatchCommentaryKind.Offside));
            Assert.IsFalse(result.Commentary.Any(cue => cue.Kind == MatchCommentaryKind.WinBallConfrontation));
            Assert.IsTrue(result.Commentary.Single(cue => cue.Kind == MatchCommentaryKind.Offside).AfterEvent);
            Assert.AreEqual(receiver.Slot.PlayerKey, result.OffsidePlayer);
            Assert.AreEqual(frreKickPlayer.Slot.PlayerKey, result.PlayerWithBall);
            Assert.IsFalse(receiver.HasBall);
            Assert.IsTrue(frreKickPlayer.HasBall);
            Assert.AreEqual(frreKickPlayer.BallSlotPosition, m_game.BallPosition);
        }

        [TestCase(PlayerActionEnum.LowPass)]
        [TestCase(PlayerActionEnum.HighPass)]
        public void ProcessTurn_PassToPlayerOnOwnHalf_IsNotOffside(PlayerActionEnum passAction)
        {
            MakeAllPlayersUsers();
            MovePlayers(m_home, new Vector2(PitchUtils.PitchBounds.Center.x - 30f, 300f));
            MovePlayers(m_away, new Vector2(PitchUtils.PitchBounds.Center.x - 20f, 300f));
            var passer = ReplaceWithUserPlayer(TeamIndex.Home, 2, new Vector2(100f, 100f), hasBall: true);
            var receiver = ReplaceWithUserPlayer(
                TeamIndex.Home,
                3,
                new Vector2(PitchUtils.PitchBounds.Center.x - 1f, 100f)
            );
            m_away[0].Server_PlayerMove(new Vector2(600f, 210f));
            m_away[1].Server_PlayerMove(new Vector2(PitchUtils.PitchBounds.Center.x - 19f, 300f));
            var receiverEndPosition = new Vector2(PitchUtils.PitchBounds.Center.x + 1f, 100f);
            m_game.MoveBall(passer.BallSlotPosition);
            m_commands.Add(CreateCommand(receiver, PlayerActionEnum.Move, receiverEndPosition));
            m_commands.Add(CreateCommand(passer, passAction, receiverEndPosition));

            var result = m_processor.ProcessTurn();

            Assert.AreEqual(TurnResultTypeEnum.None, result.TurnResultType);
            Assert.IsTrue(result.OffsidePlayer.IsEmpty);
            Assert.AreEqual(receiver.Slot.PlayerKey, result.PlayerWithBall);
            var passCue = result.Commentary.Single(cue => !cue.AfterEvent);
            Assert.AreEqual(passer.Slot.PlayerKey, passCue.Player);
            Assert.IsInstanceOf<BallMoveEvent>(result.Events[passCue.EventIndex]);
            var receiptCue = result.Commentary.Single(cue => cue.Kind == MatchCommentaryKind.WinBallConfrontation);
            Assert.AreEqual(receiver.Slot.PlayerKey, receiptCue.Player);
            Assert.IsTrue(receiptCue.AfterEvent);
            Assert.IsTrue(receiver.HasBall);
        }

        [TestCase(PlayerActionEnum.LowPass)]
        [TestCase(PlayerActionEnum.HighPass)]
        public void ProcessTurn_PassWithTwoOpponentsCloserToGoal_IsNotOffside(PlayerActionEnum passAction)
        {
            MakeAllPlayersUsers();
            MovePlayers(m_home, new Vector2(100f, 300f));
            MovePlayers(m_away, new Vector2(300f, 300f));
            var passer = ReplaceWithUserPlayer(TeamIndex.Home, 2, new Vector2(100f, 100f), hasBall: true);
            var receiver = ReplaceWithUserPlayer(TeamIndex.Home, 3, new Vector2(450f, 100f));
            m_away[0].Server_PlayerMove(new Vector2(600f, 210f));
            m_away[1].Server_PlayerMove(new Vector2(550f, 300f));
            m_game.MoveBall(passer.BallSlotPosition);
            m_commands.Add(CreateCommand(passer, passAction, receiver.Position));

            var result = m_processor.ProcessTurn();

            Assert.AreEqual(TurnResultTypeEnum.None, result.TurnResultType);
            Assert.IsTrue(result.OffsidePlayer.IsEmpty);
            Assert.AreEqual(receiver.Slot.PlayerKey, result.PlayerWithBall);
            Assert.IsTrue(receiver.HasBall);
        }

        [Test]
        public void ProcessTurn_BallMoves_UsesAdaptiveTargetsFromBeforeResolution()
        {
            m_game.MoveBall(new Vector2(345f, 210f));
            var homeBase = m_home[1].FormationPosition;
            var awayBase = m_away[1].FormationPosition;

            m_processor.ProcessTurn();

            Assert.AreEqual(homeBase + new Vector2(20f, 0f), m_home[1].Position);
            Assert.AreEqual(awayBase + new Vector2(20f, 0f), m_away[1].Position);
            Assert.AreEqual(homeBase, m_home[1].FormationPosition);
            Assert.AreEqual(awayBase, m_away[1].FormationPosition);
        }

        [Test]
        public void ProcessTurn_GoalkeepersMoveToGuardBallAcrossGoalMouth()
        {
            m_game.MoveBall(new Vector2(PitchUtils.PitchBounds.Center.x, PitchUtils.PitchBounds.Max.y));

            m_processor.ProcessTurn();

            Assert.AreEqual(m_home[0].FormationPosition.x, m_home[0].Position.x, 0.001f);
            Assert.AreEqual(PitchUtils.GoalRangeY.Max, m_home[0].Position.y, 0.001f);
            Assert.AreEqual(m_away[0].FormationPosition.x, m_away[0].Position.x, 0.001f);
            Assert.AreEqual(PitchUtils.GoalRangeY.Max, m_away[0].Position.y, 0.001f);
        }

        [Test]
        public void ProcessTurn_UsesOffensiveAndDefensiveFormationModesFromPossession()
        {
            var playerWithBall = ServerPlayerState.CreateForTest(
                1,
                TeamIndex.Home,
                9,
                42,
                "User with ball",
                m_home[8].Position,
                hasBall: true,
                userId: 100
            );
            m_home[8] = playerWithBall;
            m_game.MoveBall(playerWithBall.BallSlotPosition);

            m_processor.ProcessTurn();

            Assert.AreEqual(TeamPositioningMode.Offensive, m_game.HomeTeam.PositioningMode);
            Assert.AreEqual(TeamPositioningMode.Defensive, m_game.AwayTeam.PositioningMode);
        }

        [Test]
        public void UpdatePositioningBounds_OutfieldHolderAnchorsAssignedFormationSlot()
        {
            var holder = ServerPlayerState.CreateForTest(
                1,
                TeamIndex.Home,
                2,
                42,
                "Holder",
                new Vector2(230f, 320f),
                hasBall: true,
                userId: 100
            );
            m_home[1] = holder;
            m_game.MoveBall(holder.BallSlotPosition);

            m_game.UpdatePositioningBounds();

            var assignment = m_game.HomeTeam.GetDynamicFormationPositions()[2];
            Assert.AreEqual(holder.Position.x, assignment.Target.x, 0.001f);
            Assert.AreEqual(holder.Position.y, assignment.Target.y, 0.001f);
        }

        [Test]
        public void UpdatePositioningBounds_GoalkeeperHolderUsesScaledStartingPositions()
        {
            var goalkeeper = ServerPlayerState.CreateForTest(
                1,
                TeamIndex.Home,
                1,
                42,
                "Goalkeeper",
                m_home[0].Position,
                hasBall: true
            );
            m_home[0] = goalkeeper;
            m_game.MoveBall(new Vector2(100f, 100f));

            m_game.UpdatePositioningBounds();

            for (byte playerIdx = 2; playerIdx <= 11; playerIdx++)
            {
                Assert.AreEqual(
                    m_home[playerIdx - 1].FormationPosition,
                    m_game.HomeTeam.GetAdaptiveFormationPosition(playerIdx)
                );
            }
        }

        [Test]
        public void UpdatePositioningBounds_AfterBallMoveRefreshesFormationBounds()
        {
            float initialCenter = m_game.HomeTeam.FormationBounds.Center.x;

            m_game.MoveBall(new Vector2(405f, 210f));
            m_game.UpdatePositioningBounds();

            Assert.Greater(m_game.HomeTeam.FormationBounds.Center.x, initialCenter);
            Assert.AreEqual(TeamPositioningMode.Neutral, m_game.HomeTeam.PositioningMode);
            Assert.AreEqual(TeamPositioningMode.Neutral, m_game.AwayTeam.PositioningMode);
        }

        [Test]
        public void ClearPlayerWithBall_RemovesPossessionFromCurrentHolder()
        {
            var player = m_home[1];
            player.HasBall = true;
            Assert.AreSame(player, m_game.GetPlayerWithBall());

            m_game.ClearPlayerWithBall();

            Assert.IsFalse(player.HasBall);
            Assert.IsNull(m_game.GetPlayerWithBall());
        }

        [Test]
        public void ProcessTurn_EditorQueuedPlayerMoveOverridesAIAndCreatesMoveEvent()
        {
            var player = m_home[1];
            var target = player.Position + new Vector2(17f, -9f);
            Assert.IsNull(m_processor.EditorQueuePlayerMove(TeamIndex.Home, 2, target));

            var result = m_processor.ProcessTurn();

            Assert.AreEqual(target, player.Position);
            var moveEvent = result
                .Events.OfType<PlayerMoveEvent>()
                .Single(e => e.TeamIdx == TeamIndex.Home && e.PlayerIdx == 2);
            Assert.AreEqual(target, moveEvent.ToPosition);
        }

        [Test]
        public void ProcessTurn_UserGoalkeeperWithBallAndNoCommand_MakesHighPass()
        {
            var goalkeeper = ServerPlayerState.CreateForTest(
                1,
                TeamIndex.Home,
                1,
                42,
                "User goalkeeper",
                Formation442.GetPosition(TeamIndex.Home, 1),
                hasBall: true,
                userId: 100
            );
            m_home[0] = goalkeeper;
            m_game.MoveBall(goalkeeper.BallSlotPosition);
            var previousRandomState = Random.state;
            Random.InitState(123);

            try
            {
                m_processor.ProcessTurn();
            }
            finally
            {
                Random.state = previousRandomState;
            }

            var command = m_commands.Single(cmd => cmd.TeamIdx == TeamIndex.Home && cmd.PlayerIdx == 1);
            Assert.AreEqual(PlayerActionEnum.HighPass, command.Action);
            Assert.IsTrue(command.TargetPosition.IsOnPitch());
            Assert.Greater(command.TargetPosition.x, goalkeeper.Position.x);
        }

        [Test]
        public void ProcessTurn_UserGoalkeeperWithQueuedCommand_DoesNotAddAutomaticPass()
        {
            var goalkeeper = ServerPlayerState.CreateForTest(
                1,
                TeamIndex.Home,
                1,
                42,
                "User goalkeeper",
                Formation442.GetPosition(TeamIndex.Home, 1),
                hasBall: true,
                userId: 100
            );
            m_home[0] = goalkeeper;
            m_game.MoveBall(goalkeeper.BallSlotPosition);
            var command = new PlayerCommand(
                7,
                TeamIndex.Home,
                1,
                goalkeeper.PlayerId,
                PlayerActionEnum.HighPass,
                goalkeeper.Position + new Vector2(230f, 0f)
            );
            Assert.IsNull(goalkeeper.Server_RegisterMoveCmd(ref command));
            m_commands.Add(command);

            m_processor.ProcessTurn();

            var goalkeeperCommands = m_commands
                .Where(cmd => cmd.TeamIdx == TeamIndex.Home && cmd.PlayerIdx == 1)
                .ToArray();
            Assert.AreEqual(1, goalkeeperCommands.Length);
            Assert.AreEqual(command.CommandId, goalkeeperCommands[0].CommandId);
        }

        [TestCase(TeamIndex.Home, 6, 2, 22, 25)]
        [TestCase(TeamIndex.Home, 11, 2, 22, 25)]
        [TestCase(TeamIndex.Home, 16, 2, 22, 25)]
        [TestCase(TeamIndex.Away, 10, 4, 24, 21)]
        [TestCase(TeamIndex.Away, 15, 4, 24, 21)]
        [TestCase(TeamIndex.Away, 20, 4, 24, 21)]
        public void ProcessTurn_BotGoalkeeper_SelectsBestScoredZoneOutsideOwnPenaltyArea(
            TeamIndex team,
            int forbiddenZone,
            int betterZone,
            int worseZone,
            int opponentFallbackZone
        )
        {
            var ownPlayers = team == TeamIndex.Home ? m_home : m_away;
            var opponents = team == TeamIndex.Home ? m_away : m_home;
            var goalkeeper = ServerPlayerState.CreateForTest(
                1,
                team,
                1,
                42,
                "Bot goalkeeper",
                Formation442.GetPosition(team, 1),
                hasBall: true
            );
            ownPlayers[0] = goalkeeper;
            MoveOutfieldPlayers(ownPlayers, forbiddenZone);
            MoveOutfieldPlayers(opponents, opponentFallbackZone);
            ownPlayers[1].Server_PlayerMove(PitchUtils.GetZoneCenter(betterZone));
            ownPlayers[2].Server_PlayerMove(PitchUtils.GetZoneCenter(betterZone));
            ownPlayers[3].Server_PlayerMove(PitchUtils.GetZoneCenter(betterZone));
            ownPlayers[4].Server_PlayerMove(PitchUtils.GetZoneCenter(worseZone));
            opponents[1].Server_PlayerMove(PitchUtils.GetZoneCenter(betterZone));
            m_game.MoveBall(goalkeeper.BallSlotPosition);
            var previousRandomState = Random.state;
            Random.InitState(789);

            try
            {
                m_processor.ProcessTurn();
            }
            finally
            {
                Random.state = previousRandomState;
            }

            var command = m_commands.Single(cmd => cmd.TeamIdx == team && cmd.PlayerIdx == 1);
            Assert.AreEqual(PlayerActionEnum.HighPass, command.Action);
            Assert.AreEqual(betterZone, command.TargetPosition.GetZone());
            Assert.AreNotEqual(forbiddenZone, command.TargetPosition.GetZone());
        }

        [Test]
        public void ProcessTurn_BotGoalkeeper_UsesLowPassWhenBestZoneIsInLowPassRange()
        {
            var goalkeeper = ServerPlayerState.CreateForTest(
                1,
                TeamIndex.Home,
                1,
                42,
                "Bot goalkeeper",
                Formation442.GetPosition(TeamIndex.Home, 1),
                hasBall: true,
                moveSkill: 8,
                lowPassSkill: 20,
                longPassSkill: 15,
                crossesSkill: 15,
                shootingSkill: 15,
                longShotsSkill: 15
            );
            m_home[0] = goalkeeper;
            MoveOutfieldPlayers(m_home, 7);
            MoveOutfieldPlayers(m_away, 25);
            m_game.MoveBall(goalkeeper.BallSlotPosition);

            m_processor.ProcessTurn();

            var command = m_commands.Single(cmd => cmd.TeamIdx == TeamIndex.Home && cmd.PlayerIdx == 1);
            Assert.AreEqual(PlayerActionEnum.LowPass, command.Action);
            Assert.AreEqual(7, command.TargetPosition.GetZone());
        }

        [Test]
        public void ProcessTurn_BotGoalkeeper_HighPassesIntoOverlappingZoneWhenCenterIsOutOfRange()
        {
            const int targetZone = 8;
            var goalkeeper = ServerPlayerState.CreateForTest(
                1,
                TeamIndex.Home,
                1,
                42,
                "Bot goalkeeper",
                Formation442.GetPosition(TeamIndex.Home, 1),
                hasBall: true
            );
            m_home[0] = goalkeeper;
            MoveOutfieldPlayers(m_home, targetZone);
            MoveOutfieldPlayers(m_away, 25);
            m_game.MoveBall(goalkeeper.BallSlotPosition);
            float minimumHighPassDistance = goalkeeper.MinHighPassDistance;
            float availableHighPassDistance = goalkeeper.AvailableHighPassDistance;
            Assert.Greater(
                Vector2.Distance(goalkeeper.Position, PitchUtils.GetZoneCenter(targetZone)),
                availableHighPassDistance
            );

            m_processor.ProcessTurn();

            var command = m_commands.Single(cmd => cmd.TeamIdx == TeamIndex.Home && cmd.PlayerIdx == 1);
            float distance = Vector2.Distance(goalkeeper.Position, command.TargetPosition);
            Assert.AreEqual(PlayerActionEnum.HighPass, command.Action);
            Assert.IsTrue(PitchUtils.GetZoneBounds(targetZone).Contains(command.TargetPosition));
            Assert.AreEqual(targetZone, command.TargetPosition.GetZone());
            Assert.GreaterOrEqual(distance, minimumHighPassDistance);
            Assert.LessOrEqual(distance, availableHighPassDistance);
        }

        private static ServerPlayerState[] CreatePlayers(TeamIndex team)
        {
            var players = new ServerPlayerState[11];
            for (byte i = 1; i <= 11; i++)
                players[i - 1] = ServerPlayerState.CreateForTest(1, team, i);
            return players;
        }

        private static void MoveOutfieldPlayers(ServerPlayerState[] players, int zone)
        {
            var position = PitchUtils.GetZoneCenter(zone);
            for (int i = 1; i < players.Length; i++)
                players[i].Server_PlayerMove(position);
        }

        private void MakeAllPlayersUsers()
        {
            for (byte playerIdx = 1; playerIdx <= 11; playerIdx++)
            {
                ReplaceWithUserPlayer(TeamIndex.Home, playerIdx, m_home[playerIdx - 1].Position);
                ReplaceWithUserPlayer(TeamIndex.Away, playerIdx, m_away[playerIdx - 1].Position);
            }
        }

        private ServerPlayerState ReplaceWithUserPlayer(
            TeamIndex team,
            byte playerIdx,
            Vector2 position,
            bool hasBall = false
        )
        {
            var players = team == TeamIndex.Home ? m_home : m_away;
            var player = ServerPlayerState.CreateForTest(
                1,
                team,
                playerIdx,
                1000 + (int)team * 100 + playerIdx,
                $"{team} player {playerIdx}",
                position,
                hasBall: hasBall,
                userId: 1000 + (int)team * 100 + playerIdx,
                playerMode: PlayerModeEnum.Confrontation
            );
            players[playerIdx - 1] = player;
            return player;
        }

        private static void MovePlayers(ServerPlayerState[] players, Vector2 position)
        {
            foreach (var player in players)
                player.Server_PlayerMove(position);
        }

        private static PlayerCommand CreateCommand(ServerPlayerState player, PlayerActionEnum action, Vector2 target) =>
            new(7, player.Slot.TeamIdx, player.Slot.PlayerIdx, player.PlayerId, action, target);

        private void SetUserPlayerWithBall(TeamIndex team, Vector2 position)
        {
            var player = ServerPlayerState.CreateForTest(
                1,
                team,
                2,
                42,
                "User with ball",
                position,
                hasBall: true,
                userId: 100
            );
            if (team == TeamIndex.Home)
                m_home[1] = player;
            else
                m_away[1] = player;
            m_game.MoveBall(player.BallSlotPosition);
        }

        private static int CountPlayersInState(ServerPlayerState[] players, PlayerAIState state) =>
            players.Skip(1).Count(player => player.AI.CurrentState == state);
    }
}
