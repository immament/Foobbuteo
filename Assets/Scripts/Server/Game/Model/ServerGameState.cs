// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using Imma.Common;
using Imma.Foobbuteo.Common.Utils;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using Imma.Foobbuteo.Shared.Game.NetState;
using Imma.Foobbuteo.Shared.Lobby;
using UnityEngine;

namespace Imma.Foobbuteo.Server.Game.Model
{
    [Serializable]
    internal class ServerGameState : FoobbuteoGameState<ServerPlayerState>
    {
        public ServerGameState(int gameId, FoobbuteoGameType gameType)
            : base(gameId, gameType)
        {
            BallPosition = PitchUtils.PitchBounds.Center;
        }

        public new ServerTeamState HomeTeam => (ServerTeamState)base.HomeTeam;

        public new ServerTeamState AwayTeam => (ServerTeamState)base.AwayTeam;

        public new ServerTeamState GetOpponentTeam(TeamIndex idx) => idx == TeamIndex.Home ? AwayTeam : HomeTeam;

        public DateTime? EndDate { get; private set; }
        internal List<PlayerCommand> PendingCommands { get; } = new();

        [SerializeField]
        private Vector2 m_ballPosition;

        public Vector2 BallPosition
        {
            get => m_ballPosition;
            private set => m_ballPosition = value;
        }

        internal void RescheduleStart(DateTimeOffset start) => StartDateTime = start;

        internal void StartGame()
        {
            if (Status != FoobbuteoGameStatus.Pending)
            {
                throw new AppException($"Can't start game ${GameId} at status {Status}.");
            }
            Status = FoobbuteoGameStatus.Ongoing;
        }

        internal bool NextTurn()
        {
            if (CurrentTurn < NumberOfTurns)
            {
                m_currentTurn++;
                return true;
            }
            return false;
        }

        internal void MoveBall(Vector2 targetPosition)
        {
            BallPosition = targetPosition;
            this.LTrace(() => $"New ball position: {BallPosition}=>{targetPosition}");
        }

        internal void UpdatePositioningBounds()
        {
            var playerWithBall = GetPlayerWithBall();
            UpdateTeam(HomeTeam);
            UpdateTeam(AwayTeam);

            void UpdateTeam(ServerTeamState team)
            {
                var positioningMode =
                    playerWithBall == null ? TeamPositioningMode.Neutral
                    : playerWithBall.Slot.TeamIdx == team.TeamIdx ? TeamPositioningMode.Offensive
                    : TeamPositioningMode.Defensive;
                this.LTrace(() =>
                    $"team {team.TeamIdx} positioningMode: {positioningMode}, playerWithBall: {playerWithBall?.Slot}"
                );
                if (playerWithBall == null || playerWithBall.Slot.TeamIdx != team.TeamIdx)
                {
                    team.UpdatePositioningBounds(BallPosition, positioningMode);
                    return;
                }

                if (playerWithBall.IsGk)
                {
                    team.UpdatePositioningBounds(playerWithBall.Position, positioningMode, useStartingPositions: true);
                    return;
                }

                team.UpdatePositioningBounds(playerWithBall.Position, positioningMode, useStartingPositions: true);
                var assignments = team.GetDynamicFormationPositions();
                byte formationSlot = assignments[playerWithBall.Slot.PlayerIdx].FormationPlayerIdx;
                team.UpdatePositioningBounds(
                    playerWithBall.Position,
                    positioningMode,
                    anchoredFormationPlayerIdx: formationSlot
                );
            }
        }

        internal void PlayerMoveStage(float startTime, float stageTime)
        {
            TurnStage = TurnStageEnum.PlayerMove;
            StageStartTime = startTime;
            StageEndTime = startTime + stageTime;
            HomeTeam.StartTurn();
            AwayTeam.StartTurn();
        }

        internal void ProcessingStarted(float startTime)
        {
            TurnStage = TurnStageEnum.Processing;
            StageStartTime = startTime;
            StageEndTime = 0;
        }

        internal void EndGame()
        {
            Status = FoobbuteoGameStatus.Ended;
            EndDate = DateTime.UtcNow;
            TurnStage = TurnStageEnum.Ended;
        }

        internal void RefreshState()
        {
            HomeTeam.RefreshState(BallPosition);
            AwayTeam.RefreshState(BallPosition);
        }

        internal ServerPlayerState GetPlayerWithBall()
        {
            return HomeTeam.GetPlayerWithBall() ?? AwayTeam.GetPlayerWithBall();
        }

        internal void ClearPlayerWithBall()
        {
            if (GetPlayerWithBall() is { } playerWithBall)
                playerWithBall.HasBall = false;
        }

        internal void SetPlayerWithBall(ServerPlayerState player)
        {
            if (GetPlayerWithBall() is ServerPlayerState oldPlayerWithBall)
            {
                this.LError($"Other {oldPlayerWithBall.Tag()} player has ball!!");
                oldPlayerWithBall.HasBall = false;
            }
            player.HasBall = true;
        }

