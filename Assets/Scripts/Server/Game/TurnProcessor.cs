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
    internal class TurnProcessor
    {
        private const float k_MoveBallBeforePlayerDuration = 0.5f;
        private readonly ServerGameState m_gameState;
        private readonly List<PlayerCommand> m_commands;
        private readonly List<MatchEvent> m_events = new();
        private readonly List<MatchCommentaryCue> m_commentary = new();
        private readonly Dictionary<PlayerKey, float> m_playerStartX = new();
        private TurnResultTypeEnum m_turnResultType;
        private ServerPlayerState m_playerMovedWithBall;
        private PlayerCommand? m_ballCmd;
        private ServerPlayerState m_ballCmdPlayer;
        private ServerPlayerState m_offsidePlayer;
        private ServerPlayerState m_goalScorer;
        private ServerPlayerState m_interceptor;
        private ServerPlayerState m_processedShotPlayer;
        private ServerPlayerState m_confrontationWinner;

        internal ServerPlayerState GoalScorer => m_goalScorer;
        internal ServerPlayerState Interceptor => m_interceptor;
        internal ServerPlayerState ProcessedShotPlayer => m_processedShotPlayer;
        internal ServerPlayerState ConfrontationWinner => m_confrontationWinner;
        internal ServerPlayerState OffsidePlayer => m_offsidePlayer;

        public TurnProcessor(ServerGameState gameState, List<PlayerCommand> commands)
        {
            m_gameState = gameState;
            m_commands = commands;
        }

        private void ResetState()
        {
            m_turnResultType = TurnResultTypeEnum.None;
            m_playerMovedWithBall = null;
            m_ballCmd = null;
            m_ballCmdPlayer = null;
            m_offsidePlayer = null;
            m_goalScorer = null;
            m_interceptor = null;
            m_processedShotPlayer = null;
            m_confrontationWinner = null;
            m_events.Clear();
            m_commentary.Clear();
            m_playerStartX.Clear();
        }

        internal TurnResult ProcessTurn()
        {
            this.LInfo($"{m_gameState.InfoTag} Conmands: {m_commands.Count}");
            ResetState();
            CapturePlayerStartPositions();
            new AutomaticTurnPlanner(m_gameState, m_commands).AddAutomaticCommands();
            m_ballCmd = ProcessPlayerMoveCommands();

            if (m_ballCmd.HasValue)
                ProcessBallCmd(m_ballCmd.Value);

            if (m_turnResultType == TurnResultTypeEnum.None)
            {
                // Check confrontations (if not intercept)
                CheckConfrontation();
            }

            CheckOffside();
            FinalizePossessionCommentary();
            // Check if ball outside pitch => Goal kick

            // CreateFixPositionEvents(m_gameState.HomeTeam);
            // CreateFixPositionEvents(m_gameState.AwayTeam);

            float duration = m_events.Sum(a => a.Duration);
            var withBall = m_gameState.GetPlayerWithBall();
            var result = new TurnResult(
                m_turnResultType,
                m_gameState.HomeTeam.Score,
                m_gameState.AwayTeam.Score,
                m_events.ToArray(),
                withBall?.Slot.PlayerKey ?? PlayerKey.None,
                m_offsidePlayer?.Slot.PlayerKey ?? PlayerKey.None,
                duration,
                m_commentary.ToArray()
            );
            m_gameState.RefreshState();
            return result;
        }

        private void FinalizePossessionCommentary()
        {
            if (m_events.Count == 0 || m_turnResultType == TurnResultTypeEnum.Goal)
                return;

            var winner = m_gameState.GetPlayerWithBall();
            if (m_turnResultType == TurnResultTypeEnum.Offside)
            {
                m_commentary.Add(
                    new MatchCommentaryCue(MatchCommentaryKind.Offside, PlayerKey.None, m_events.Count - 1, true)
                );
                return;
            }
            if (winner == null)
                return;

            bool pass =
                m_ballCmd.HasValue && m_ballCmd.Value.Action is PlayerActionEnum.LowPass or PlayerActionEnum.HighPass;
            if (m_interceptor != null)
            {
                var kind =
                    m_ballCmd.Value.Action == PlayerActionEnum.LowPass
                        ? MatchCommentaryKind.InterceptPass
                        : MatchCommentaryKind.GetBall;
                m_commentary.Add(new MatchCommentaryCue(kind, winner.Slot.PlayerKey, m_events.Count - 1, true));
            }
            else if (pass)
            {
                int zone = PitchUtils.GetZone(m_gameState.BallPosition);
                int teammates = CountPlayers(
                    winner.Slot.TeamIdx,
                    p => p != winner && PitchUtils.GetZone(p.Position) == zone
                );
                int opponents = CountPlayers(
                    m_gameState.GetOpponentTeam(winner.Slot.TeamIdx).TeamIdx,
                    p => PitchUtils.GetZone(p.Position) == zone
                );
                m_commentary.Add(
                    new MatchCommentaryCue(
                        MatchCommentaryKind.WinBallConfrontation,
                        winner.Slot.PlayerKey,
                        m_events.Count - 1,
                        true,
                        zone,
                        Math.Min(2, teammates),
                        Math.Min(3, opponents)
                    )
                );
            }
            else if (m_confrontationWinner != null || m_turnResultType == TurnResultTypeEnum.GoalKick)
            {
                m_commentary.Add(
                    new MatchCommentaryCue(MatchCommentaryKind.GetBall, winner.Slot.PlayerKey, m_events.Count - 1, true)
                );
            }
        }

        private int CountPlayers(TeamIndex team, Func<ServerPlayerState, bool> predicate)
        {
            int count = 0;
            for (byte index = 1; index <= 11; index++)
                if (predicate(m_gameState.Team(team).GetPlayerAtIdx(index)))
                    count++;
            return count;
        }

        private void CapturePlayerStartPositions()
        {
            for (byte playerIdx = 1; playerIdx <= 11; playerIdx++)
            {
                Capture(m_gameState.HomeTeam.GetPlayerAtIdx(playerIdx));
                Capture(m_gameState.AwayTeam.GetPlayerAtIdx(playerIdx));
            }

            void Capture(ServerPlayerState player) => m_playerStartX[player.Slot.PlayerKey] = player.Position.x;
        }

        private void CheckOffside()
        {
            if (
                m_turnResultType != TurnResultTypeEnum.None
                || !m_ballCmd.HasValue
                || m_ballCmdPlayer == null
                || m_ballCmd.Value.Action is not (PlayerActionEnum.LowPass or PlayerActionEnum.HighPass)
            )
                return;

            var playerWithBall = m_gameState.GetPlayerWithBall();
            if (playerWithBall == null || playerWithBall.Slot.TeamIdx != m_ballCmdPlayer.Slot.TeamIdx)
                return;

            var opponentTeam = m_gameState.GetOpponentTeam(playerWithBall.Slot.TeamIdx);
            if (
                !OffsideRule.IsOffsidePosition(
                    playerWithBall.Slot.TeamIdx,
                    m_playerStartX[playerWithBall.Slot.PlayerKey],
                    opponentTeam,
                    opponent => m_playerStartX[opponent.Slot.PlayerKey]
                )
            )
                return;

            float opponentGoalX = PitchUtils.OpponentGoalCenter(playerWithBall.Slot.TeamIdx).x;

            var freeKickPlayer = Enumerable
                .Range(2, 10)
                .Select(playerIdx => opponentTeam.GetPlayerAtIdx((byte)playerIdx))
                .OrderBy(player => Mathf.Abs(opponentGoalX - player.Position.x))
                .ThenBy(player => player.Slot.PlayerIdx)
                .First();

            m_offsidePlayer = playerWithBall;
            playerWithBall.HasBall = false;
            m_gameState.SetPlayerWithBall(freeKickPlayer);
            MoveBallBeforePlayer(freeKickPlayer);
            m_turnResultType = TurnResultTypeEnum.Offside;
        }

        private void CheckConfrontation()
        {
            var ballZone = PitchUtils.GetZone(m_gameState.BallPosition);
            var homePlayers = m_gameState.HomeTeam.GetPlayersInZone(ballZone, m_gameState.BallPosition);
            var awayPlayers = m_gameState.AwayTeam.GetPlayersInZone(ballZone, m_gameState.BallPosition);

            this.LTrace(() =>
                $"ballZone: {ballZone}, homePlayers: {homePlayers.Count}, awayPlayers: {awayPlayers.Count}, m_playerMovedWithBall: {m_playerMovedWithBall?.Slot}"
            );

            InlcudePlayerMovedWithBall(m_playerMovedWithBall);

            if (homePlayers.Count == 0)
            {
                if (awayPlayers.Count == 0)
                    return;

                // won away team
                SetConfrontationWinner(awayPlayers);
                return;
            }
            if (awayPlayers.Count == 0)
            {
                SetConfrontationWinner(homePlayers);
                return;
            }

            float homeStrength = ConfrontationStrength(homePlayers, TeamIndex.Home);
            float awayStrength = ConfrontationStrength(homePlayers, TeamIndex.Away);

            if (IsWeakerTeamWin(homeStrength, awayStrength))
            {
                SetConfrontationWinner(homeStrength < awayStrength ? homePlayers : awayPlayers);
            }
            else
            {
                SetConfrontationWinner(homeStrength >= awayStrength ? homePlayers : awayPlayers);
            }

            return;

            // -----

            float ConfrontationStrength(List<ServerPlayerState> players, TeamIndex teamIdx)
            {
                players.Sort((a, b) => b.ConfrontationSkill - a.ConfrontationSkill);

                int maxPlayersCount = m_playerMovedWithBall?.Slot.TeamIdx == teamIdx ? 1 : 3;

                float result = 0f;

                for (int i = 0; i < Math.Min(maxPlayersCount, players.Count); i++)
                {
                    result += Mathf.Pow(players[i].ConfrontationSkill, StrengthPowerRatio(i));
                }
                // 	if action_with_ball = M_LONG_PASS and team_with_ball = 0 and is_cruzamento = 0 then forca_casa = forca_casa - (forca_casa * 0.3)
                if (
                    m_ballCmd.HasValue
                    && m_ballCmdPlayer != null
                    && m_ballCmd.Value.TeamIdx == teamIdx
                    && m_ballCmd.Value.Action == PlayerActionEnum.HighPass
                    && !PitchUtils.InCrossZone(teamIdx, m_ballCmdPlayer.Zone)
                )
                {
                    result *= 0.7f;
                }
                return result;
            }

            bool IsWeakerTeamWin(float homeStrength, float awayStrength)
            {
                float strengthDiff = Mathf.Abs(homeStrength - awayStrength) * 5f;
                int randTreshold = strengthDiff switch
                {
                    <= 1f => 50,
                    < 5f => 65,
                    < 10f => 70,
                    < 15f => 80,
                    < 20f => 85,
                    < 30f => 87,
                    < 35f => 89,
                    < 40f => 91,
                    < 45f => 92,
                    < 50f => 93,
                    < 75f => 95,
                    < 100f => 97,
                    _ => 99,
                };
                return UnityEngine.Random.Range(0, 101) > randTreshold;
            }

            float StrengthPowerRatio(int idx) =>
                idx switch
                {
                    0 => 1f,
                    1 => 0.77f,
                    2 => 0.54f,
                    _ => throw new ArgumentOutOfRangeException($"Incorrect power ratio {idx}"),
                };

            // include player who last move with ball if is near ball
            void InlcudePlayerMovedWithBall(ServerPlayerState player)
            {
                if (player == null)
                    return;
                player.RefreshState(m_gameState.BallPosition);
                if (player.BallDistance < 40f)
                {
                    if (player.InHomeTeam)
                    {
                        if (!homePlayers.Contains(player))
                            homePlayers.Add(player);
                    }
                    else if (!awayPlayers.Contains(player))
                        awayPlayers.Add(player);
                }
            }

            void SetConfrontationWinner(List<ServerPlayerState> players)
            {
                this.LTrace("++");
                players.Sort((a, b) => a.BallDistance - b.BallDistance);

                var winner = players[0];

                var withBall = m_gameState.GetPlayerWithBall();

                if (withBall == winner)
                    return;

                if (withBall != null)
                    withBall.HasBall = false;

                this.LTrace(() => $"{winner.Slot}");

                MoveBall(MatchEventType.BallMove, winner.BallSlotPosition);
                m_gameState.SetPlayerWithBall(winner);
                m_confrontationWinner = winner;
            }
        }

        private void ProcessBallCmd(PlayerCommand ballCmd)
        {
            var playerWithBall = m_gameState.GetPlayerWithBall();

            if (playerWithBall == null)
            {
                this.LWarn(
                    $"{m_gameState.InfoTag} Got ball cmd but no player with ball in state ! command {ballCmd.Action} from: {ballCmd.CreatorTag()}/id: {ballCmd.PlayerId}"
                );
                return;
            }
            if (!playerWithBall.Slot.PlayerKey.Equals(ballCmd.TeamIdx, ballCmd.PlayerIdx))
                this.LError(
                    $"{m_gameState.InfoTag} Player with ball {playerWithBall.Tag()} and cmd creator are different {ballCmd.CreatorTag()}"
                );

            // DEV_FixBallPosition(playerWithBall);
            this.LDebug(() => $"{m_gameState.InfoTag} Has ball cmd: {ballCmd.Action}");

            m_ballCmdPlayer = playerWithBall;
            if (ballCmd.Action == PlayerActionEnum.Shot)
                m_processedShotPlayer = playerWithBall;
            switch (ballCmd.Action)
            {
                case PlayerActionEnum.LowPass:
                    ProcessLowPass(playerWithBall, ballCmd);
                    break;
                case PlayerActionEnum.Shot:
                    ProcessShot(playerWithBall, ballCmd);
                    break;
                case PlayerActionEnum.HighPass:
                    ProcessHighPass(playerWithBall, ballCmd);
                    break;
            }
        }

        private void ProcessLowPass(ServerPlayerState passer, PlayerCommand ballCmd)
        {
            this.LDebug(() => $"{m_gameState.InfoTag} Pass - from: {passer.Position}, to: {ballCmd.TargetPosition}");
            ServerTeamState opponent = m_gameState.GetOpponentTeam(ballCmd.TeamIdx);

            var (interceptor, interceptPoint) = opponent.CheckPassIntercept(passer.Position, ballCmd.TargetPosition);

            if (interceptor == null)
            {
                PlayerKickBall(MatchEventType.LowPass, passer, ballCmd.TargetPosition);
                return;
            }
            this.LDebug(() => $"{m_gameState.InfoTag} Pass interpreted: {interceptor.Slot.PlayerKey}");
            PlayerKickBall(MatchEventType.LowPass, passer, interceptPoint);
            m_gameState.SetPlayerWithBall(interceptor);
            MoveBallBeforePlayer(interceptor);
            m_interceptor = interceptor;
            m_turnResultType = TurnResultTypeEnum.Intercept;
        }

        private void ProcessHighPass(ServerPlayerState passer, PlayerCommand ballCmd)
        {
            PlayerKickBall(MatchEventType.HighPass, passer, ballCmd.TargetPosition);
        }

        #region ++ Shot +++++

        private void ProcessShot(ServerPlayerState shooter, PlayerCommand ballCmd)
        {
            // check shot GK intercept
            // If shot not intercept and is on target then GOAL

            var (begin, end) = RandomShot(shooter, ballCmd.TargetPosition);
            var opponentTeam = m_gameState.GetOpponentTeam(shooter.Slot.TeamIdx);
            var (interceptor, interceptPoint) = opponentTeam.CheckShotIntercept(begin, end);
            if (interceptor != null) // Shot intecepted
            {
                ShotIntercepted(shooter, interceptor, interceptPoint);
                return;
            }

            // not intercepted - goal or missed

            // animationEnd little farer then pith
            Vector2 animationEnd = end + (end - begin).normalized * 10f;

            PlayerKickBall(MatchEventType.Shot, shooter, animationEnd);

            if (PitchUtils.IsGoal(end))
            {
                Goal(shooter, end);
                return;
            }
            this.LDebug(() => $"{m_gameState.InfoTag} Missed Shot {shooter.Tag()}, {end}");
            GoalKick(opponentTeam, animationEnd);
        }

        private void GoalKick(ServerTeamState team, Vector2 shotEnd)
        {
            // Missed shot => (Gaol kick)

            m_turnResultType = TurnResultTypeEnum.GoalKick;

            // Gk goes for ball
            var gk = team.GetGk();
            MovePlayer(gk, shotEnd);

            m_gameState.SetPlayerWithBall(gk);
            // Gk Come back to pitch with ball
            var goalKickPosition = PitchUtils.GoalKickPosition(shotEnd);
            MovePlayerWithBall(gk, goalKickPosition);
            MoveBallBeforePlayer(gk);
        }

        private static readonly bool DEV_k_randomizeShot = true;

        private (Vector2 begin, Vector2 end) RandomShot(ServerPlayerState playerWithBall, Vector2 target)
        {
            var begin = playerWithBall.Position;
            var end = target.ClampToPitch();
            if (!DEV_k_randomizeShot)
            {
                this.LWarn("{m_gameState.InfoTag} Randomize Shot Off");
                return (begin, end);
            }
            float distance = (end - begin).magnitude;
            bool inPenaltyArea = begin.InPenaltyArea(playerWithBall.Slot.TeamIdx);
            float shootingSkill = playerWithBall.GetFinishingSkill(distance, inPenaltyArea);

            float distanceFormula = (distance / (610f / 2f)) * 400f;

            var maxVariationY = distanceFormula / shootingSkill;
            float rand = UnityEngine.Random.Range(-1f, 1f);
            var variationY = rand * maxVariationY;
            end.y = Mathf.Clamp(end.y + variationY, 5f, 415f);

            this.LInfo(
                $"{m_gameState.InfoTag} Shot {(new { inPenaltyArea, shootingSkill, distance, distanceFormula, rand, maxVariationY, variationY, end.y, targetY = target.ClampToPitch().y })}"
            );

            return (begin, end);
        }

        private void Goal(ServerPlayerState shooter, Vector2 end)
        {
            // Goal - Start from center
            this.LDebug(() => $"{m_gameState.InfoTag} Goal!!! {shooter.Tag()}, {end}");

            m_turnResultType = TurnResultTypeEnum.Goal;
            m_goalScorer = shooter;
            m_commentary.Add(
                new MatchCommentaryCue(MatchCommentaryKind.ScoreGoal, shooter.Slot.PlayerKey, m_events.Count - 1, true)
            );

            m_gameState.Team(shooter.Slot.TeamIdx).Goal();

            // TODO: change match result

            ResetPlayersPositionEvents(m_gameState.HomeTeam);
            ResetPlayersPositionEvents(m_gameState.AwayTeam);

            const float quickBallMove = 0.2f;
            MoveBall(MatchEventType.BallMove, PitchUtils.PitchBounds.Center, quickBallMove);

            var opponentTeam = m_gameState.GetOpponentTeam(shooter.Slot.TeamIdx);
            var startingPlayer = opponentTeam.GetPlayerAtIdx((byte)UnityEngine.Random.Range(9, 11));

            MovePlayer(
                startingPlayer,
                new(
                    PitchUtils.PitchBounds.Center.x - startingPlayer.GetLocalBallSlotPosition().x,
                    PitchUtils.PitchBounds.Center.y
                )
            );
            m_gameState.SetPlayerWithBall(startingPlayer);
            MoveBallBeforePlayer(startingPlayer);
        }

        private void ShotIntercepted(ServerPlayerState shooter, ServerPlayerState interceptor, Vector2 interceptPoint)
        {
            this.LDebug(() => $"Intercepted shot {interceptor.Tag()}, {interceptPoint}");
            PlayerKickBall(MatchEventType.Shot, shooter, interceptPoint);
            m_gameState.SetPlayerWithBall(interceptor);
            MoveBallBeforePlayer(interceptor);
            m_interceptor = interceptor;
            m_turnResultType = TurnResultTypeEnum.Intercept;
        }

        #endregion -- Shot --

        #region ++ move events +++++

        private void MovePlayer(ServerPlayerState player, Vector2 to)
        {
            this.LDebug(() =>
                $"{m_gameState.InfoTag} [{MatchEventType.PlayerMove}] {player.Slot}, {player.Position} => {to}"
            );

            if (player.HasBall)
                this.LError($"{m_gameState.InfoTag} Player {player.Tag()} has ball but move without!");

            m_events.Add(new PlayerMoveEvent(MatchEventType.PlayerMove, m_gameState.CurrentTurn, player, to));
            player.Server_PlayerMove(to);
        }

        private void MovePlayerWithBall(ServerPlayerState player, Vector2 to)
        {
            this.LDebug(() =>
                $"{m_gameState.InfoTag} [{MatchEventType.PlayerMove}] {player.Slot}, {player.Position} => {to}"
            );
            Dev_AssertPlayerBallAction(player);

            int opponents = CountPlayers(
                m_gameState.GetOpponentTeam(player.Slot.TeamIdx).TeamIdx,
                p => (p.Position - to).sqrMagnitude <= 40f * 40f
            );
            m_commentary.Add(
                new MatchCommentaryCue(
                    opponents > 0 ? MatchCommentaryKind.DribbleAgainstOpponents : MatchCommentaryKind.AdvanceUnopposed,
                    player.Slot.PlayerKey,
                    m_events.Count,
                    opponents: Math.Min(3, opponents)
                )
            );
            m_events.Add(new PlayerMoveEvent(MatchEventType.PlayerMove, m_gameState.CurrentTurn, player, to, true));
            player.Server_PlayerMove(to);
            m_gameState.MoveBall(to);
            MoveBallBeforePlayer(player);
        }

        private void PlayerKickBall(MatchEventType type, ServerPlayerState playerWithBall, Vector2 to)
        {
            this.LDebug(() =>
                $"{m_gameState.InfoTag} [{type}] {playerWithBall.Slot}, {m_gameState.BallPosition} => {to}"
            );
            Dev_AssertPlayerBallAction(playerWithBall);
            var kind = type switch
            {
                MatchEventType.LowPass => MatchCommentaryKind.LowPassToZone,
                MatchEventType.HighPass => MatchCommentaryKind.HighPassToZone,
                MatchEventType.Shot => MatchCommentaryKind.Shoot,
                _ => throw new ArgumentOutOfRangeException(nameof(type)),
            };
            m_commentary.Add(
                new MatchCommentaryCue(
                    kind,
                    playerWithBall.Slot.PlayerKey,
                    m_events.Count,
                    zone: PitchUtils.GetZone(to)
                )
            );
            m_events.Add(new BallMoveEvent(type, m_gameState.CurrentTurn, m_gameState.BallPosition, to));
            m_gameState.MoveBall(to);
            playerWithBall.HasBall = false;
        }

        private void MoveBall(MatchEventType type, Vector2 to, float? duration = null)
        {
            this.LDebug(() => $"{m_gameState.InfoTag} [{type}] {m_gameState.BallPosition} => {to} [{duration}s]");

            m_events.Add(new BallMoveEvent(type, m_gameState.CurrentTurn, m_gameState.BallPosition, to, duration));
            m_gameState.MoveBall(to);
        }

        private void MoveBallBeforePlayer(ServerPlayerState player)
        {
            if (!player.HasBall)
                this.LError($"{m_gameState.InfoTag} Player {player.Tag()} has not ball");

            var endPosition = player.BallSlotPosition;

            this.LDebug(() =>
                $"{m_gameState.InfoTag} [{MatchEventType.BallMove}] {player.Slot}, {m_gameState.BallPosition} => {endPosition}"
            );

            m_events.Add(
                new BallMoveEvent(
                    MatchEventType.BallMove,
                    m_gameState.CurrentTurn,
                    m_gameState.BallPosition,
                    endPosition,
                    k_MoveBallBeforePlayerDuration
                )
            );
            m_gameState.MoveBall(endPosition);
        }

        private void Dev_AssertPlayerBallAction(ServerPlayerState playerWithBall)
        {
            this.LAssert(
                playerWithBall.HasBall,
                () => $"{m_gameState.InfoTag} Player {playerWithBall.Slot} has not ball!"
            );
            var dev_ballStartVsCurrentStateDistance = (m_gameState.BallPosition - playerWithBall.Position).sqrMagnitude;

            this.LAssert(
                dev_ballStartVsCurrentStateDistance <= 10f * 10f,
                () =>
                    $"{m_gameState.InfoTag} Incorect Ball position {dev_ballStartVsCurrentStateDistance} > {16f * 16f}"
            );
        }

        #endregion -- move events --


        private void ResetPlayersPositionEvents(ServerTeamState team)
        {
            for (byte i = 1; i <= 11; i++)
            {
                var player = team.GetPlayerAtIdx(i);
                var newPosiition = player.FormationPosition;
                m_events.Add(
                    new PlayerMoveEvent(
                        MatchEventType.FixPosition,
                        m_gameState.CurrentTurn,
                        player,
                        newPosiition,
                        duration: 0.2f
                    )
                );
                player.Server_PlayerMove(newPosiition);
            }
        }

        // Create event for players which not move in current turn
        // private void CreateFixPositionEvents(ServerTeamState team)
        // {
        //     for (byte i = 1; i <= 11; i++)
        //     {
        //         var ev = m_events.FirstOrDefault(e =>
        //             (e is PlayerMoveEvent playerMove && playerMove.TeamIdx == team.TeamIdx && playerMove.PlayerIdx == i)
        //         );
        //         if (ev == null)
        //         {
        //             var player = team.GetPlayerAtIdx(i);
        //             m_events.Add(
        //                 new PlayerMoveEvent(
        //                     MatchEventType.FixPosition,
        //                     m_gameState.CurrentTurn,
        //                     player,
        //                     player.Position,
        //                     player.HasBall
        //                 )
        //             );
        //         }
        //     }
        // }

#if UNITY_EDITOR
        internal string EditorQueuePlayerMove(TeamIndex teamIdx, byte playerIdx, Vector2 target)
        {
            if (playerIdx is < 1 or > 11)
                return $"Player index must be between 1 and 11: {playerIdx}";
            if (!target.IsOnPitch())
                return $"Target position is outside the pitch: {target}";

            var player = m_gameState.Team(teamIdx).GetPlayerAtIdx(playerIdx);
            m_commands.RemoveAll(command => command.TeamIdx == teamIdx && command.PlayerIdx == playerIdx);
            m_commands.Add(
                new PlayerCommand(
                    m_gameState.CurrentTurn,
                    teamIdx,
                    playerIdx,
                    player.PlayerId,
                    PlayerActionEnum.Move,
                    target
                )
            );
            return null;
        }
#endif

        private PlayerCommand? ProcessPlayerMoveCommands()
        {
            this.LDebug(() => $"{m_gameState.InfoTag} Commands count: {m_commands.Count}");

            PlayerCommand? lastBallCmd = null;

            for (int i = 0; i < m_commands.Count; i++)
            {
                PlayerCommand cmd = m_commands[i];

                switch (cmd.Action)
                {
                    case PlayerActionEnum.Move:
                        var player = m_gameState.GetPlayer(cmd.TeamIdx, cmd.PlayerIdx);

                        if (player == null)
                        {
                            this.LError(
                                $"{m_gameState.InfoTag} ProcessPlayerMoveCommandsPlayer #{cmd.PlayerId} not found in team with idx: {cmd.TeamIdx}"
                            );
                            continue;
                        }
                        if (player.HasBall)
                        {
                            m_playerMovedWithBall = player;
                            MovePlayerWithBall(player, cmd.TargetPosition);
                        }
                        else
                            MovePlayer(player, cmd.TargetPosition);

                        // if (player.HasBall)
                        // {
                        //     Vector2 moveWithBallOffset =
                        //         Vector2.Normalize(cmd.TargetPosition - player.Position) * 8f;
                        //     m_gameState.MoveBall(cmd.TargetPosition + moveWithBallOffset);
                        //     // events.Add(new BallMoveEvent());
                        // }
                        break;
                    case PlayerActionEnum.LowPass:
                    case PlayerActionEnum.HighPass:
                    case PlayerActionEnum.Shot:
                        if (lastBallCmd.HasValue)
                        {
                            this.LDebug(() =>
                                $"{m_gameState.InfoTag} Last ball cmd replaced {lastBallCmd.Value.Action} => {cmd.Action}"
                            );
                        }
                        lastBallCmd = cmd;

                        break;
                    default:
                        this.LWarn($"{m_gameState.InfoTag} Unknown player command: {cmd.Action}");
                        break;
                }
            }

            for (byte playerIdx = 1; playerIdx <= 11; playerIdx++)
            {
                var homePlayer = m_gameState.HomeTeam.GetPlayerAtIdx(playerIdx);
                var awayPlayer = m_gameState.AwayTeam.GetPlayerAtIdx(playerIdx);
                homePlayer.Server_RegisterTurnMovedWithBall(homePlayer == m_playerMovedWithBall);
                awayPlayer.Server_RegisterTurnMovedWithBall(awayPlayer == m_playerMovedWithBall);
            }
            return lastBallCmd;
        }
    }
}
