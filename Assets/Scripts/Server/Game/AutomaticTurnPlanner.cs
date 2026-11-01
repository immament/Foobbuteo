// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using System.Linq;
using Imma.Common;
using Imma.Foobbuteo.Server.Game.Model;
using Imma.Foobbuteo.Shared.Game;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using UnityEngine;

namespace Imma.Foobbuteo.Server.Game
{
    /// <summary>
    /// Plans every automatic command from one pre-resolution snapshot.
    /// </summary>
    internal sealed class AutomaticTurnPlanner
    {
        private readonly ServerGameState m_gameState;
        private readonly List<PlayerCommand> m_commands;
        private readonly HashSet<PlayerKey> m_queuedPlayers;
        private readonly TeamPositioningPlanner m_positioningPlanner;

        internal AutomaticTurnPlanner(ServerGameState gameState, List<PlayerCommand> commands)
        {
            m_gameState = gameState;
            m_commands = commands;
            m_queuedPlayers = commands.Select(command => new PlayerKey(command.TeamIdx, command.PlayerIdx)).ToHashSet();
            m_positioningPlanner = new TeamPositioningPlanner(gameState, HasQueuedCommand);
        }

        internal void AddAutomaticCommands()
        {
            m_gameState.UpdatePositioningBounds();

            // Create both plans before registering a command so neither team observes
            // movement or action-point changes caused by the other team's plan.
            TeamTurnPlan homePlan = CreatePlan(m_gameState.HomeTeam, m_gameState.AwayTeam);
            TeamTurnPlan awayPlan = CreatePlan(m_gameState.AwayTeam, m_gameState.HomeTeam);
            for (byte playerIdx = 1; playerIdx <= 11; playerIdx++)
            {
                AddCommand(homePlan, playerIdx);
                AddCommand(awayPlan, playerIdx);
            }
        }

        private TeamTurnPlan CreatePlan(ServerTeamState team, ServerTeamState opponentTeam) =>
            new(team, team.GetDynamicFormationPositions(), m_positioningPlanner.CreateInstructions(team, opponentTeam));

        private void AddCommand(TeamTurnPlan plan, byte playerIdx)
        {
            ServerPlayerState player = plan.Team.GetPlayerAtIdx(playerIdx);
            if (HasQueuedCommand(player))
                return;

            bool needsGoalkeeperPass = player.IsGk && player.HasBall;
            if (!player.UsesAutomaticControl && !needsGoalkeeperPass)
                return;

            Vector2 formationTarget = plan.Formation.TryGetValue(playerIdx, out DynamicFormationAssignment assignment)
                ? assignment.Target
                : plan.Team.GetAdaptiveFormationPosition(playerIdx);
            PlayerAIPositioningInstruction? instruction = plan.Instructions.TryGetValue(playerIdx, out var value)
                ? value
                : null;
            PlayerCommand? proposal = player.AI.Decide(
                m_gameState.CurrentTurn,
                formationTarget,
                m_gameState.BallPosition,
                plan.Team,
                m_gameState.GetOpponentTeam(plan.Team.TeamIdx),
                instruction
            );
            if (!proposal.HasValue)
                return;

            PlayerCommand command = proposal.Value;
            if (player.Server_RegisterMoveCmd(ref command) is string reason)
            {
                this.LError($"{m_gameState.InfoTag} Bot command {command.Action} rejected for {player.Slot}: {reason}");
                return;
            }

            m_commands.Add(command);
            m_queuedPlayers.Add(player.Slot.PlayerKey);
        }

        private bool HasQueuedCommand(ServerPlayerState player) => m_queuedPlayers.Contains(player.Slot.PlayerKey);

        private readonly struct TeamTurnPlan
        {
            internal ServerTeamState Team { get; }
            internal IReadOnlyDictionary<byte, DynamicFormationAssignment> Formation { get; }
            internal IReadOnlyDictionary<byte, PlayerAIPositioningInstruction> Instructions { get; }

            internal TeamTurnPlan(
                ServerTeamState team,
                IReadOnlyDictionary<byte, DynamicFormationAssignment> formation,
                IReadOnlyDictionary<byte, PlayerAIPositioningInstruction> instructions
            )
            {
                Team = team;
                Formation = formation;
                Instructions = instructions;
            }
        }
    }

    /// <summary>
    /// Applies the first positioning rule that matches the current possession state.
    /// This is a Chain of Responsibility: adding a new match situation does not grow TurnProcessor.
    /// </summary>
    internal sealed class TeamPositioningPlanner
    {
        internal const int k_ReachBallPlayersCount = 2;
        internal const int k_BlockShotPlayersCount = 2;
        internal const int k_AttackingPlayersCount = 2;
        internal const float k_ShotBlockDistance = 40f;
        private const float k_PositioningTargetSpread = 10f;
        private const float k_OffsideAwarenessChance = 0.9f;
        private const float k_OffsideSafeDistance = 20f;
        private const float k_OffsideSafeDistanceRandom = 10f;

        private readonly ServerGameState m_gameState;
        private readonly Func<ServerPlayerState, bool> m_hasQueuedCommand;
        private readonly IPositioningRule[] m_rules;

