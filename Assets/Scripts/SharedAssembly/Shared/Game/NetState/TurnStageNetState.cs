// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Foobbuteo.Shared.Game.Model;
using Unity.Netcode;

namespace Imma.Foobbuteo.Shared.Game.NetState
{
    public enum TurnStageEnum
    {
        Pending = 0,
        PlayerMove,
        EndPlayerMove,
        Processing,
        ShowResults,
        Ended,
    }

    public struct TurnStageNetState : INetworkSerializable
    {
        public TurnStageNetState(
            FoobbuteoGameStatus gameStatus,
            int currentTurn,
            TurnStageEnum stage,
            float stageStartTime,
            float stageEndTime
        )
        {
            m_gameStatus = gameStatus;
            m_currentTurn = currentTurn;
            m_stage = stage;
            m_stageStartTime = stageStartTime;
            m_stageEndTime = stageEndTime;
        }

        private FoobbuteoGameStatus m_gameStatus;
        public FoobbuteoGameStatus GameStatus => m_gameStatus;

        private int m_currentTurn;
        public int CurrentTurn => m_currentTurn;

        private TurnStageEnum m_stage;
        public TurnStageEnum Stage => m_stage;

        private float m_stageStartTime;
        public float StageStartTime => m_stageStartTime;

        private float m_stageEndTime;
        public float StageEndTime => m_stageEndTime;

        public void NetworkSerialize<T>(BufferSerializer<T> s)
            where T : IReaderWriter
        {
            s.SerializeValue(ref m_gameStatus);
            s.SerializeValue(ref m_currentTurn);
            s.SerializeValue(ref m_stage);
            s.SerializeValue(ref m_stageStartTime);
            s.SerializeValue(ref m_stageEndTime);
        }

        public override string ToString()
        {
            return $"{(new { GameStatus, CurrentTurn, Stage, StageStartTime, StageEndTime })}";
        }
    }
}