        internal void Load(
            FoobbuteoGameStatus status,
            int turn,
            int turns,
            DateTime startDate,
            ServerTeamState homeTeam,
            ServerTeamState awayTeam,
            Vector2 ballPosition,
            DateTime? endDate = null
        )
        {
            Status = status;
            m_currentTurn = turn;
            NumberOfTurns = turns;
            StartDateTime = startDate;

            m_homeTeam = homeTeam;
            m_awayTeam = awayTeam;
            BallPosition = ballPosition;
            EndDate = endDate;
            this.LTrace(
                new
                {
                    this.GameId,
                    status,
                    turn,
                    turns,
                    startDate,
                    this.MatchType,
                    homeTeam,
                    awayTeam,
                }
            );
        }

        internal void LoadPlayer(ServerPlayerState player)
        {
            Team(player.Slot.TeamIdx).LoadPlayer(player);
        }

        internal void SetManagedTeam(ServerTeamState team)
        {
            if (team.TeamIdx == TeamIndex.Home)
                m_homeTeam = team;
            else
                m_awayTeam = team;
        }

        internal static ServerTeamState CreateUnassignedTeam(int gameId, TeamIndex teamIdx, ShirtColorEnum color) =>
            new(teamIdx, 0, "Waiting for opponent", color, CreateStatePlayers(gameId, teamIdx));

        internal static ServerGameState CreateNewGame(
            int gameId,
            NewGameOptions options,
            GameUser user,
            DateTime utcNow
        )
        {
            var homeTeam = new ServerTeamState(
                TeamIndex.Home,
                id: user.UserId,
                name: user.Name,
                color: options.HomeTeamColor,
                players: CreateStatePlayers(gameId, TeamIndex.Home)
            );
            // Populate persisted identity without changing the live user before the game is loaded and joined.
            if (options.GameType == FoobbuteoGameType.PvP)
                homeTeam.GetPlayerAtIdx((byte)options.PlayerPosition).InitializeCreator(user);
            return new(gameId, options.GameType)
            {
                NumberOfTurns = options.NumberOfTurns,
                StartDateTime = utcNow.AddMinutes(options.StartInMinutes).StripSeconds(),
                m_homeTeam = homeTeam,
                m_awayTeam = new ServerTeamState(
                    TeamIndex.Away,
                    id: -2,
                    name: "Empty",
                    color: options.AwayTeamColor,
                    players: CreateStatePlayers(gameId, TeamIndex.Away)
                ),
            };
        }

        private static ServerPlayerState[] CreateStatePlayers(int gameId, TeamIndex teamIdx)
        {
            var result = new ServerPlayerState[11];
            for (byte i = 1; i <= 11; i++)
            {
                // TODO: rename method
                result[i - 1] = ServerPlayerState.CreateForTest(gameId, teamIdx, i);
            }
            return result;
        }

        #region ++ Mapping +++


        internal TurnStageNetState GetTurnStageNet()
        {
            return new(Status, CurrentTurn, TurnStage, StageStartTime, StageEndTime);
        }

        internal GameInfoContract ToGameInfoContract()
        {
            this.LDebug(() => $"StartDateTime: {StartDateTime}");
            return new GameInfoContract
            {
                GameId = GameId,
                MatchType = MatchType,
                HomeTeam = HomeTeam.TeamKey,
                AwayTeam = AwayTeam.TeamKey,
                Status = Status,
                Turn = CurrentTurn,
                NumberOfTurns = NumberOfTurns,
                HasResult = Status != FoobbuteoGameStatus.Pending,
                OwnerScore = HomeTeam.Score,
                OpponentScore = AwayTeam.Score,
                StartDateTime = StartDateTime,
                // CurrentDateTime = DateTimeOffset.UtcNow,
                // EndDateTime = EndDate,
                OwnerUid = HomeTeam.Id,
                OwnerName = HomeTeam.Name,
                OwnerColor = HomeTeam.Color,
                OpponentUid = AwayTeam.Id,
                OpponentName = AwayTeam.Name,
                OpponentColor = AwayTeam.Color,
            };
        }

        internal GameDetailsContract ToGameDetailsContract()
        {
            return new GameDetailsContract(GameId, BallPosition, HomeTeam.ToContract(), AwayTeam.ToContract());
        }

        internal FixedGameInfoNetState ToFixedGameInfoNetState(long baseServerTime)
        {
            return new FixedGameInfoNetState(GameId, MatchType, NumberOfTurns, StartDateTime, baseServerTime);
        }

        #endregion -- Mapping ---

        #region ++ TESTS ++++++
        internal static ServerGameState CreateForTest(
            int gameId,
            DateTime startDate,
            ServerTeamState homeTeam,
            ServerTeamState awayTeam,
            FoobbuteoGameStatus status = FoobbuteoGameStatus.Pending,
            int turns = 30,
            int turn = 0,
            FoobbuteoGameType matchType = FoobbuteoGameType.PvP,
            DateTime? endDate = null
        )
        {
            return new(gameId, matchType)
            {
                Status = status,
                m_currentTurn = turn,
                NumberOfTurns = turns,
                StartDateTime = startDate, // .StripSeconds(),
                m_homeTeam = homeTeam,
                m_awayTeam = awayTeam,
                EndDate = endDate,
            };
        }

        #endregion -- TESTS --
    }
}
