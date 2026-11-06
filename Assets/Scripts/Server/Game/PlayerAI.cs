// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System.Collections.Generic;
using System.Linq;
using Imma.Common;
using Imma.Foobbuteo.Server.Game.Model;
using Imma.Foobbuteo.Shared.Game;
using Imma.Foobbuteo.Shared.Game.Contract;
using UnityEngine;
using UnityHFSM;

namespace Imma.Foobbuteo.Server.Game
{
    internal enum PlayerAIState
    {
        MaintainFormation,
        ReachBallZone,
        BlockShot,
        SupportAttack,
        RecoverOnside,
        WithBall,
        Goalkeeper,
    }

    internal readonly struct PlayerAIPositioningInstruction
    {
        internal PlayerAIState State { get; }
        internal Vector2 Target { get; }

        internal PlayerAIPositioningInstruction(PlayerAIState state, Vector2 target)
        {
            State = state;
            Target = target;
        }
    }

    internal sealed class PlayerAI
    {
        private const float k_MaxShotDistanceSqr = 300f * 300f;
        private readonly ServerPlayerState m_player;
        private readonly StateMachine<PlayerAIState> m_fsm = new();
        private PlayerActionEnum? m_action;
        private Vector2 m_target;
        private Vector2 m_formationTarget;
        private PlayerAIPositioningInstruction? m_positioningInstruction;
        private Vector2 m_ballPosition;
        private ServerTeamState m_team;
        private ServerTeamState m_opponentTeam;

        internal PlayerAIState CurrentState => m_fsm.ActiveStateName;

        internal PlayerAI(ServerPlayerState player)
        {
            m_player = player;
            m_fsm.AddState(PlayerAIState.MaintainFormation, new MaintainFormation(this));
            m_fsm.AddState(PlayerAIState.ReachBallZone, new MoveToPositioningTarget(this));
            m_fsm.AddState(PlayerAIState.BlockShot, new MoveToPositioningTarget(this));
            m_fsm.AddState(PlayerAIState.SupportAttack, new MoveToPositioningTarget(this));
            m_fsm.AddState(PlayerAIState.RecoverOnside, new MoveToPositioningTarget(this));
            m_fsm.AddState(PlayerAIState.WithBall, new WithBall(this));
            m_fsm.AddState(PlayerAIState.Goalkeeper, new Goalkeeper(this));
            m_fsm.SetStartState(SelectState());
            m_fsm.Init();
        }

        // Called once before turn resolution; states propose actions without moving players.
        internal PlayerCommand? Decide(
            int turn,
            Vector2 formationTarget,
            Vector2 ballPosition,
            ServerTeamState team = null,
            ServerTeamState opponentTeam = null,
            PlayerAIPositioningInstruction? positioningInstruction = null
        )
        {
            m_formationTarget = formationTarget;
            m_positioningInstruction = positioningInstruction;
            m_ballPosition = ballPosition;
            m_team = team;
            m_opponentTeam = opponentTeam;
            m_action = null;
            var state = SelectState();
            if (CurrentState != state)
                m_fsm.RequestStateChange(state);
            m_fsm.OnLogic();

            if (!m_action.HasValue)
                return null;

            return new PlayerCommand(
                turn,
                m_player.Slot.TeamIdx,
                m_player.Slot.PlayerIdx,
                m_player.PlayerId,
                m_action.Value,
                m_target
            );
        }

        private PlayerAIState SelectState() =>
            m_player.IsGk ? PlayerAIState.Goalkeeper
            : m_player.HasBall ? PlayerAIState.WithBall
            : m_positioningInstruction?.State ?? PlayerAIState.MaintainFormation;

        private sealed class MaintainFormation : StateBase<PlayerAIState>
        {
            private readonly PlayerAI m_ai;

            internal MaintainFormation(PlayerAI ai)
                : base(needsExitTime: false) => m_ai = ai;

            public override void OnLogic()
            {
                m_ai.MoveTowards(m_ai.m_formationTarget);
            }
        }

        private sealed class MoveToPositioningTarget : StateBase<PlayerAIState>
        {
            private readonly PlayerAI m_ai;

            internal MoveToPositioningTarget(PlayerAI ai)
                : base(needsExitTime: false) => m_ai = ai;

