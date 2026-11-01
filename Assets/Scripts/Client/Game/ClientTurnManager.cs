// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Common;
using Imma.Foobbuteo.Client.Game.Model;
using Imma.Foobbuteo.Client.Game.UI;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using Imma.Foobbuteo.Shared.Game.NetState;

namespace Imma.Foobbuteo.Client.Game
{
    internal class ClientTurnManager
    {
        public ClientTurnManager(GameHudPresenter hud, ClientGameState gameState, long baseServerTimeUnixSeconds)
        {
            m_hud = hud;
            m_gameState = gameState;
            m_baseServerTimeUnixSeconds = baseServerTimeUnixSeconds;
        }

        private readonly GameHudPresenter m_hud;

        private readonly ClientGameState m_gameState;

        private FoobbuteoGameStatus GameStatus => m_gameState.Status;

        // private FixedGameInfoNetState FixedGameInfo { get; set; }
        private readonly long m_baseServerTimeUnixSeconds;
        public int CurrentTurn => m_gameState.CurrentTurn;

        public void Init(float time)
        {
            if (GameStatus == FoobbuteoGameStatus.Ongoing && CurrentTurn > 0)
            {
                m_hud.StartTurn(CurrentTurn, m_gameState.NumberOfTurns);
            }
            else
            {
                // Stage = TurnStageEnum.Pending;
            }
            UpdateOnce(time);
        }

        public void UpdateOnce(float serverTime)
        {
            switch (GameStatus)
            {
                case FoobbuteoGameStatus.Pending:
                case FoobbuteoGameStatus.Ongoing:
                    Update(serverTime);
                    break;
                case FoobbuteoGameStatus.Ended:
                    m_hud.GameEnded();
                    break;
            }
        }

        public void Update(double serverTime)
        {
            switch (GameStatus)
            {
                case FoobbuteoGameStatus.Pending:
                    m_hud.Pending(m_gameState.StartDateTime, m_baseServerTimeUnixSeconds, serverTime);
                    break;
                case FoobbuteoGameStatus.Ongoing:

                    switch (m_gameState.TurnStage)
                    {
                        case TurnStageEnum.PlayerMove:
                            if (serverTime >= m_gameState.StageEndTime)
                            {
                                if (m_gameState.EndPlayerMove())
                                    m_hud.EndPlayerMove();
                                break;
                            }

                            m_hud.UpdateStageEndTimer(
                                Math.Max(0, (int)Math.Round(m_gameState.StageEndTime - serverTime))
                            );

                            break;
                        case TurnStageEnum.ShowResults:
                            m_hud.UpdateShowResultsStage(
                                Math.Max(0, (int)Math.Round(m_gameState.StageEndTime - serverTime))
                            );
                            break;
                    }
                    break;
            }
        }

        internal void TurnStarted(TurnStateChangedMessage msg)
        {
            this.LTrace("++");
            m_gameState.UpdateTurn(msg);
            m_hud.RefreshRoomPermissions();
            m_hud.StartTurn(CurrentTurn, m_gameState.NumberOfTurns);
        }

        internal void ProcessingStarted(TurnStateChangedMessage msg)
        {
            this.LTrace("++");
            m_gameState.UpdateTurn(msg);
            m_hud.RefreshRoomPermissions();
            if (m_gameState.TurnStage == TurnStageEnum.Processing)
                m_hud.Processing();
        }

        internal void TurnResultReady(TurnResultReadyMessage msg)
        {
            this.LTrace("++");
            m_gameState.TurnResultReady(msg);
            m_hud.RefreshCommandAvailability();
        }
    }
}
