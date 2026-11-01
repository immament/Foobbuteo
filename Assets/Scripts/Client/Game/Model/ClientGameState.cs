// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using Imma.Foobbuteo.Shared.Game.NetState;

namespace Imma.Foobbuteo.Client.Game.Model
{
    internal class ClientGameState : FoobbuteoGameState<ClientPlayerState>
    {
        private const int k_playersPerTeam = 11;

        private NetFoobbuteoTeam m_homeTeamInfo;
        private NetFoobbuteoTeam m_awayTeamInfo;

        public ClientGameState(int gameId, FoobbuteoGameType gameType)
            : base(gameId, gameType) { }

        public static ClientGameState FromContract(BaseGameInfo info)
        {
            return new(info.GameId, info.FixedGameInfo.GameType)
            {
                Status = info.GameStatus,
                TurnStage = info.TurnStage.Stage,
                StageStartTime = info.TurnStage.StageStartTime,
                StageEndTime = info.TurnStage.StageEndTime,
                m_currentTurn = info.CurrentTurn,
                NumberOfTurns = info.NumberOfTurns,
                StartDateTime = info.FixedGameInfo.StartDateTime,
                m_homeTeamInfo = info.HomeTeam,
                m_awayTeamInfo = info.AwayTeam,
            };
        }

        internal void RescheduleStart(System.DateTimeOffset start) => StartDateTime = start;

        internal void LoadDetails(GameDetailsContract details)
        {
            if (details.GameId != GameId)
                throw new AppException($"Game details ${details.GameId} do not match game ${GameId}");

            var homeTeam = CreateTeamState(TeamIndex.Home, m_homeTeamInfo, details.HomeTeam);
            var awayTeam = CreateTeamState(TeamIndex.Away, m_awayTeamInfo, details.AwayTeam);

            m_homeTeam = homeTeam;
            m_awayTeam = awayTeam;
        }

        internal void UpdateTeamInfo(TeamIndex teamIdx, NetFoobbuteoTeam teamInfo)
        {
            if (teamIdx == TeamIndex.Home)
                m_homeTeamInfo = teamInfo;
            else
                m_awayTeamInfo = teamInfo;

            Team(teamIdx)?.UpdateInfo(teamInfo);
        }

        private FoobbuteoTeamState<ClientPlayerState> CreateTeamState(
            TeamIndex teamIdx,
            NetFoobbuteoTeam teamInfo,
            GameDetails_TeamContract details
        )
        {
            if (details.Id != teamInfo.Id)
                throw new AppException($"Team details @{details.Id} do not match team @{teamInfo.Id}");

            if (details.Players == null || details.Players.Length != k_playersPerTeam)
                throw new AppException($"Team {teamIdx} must contain {k_playersPerTeam} players");

            ClientPlayerState[] players = CreateStatePlayers(teamIdx, details);

            return new FoobbuteoTeamState<ClientPlayerState>(
                teamIdx,
                teamInfo.Id,
                teamInfo.Name,
                teamInfo.Color,
                players,
                details.Score,
                details.Team
            );
        }

        private ClientPlayerState[] CreateStatePlayers(TeamIndex teamIdx, GameDetails_TeamContract details)
        {
            var players = new ClientPlayerState[k_playersPerTeam];
            for (int i = 0; i < players.Length; i++)
            {
                players[i] = CreateStatePlayers(teamIdx, (byte)(i + 1), details.Players[i]);
            }

            return players;
        }

        private ClientPlayerState CreateStatePlayers(
            TeamIndex teamIdx,
            byte expectedPlayerIdx,
            GameDetails_PlayerContract player
        )
        {
            if (player.TeamIdx != teamIdx || player.PlayerIdx != expectedPlayerIdx)
            {
                throw new AppException(
                    $"Invalid player slot ({player.TeamIdx}/{player.PlayerIdx}); expected ({teamIdx}/{expectedPlayerIdx})"
                );
            }

            return ClientPlayerState.FromContract(GameId, player);
        }

        internal void UpdateTurn(TurnStateChangedMessage msg)
        {
            Status = msg.GameStatus;
            m_currentTurn = msg.TurnNumber;
            TurnStage = msg.TurnStage;
            StageEndTime = msg.StageEndTime;
        }

        internal void TurnResultReady(TurnResultReadyMessage msg)
        {
            m_currentTurn = msg.TurnNumber;
            TurnStage = TurnStageEnum.ShowResults;
            StageEndTime = msg.StageEndTime;
        }

        internal bool EndPlayerMove()
        {
            if (TurnStage != TurnStageEnum.PlayerMove)
                return false;

            TurnStage = TurnStageEnum.EndPlayerMove;
            return true;
        }

        public bool CanMove => TurnStage == TurnStageEnum.PlayerMove;
    }
}