            public override void OnLogic()
            {
                if (m_ai.CurrentState == PlayerAIState.BlockShot)
                    m_ai.m_player.ChangePlayerMode(PlayerModeEnum.Interception);
                else if (m_ai.CurrentState == PlayerAIState.ReachBallZone)
                    m_ai.m_player.ChangePlayerMode(PlayerModeEnum.Confrontation);

                m_ai.MoveTowards(m_ai.m_positioningInstruction.Value.Target);
            }
        }

        private sealed class WithBall : StateBase<PlayerAIState>
        {
            private readonly PlayerAI m_ai;

            internal WithBall(PlayerAI ai)
                : base(needsExitTime: false) => m_ai = ai;

            public override void OnLogic()
            {
                m_ai.CreateBotFieldPlayerAction();
            }
        }

        private sealed class Goalkeeper : StateBase<PlayerAIState>
        {
            private readonly PlayerAI m_ai;

            internal Goalkeeper(PlayerAI ai)
                : base(needsExitTime: false) => m_ai = ai;

            public override void OnLogic()
            {
                if (m_ai.m_player.HasBall)
                {
                    if (!m_ai.m_player.UsesAutomaticControl)
                        m_ai.CreateUserGoalkeeperPass();
                    else
                        m_ai.CreateBotGoalkeeperPass();
                    return;
                }

                m_ai.MoveTowards(
                    PitchUtils.GetGoalkeeperGuardPosition(m_ai.m_player.FormationPosition, m_ai.m_ballPosition)
                );
            }
        }

        private void MoveTowards(Vector2 destination)
        {
            if (m_player.AvailableMoveDistance <= 0f)
                return;

            // Leave a small margin to avoid floating-point rounding exceeding the AP budget.
            var target = Vector2.MoveTowards(
                m_player.Position,
                destination,
                Mathf.Max(0f, m_player.AvailableMoveDistance - 0.001f)
            );
            if ((target - m_player.Position).sqrMagnitude > 0.000001f)
                SetCommand(PlayerActionEnum.Move, target);
        }

        private void CreateUserGoalkeeperPass()
        {
            float minDistance = m_player.MinHighPassDistance;
            float maxDistance = m_player.AvailableHighPassDistance;

            this.LDebug(() => $"distance: {minDistance} - {maxDistance}");

            if (maxDistance < minDistance)
                return;

            float directionX = PitchUtils.ForwardDirectionX(m_player.Slot.TeamIdx);
            for (int attempt = 0; attempt < 32; attempt++)
            {
                float distance = Random.Range(minDistance, maxDistance);
                float angle = Random.Range(-Mathf.PI * 0.2f, Mathf.PI * 0.2f);
                var direction = new Vector2(Mathf.Cos(angle) * directionX, Mathf.Sin(angle));
                var target = m_player.Position + direction * distance;
                if (target.IsOnPitch())
                {
                    SetCommand(PlayerActionEnum.HighPass, target);
                    return;
                }
            }

            var fallback = m_player.Position + new Vector2(directionX * minDistance, 0f);
            if (fallback.IsOnPitch())
                SetCommand(PlayerActionEnum.HighPass, fallback);
        }

        private void CreateBotGoalkeeperPass()
        {
            if (TryGetBestPass(false, false, out var action, out var target))
                SetCommand(action, target);
        }

        private void CreateBotFieldPlayerAction()
        {
            var candidates = new List<(PlayerActionEnum action, Vector2 target)>(3);
            if (TryGetBestPass(true, true, out var passAction, out var passTarget))
                candidates.Add((passAction, passTarget));

            Vector2 opponentGoal = PitchUtils.OpponentGoalCenter(m_player.Slot.TeamIdx);
            if ((m_player.Position - opponentGoal).sqrMagnitude < k_MaxShotDistanceSqr)
            {
                var shotTarget = new Vector2(opponentGoal.x + (m_player.InHomeTeam ? -0.001f : 0.001f), opponentGoal.y);
                candidates.Add((PlayerActionEnum.Shot, shotTarget));
            }

            if (TryGetMoveWithBallTarget(out var moveTarget))
                candidates.Add((PlayerActionEnum.Move, moveTarget));

            if (candidates.Count == 0)
                return;

            foreach (var (a, t) in candidates)
            {
                this.LTrace(() => $"candidates: {string.Join(", ", candidates.Select((a, t) => "({a}, {t})"))}");
            }

            var (action, target) = candidates[Random.Range(0, candidates.Count)];
            this.LDebug(() => $"Choose: {action}, {target}");
            SetCommand(action, target);
        }

