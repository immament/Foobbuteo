// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Foobbuteo.Shared.Game.Model;
using Imma.Foobbuteo.Shared.Game.NetState;

namespace Imma.Foobbuteo.Shared.Game.Contract
{
    public readonly struct TurnStateChangedMessage
    {
        public FoobbuteoGameStatus GameStatus { get; }
        public TurnStageEnum TurnStage { get; }
        public int TurnNumber { get; }
        public float StageEndTime { get; }

        public TurnStateChangedMessage(
            FoobbuteoGameStatus gameStatus,
            TurnStageEnum turnStage,
            int turnNumber,
            float stageEndTime
        )
        {
            GameStatus = gameStatus;
            TurnStage = turnStage;
            TurnNumber = turnNumber;
            StageEndTime = stageEndTime;
        }
    }
}