        internal TeamPositioningPlanner(ServerGameState gameState, Func<ServerPlayerState, bool> hasQueuedCommand)
        {
            m_gameState = gameState;
            m_hasQueuedCommand = hasQueuedCommand;
            m_rules = new IPositioningRule[] { new LooseBallRule(), new AttackingRule(), new DefendingRule() };
        }

        internal IReadOnlyDictionary<byte, PlayerAIPositioningInstruction> CreateInstructions(
            ServerTeamState team,
            ServerTeamState opponentTeam
        )
        {
            var result = new Dictionary<byte, PlayerAIPositioningInstruction>();
            var context = new PositioningContext(
                m_gameState,
                team,
                opponentTeam,
                GetPlayers(team),
                IsValidCandidate,
                result
            );
            foreach (IPositioningRule rule in m_rules)
            {
                if (rule.TryApply(context))
                    break;
            }
            return result;
        }

        private static ServerPlayerState[] GetPlayers(ServerTeamState team) =>
            Enumerable.Range(1, 11).Select(playerIdx => team.GetPlayerAtIdx((byte)playerIdx)).ToArray();

        private bool IsValidCandidate(ServerPlayerState player) =>
            !player.IsGk
            && player.UsesAutomaticControl
            && !player.HasBall
            && player.AvailableMoveDistance > 0f
            && !m_hasQueuedCommand(player);

        private interface IPositioningRule
        {
            bool TryApply(PositioningContext context);
        }

        private sealed class LooseBallRule : IPositioningRule
        {
            public bool TryApply(PositioningContext context)
            {
                if (context.PlayerWithBall != null)
                    return false;

                context.AssignClosestToBall(
                    PlayerAIState.ReachBallZone,
                    context.CreateBallZoneTarget,
                    k_ReachBallPlayersCount
                );
                return true;
            }
        }

        private sealed class AttackingRule : IPositioningRule
        {
            public bool TryApply(PositioningContext context)
            {
                if (context.PlayerWithBall?.Slot.TeamIdx != context.Team.TeamIdx)
                    return false;

                float safeOnsideX = GetSafeOnsideX(context.Team.TeamIdx, context.OpponentTeam);
                TryApplyRecoverOnside(context, safeOnsideX);

                TryApplySupportAttack(context, safeOnsideX);
                return true;
            }

            private static void TryApplySupportAttack(PositioningContext context, float safeOnsideX)
            {
                float attackEndX = PitchUtils.OpponentGoalCenter(context.Team.TeamIdx).x;
                foreach (
                    ServerPlayerState player in context
                        .Players.OrderBy(player => Mathf.Abs(attackEndX - player.Position.x))
                        .ThenBy(player => player.Slot.PlayerIdx)
                        .Take(k_AttackingPlayersCount)
                )
                {
                    if (!context.IsValidCandidate(player) || context.HasInstruction(player))
                        continue;

                    context.Assign(
                        player,
                        PlayerAIState.SupportAttack,
                        new Vector2(
                            safeOnsideX,
                            context.Team.GetAdaptiveFormationPosition(player.Slot.PlayerIdx).y
                        ).ClampToOutfield()
                    );
                }
            }

            private static void TryApplyRecoverOnside(PositioningContext context, float safeOnsideX)
            {
                foreach (ServerPlayerState player in context.Players.Where(context.IsValidCandidate))
                {
                    if (
                        !OffsideRule.IsOffsidePosition(
                            context.Team.TeamIdx,
                            player.Position.x,
                            context.OpponentTeam,
                            opponent => opponent.Position.x
                        )
                        || UnityEngine.Random.value > k_OffsideAwarenessChance
                    )
                        continue;

                    context.Assign(
                        player,
                        PlayerAIState.RecoverOnside,
                        new Vector2(safeOnsideX, player.Position.y).ClampToOutfield()
                    );
                }
            }
        }

        private sealed class DefendingRule : IPositioningRule
        {
            public bool TryApply(PositioningContext context)
            {
                if (context.PlayerWithBall == null || context.PlayerWithBall.Slot.TeamIdx == context.Team.TeamIdx)
                    return false;

                if (!PitchUtils.IsOnOwnHalf(context.Team.TeamIdx, context.GameState.BallPosition))
                {
                    context.AssignClosestToBall(
                        PlayerAIState.ReachBallZone,
                        context.CreateBallZoneTarget,
                        k_ReachBallPlayersCount
                    );
                    return true;
                }

                Vector2 ownGoal = PitchUtils.OpponentGoalCenter(context.OpponentTeam.TeamIdx);
                Vector2 target = Vector2
                    .MoveTowards(context.PlayerWithBall.Position, ownGoal, k_ShotBlockDistance)
                    .ClampToOutfield();
                var spread = Vector2.one * k_PositioningTargetSpread;
                var targetBounds = new Bounds2d((target - spread).ClampToPitch(), (target + spread).ClampToPitch());
                context.AssignClosestToBall(
                    PlayerAIState.BlockShot,
                    () => CreateRandomTarget(targetBounds),
                    k_BlockShotPlayersCount
                );
                return true;
            }
        }