        private bool TryGetBestPass(
            bool restrictToForwardColumns,
            bool requirePositiveScore,
            out PlayerActionEnum bestAction,
            out Vector2 bestTarget
        )
        {
            bestAction = default;
            bestTarget = default;
            if (m_team == null || m_opponentTeam == null)
                return false;

            float bestScore = float.NegativeInfinity;
            bool found = false;
            for (int zone = 1; zone <= PitchUtils.ZonesCount; zone++)
            {
                if (m_player.IsGk && PitchUtils.IsOwnPenaltyAreaZone(m_player.Slot.TeamIdx, zone))
                    continue;

                if (
                    restrictToForwardColumns
                    && !PitchUtils.IsForwardOrSameZoneColumn(m_player.Slot.TeamIdx, m_player.Zone, zone)
                )
                    continue;

                var zoneBounds = PitchUtils.GetZoneBounds(zone);
                if (!TryGetPassAction(zoneBounds, out var action, out var target))
                    continue;

                int ownPlayers = CountPlayersInZone(m_team, zone);
                if (m_player.Zone == zone)
                    ownPlayers--;
                int opponents = CountPlayersInZone(m_opponentTeam, zone);
                float baseScore = ownPlayers - opponents * 1.5f;
                if (requirePositiveScore && baseScore <= 0f)
                    continue;

                float score = baseScore * Random.Range(0.9f, 1.1f);
                if (score > bestScore)
                {
                    found = true;
                    bestScore = score;
                    bestAction = action;
                    bestTarget = target;
                }
            }

            if (found)
                this.LDebug($"bestPass: {bestAction}, {bestTarget}");

            return found;
        }

        private bool TryGetMoveWithBallTarget(out Vector2 target)
        {
            target = default;
            float maximumDistance = Mathf.Max(0f, m_player.AvailableMoveDistance - 0.001f);
            if (!m_player.CanMoveWithBall || maximumDistance <= 0f)
                return false;

            float directionX = PitchUtils.ForwardDirectionX(m_player.Slot.TeamIdx);
            for (int attempt = 0; attempt < 16; attempt++)
            {
                float distance = Random.Range(maximumDistance * 0.25f, maximumDistance);
                float angle = Random.Range(-Mathf.PI * 0.3f, Mathf.PI * 0.3f);
                var direction = new Vector2(Mathf.Cos(angle) * directionX, Mathf.Sin(angle));
                var candidate = m_player.Position + direction * distance;
                if (candidate.IsAllowedMove() && !candidate.InAnyGkArea())
                {
                    target = candidate;
                    return true;
                }
            }

            return false;
        }

        private bool TryGetPassAction(Bounds2d zoneBounds, out PlayerActionEnum action, out Vector2 target)
        {
            // this.LTrace(
            //     $"zone: {zoneBounds}, low: {m_player.AvailableLowPassDistance}, high min: {m_player.MinHighPassDistance} high available: {m_player.AvailableHighPassDistance}"
            // );

            if (
                zoneBounds.TryGetPointInDistanceRange(
                    m_player.Position,
                    0f,
                    m_player.AvailableLowPassDistance,
                    out target
                )
            )
            {
                action = PlayerActionEnum.LowPass;
                return true;
            }

            if (
                zoneBounds.TryGetPointInDistanceRange(
                    m_player.Position,
                    m_player.MinHighPassDistance,
                    m_player.AvailableHighPassDistance,
                    out target
                )
            )
            {
                action = PlayerActionEnum.HighPass;
                return true;
            }

            action = default;
            target = default;
            return false;
        }

        private static int CountPlayersInZone(ServerTeamState team, int zone)
        {
            int result = 0;
            for (byte playerIdx = 1; playerIdx <= 11; playerIdx++)
            {
                if (team.GetPlayerAtIdx(playerIdx).Zone == zone)
                    result++;
            }
            return result;
        }

        private void SetCommand(PlayerActionEnum action, Vector2 target)
        {
            m_action = action;
            m_target = target;
        }
    }
}
