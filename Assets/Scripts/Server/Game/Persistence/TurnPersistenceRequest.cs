// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System.Collections.Generic;
using Imma.Foobbuteo.Server.Game.Model;
using Imma.Foobbuteo.Shared.Game.Model;
using UnityEngine;

namespace Imma.Foobbuteo.Server.Game.Persistence
{
    internal sealed class TurnPersistenceRequest
    {
        private const byte k_EventGoal = 1;
        private const byte k_EventOffside = 2;
        private const byte k_StatisticGoal = 1;
        private const byte k_StatisticGoalkeeperSave = 2;
        private const byte k_StatisticInterception = 3;
        private const byte k_StatisticShot = 4;
        private const byte k_StatisticConfrontation = 5;

        private TurnPersistenceRequest(
            int gameId,
            int turn,
            FoobbuteoGameStatus status,
            Vector2 ballPosition,
            byte homeScore,
            byte awayScore,
            IReadOnlyList<PersistedPlayerState> players,
            IReadOnlyList<PersistedStatistic> statistics,
            PersistedGameEvent? gameEvent
        )
        {
            GameId = gameId;
            Turn = turn;
            Status = status;
            BallPosition = ballPosition;
            HomeScore = homeScore;
            AwayScore = awayScore;
            Players = players;
            Statistics = statistics;
            GameEvent = gameEvent;
        }

        public int GameId { get; }
        public int Turn { get; }
        public FoobbuteoGameStatus Status { get; }
        public Vector2 BallPosition { get; }
        public byte HomeScore { get; }
        public byte AwayScore { get; }
        public IReadOnlyList<PersistedPlayerState> Players { get; }
        public IReadOnlyList<PersistedStatistic> Statistics { get; }
        public PersistedGameEvent? GameEvent { get; }

        internal static TurnPersistenceRequest Create(ServerGameState state, TurnProcessor processor)
        {
            var players = new List<PersistedPlayerState>(22);
            AddTeam(state.HomeTeam);
            AddTeam(state.AwayTeam);

            var statistics = new List<PersistedStatistic>(4);
            AddStatistic(processor.GoalScorer, k_StatisticGoal);
            AddStatistic(
                processor.Interceptor,
                processor.Interceptor != null && processor.Interceptor.IsGk
                    ? k_StatisticGoalkeeperSave
                    : k_StatisticInterception
            );
            AddStatistic(processor.ProcessedShotPlayer, k_StatisticShot);
            AddStatistic(processor.ConfrontationWinner, k_StatisticConfrontation);

            PersistedGameEvent? gameEvent = null;
            if (processor.GoalScorer != null)
            {
                gameEvent = new PersistedGameEvent(
                    k_EventGoal,
                    processor.GoalScorer.PlayerId,
                    processor.GoalScorer.Slot.TeamIdx
                );
            }
            else if (processor.OffsidePlayer != null)
            {
                gameEvent = new PersistedGameEvent(
                    k_EventOffside,
                    processor.OffsidePlayer.PlayerId,
                    processor.OffsidePlayer.Slot.TeamIdx
                );
            }

            FoobbuteoGameStatus status =
                state.CurrentTurn >= state.NumberOfTurns ? FoobbuteoGameStatus.Ended : state.Status;
            return new TurnPersistenceRequest(
                state.GameId,
                state.CurrentTurn,
                status,
                state.BallPosition,
                state.HomeTeam.Score,
                state.AwayTeam.Score,
                players,
                statistics,
                gameEvent
            );

            void AddTeam(ServerTeamState team)
            {
                for (byte playerIndex = 1; playerIndex <= 11; playerIndex++)
                {
                    ServerPlayerState player = team.GetPlayerAtIdx(playerIndex);
                    players.Add(
                        new PersistedPlayerState(
                            player.Slot.TeamIdx,
                            player.Slot.PlayerIdx,
                            player.PlayerId,
                            player.Position,
                            player.HasBall,
                            player.PlayerMode
                        )
                    );
                }
            }

            void AddStatistic(ServerPlayerState player, byte statisticType)
            {
                if (player != null && player.IsUser)
                    statistics.Add(new PersistedStatistic(player.PlayerId, statisticType));
            }
        }
    }
}