        private static float GetSafeOnsideX(TeamIndex attackingTeam, ServerTeamState opponentTeam)
        {
            float opponentGoalX = PitchUtils.OpponentGoalCenter(attackingTeam).x;
            float offsideLineX = Enumerable
                .Range(1, 11)
                .Select(playerIdx => opponentTeam.GetPlayerAtIdx((byte)playerIdx))
                .OrderBy(player => Mathf.Abs(opponentGoalX - player.Position.x))
                .ThenBy(player => player.Slot.PlayerIdx)
                .Skip(1)
                .First()
                .Position.x;
            float directionToOwnGoal = -PitchUtils.ForwardDirectionX(attackingTeam);
            float safeDistance =
                k_OffsideSafeDistance
                + UnityEngine.Random.Range(-k_OffsideSafeDistanceRandom, k_OffsideSafeDistanceRandom);
            return offsideLineX + directionToOwnGoal * safeDistance;
        }

        private sealed class PositioningContext
        {
            internal ServerGameState GameState { get; }
            internal ServerTeamState Team { get; }
            internal ServerTeamState OpponentTeam { get; }
            internal IReadOnlyCollection<ServerPlayerState> Players { get; }
            internal ServerPlayerState PlayerWithBall { get; }
            private readonly Func<ServerPlayerState, bool> m_isValidCandidate;
            private readonly Dictionary<byte, PlayerAIPositioningInstruction> m_instructions;

            internal PositioningContext(
                ServerGameState gameState,
                ServerTeamState team,
                ServerTeamState opponentTeam,
                IReadOnlyCollection<ServerPlayerState> players,
                Func<ServerPlayerState, bool> isValidCandidate,
                Dictionary<byte, PlayerAIPositioningInstruction> instructions
            )
            {
                GameState = gameState;
                Team = team;
                OpponentTeam = opponentTeam;
                Players = players;
                PlayerWithBall = gameState.GetPlayerWithBall();
                m_isValidCandidate = isValidCandidate;
                m_instructions = instructions;
            }

            internal Vector2 CreateBallZoneTarget()
            {
                int zone = GameState.BallPosition.GetZone();
                Bounds2d bounds;
                if (PitchUtils.IsRegularZone(zone))
                {
                    Bounds2d zoneBounds = PitchUtils.GetZoneBounds(zone);
                    // Keep targets away from zone boundaries, including after outfield clamping.
                    Vector2 spread = (zoneBounds.Max - zoneBounds.Min) * 0.25f;
                    bounds = new Bounds2d(zoneBounds.Center - spread, zoneBounds.Center + spread);
                }
                else
                {
                    Vector2 center = PitchUtils.GetBallZoneTarget(GameState.BallPosition);
                    Vector2 spread = Vector2.one * k_PositioningTargetSpread;
                    bounds = new Bounds2d((center - spread).ClampToPitch(), (center + spread).ClampToPitch());
                }
                return CreateRandomTarget(bounds);
            }

            internal void AssignClosestToBall(PlayerAIState state, Func<Vector2> createTarget, int count)
            {
                foreach (
                    ServerPlayerState player in Players
                        .OrderBy(player => (player.Position - GameState.BallPosition).sqrMagnitude)
                        .ThenBy(player => player.Slot.PlayerIdx)
                        .Take(count)
                )
                {
                    if (IsValidCandidate(player))
                        Assign(player, state, createTarget());
                }
            }

            internal bool IsValidCandidate(ServerPlayerState player) => m_isValidCandidate(player);

            internal bool HasInstruction(ServerPlayerState player) => m_instructions.ContainsKey(player.Slot.PlayerIdx);

            internal void Assign(ServerPlayerState player, PlayerAIState state, Vector2 target) =>
                m_instructions[player.Slot.PlayerIdx] = new PlayerAIPositioningInstruction(state, target);
        }

        private static Vector2 CreateRandomTarget(Bounds2d bounds)
        {
            return new Vector2(
                UnityEngine.Random.Range(bounds.Min.x, bounds.Max.x),
                UnityEngine.Random.Range(bounds.Min.y, bounds.Max.y)
            ).ClampToOutfield();
        }
    }

    internal static class OffsideRule
    {
        internal static bool IsOffsidePosition(
            TeamIndex attackingTeam,
            float playerX,
            ServerTeamState opponentTeam,
            Func<ServerPlayerState, float> getOpponentX
        )
        {
            if (PitchUtils.IsOnOwnHalf(attackingTeam, playerX))
                return false;

            float opponentGoalX = PitchUtils.OpponentGoalCenter(attackingTeam).x;
            float playerGoalDistance = Mathf.Abs(opponentGoalX - playerX);
            int opponentsCloserToGoal = 0;
            for (byte playerIdx = 1; playerIdx <= 11; playerIdx++)
            {
                ServerPlayerState opponent = opponentTeam.GetPlayerAtIdx(playerIdx);
                if (Mathf.Abs(opponentGoalX - getOpponentX(opponent)) < playerGoalDistance)
                {
                    if (++opponentsCloserToGoal >= 2)
                        return false;
                }
            }
            return true;
        }
    }
}
