// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Foobbuteo.Shared.Game.Model;
using Imma.Foobbuteo.Shared.Game.NetState;
using Unity.Netcode;

namespace Imma.Foobbuteo.Shared.Game.Contract
{
    public struct BaseGameInfo
    {
        public BaseGameInfo(
            FixedGameInfoNetState fixedGameInfo,
            TurnStageNetState turnStage,
            NetFoobbuteoTeam homeTeam,
            NetFoobbuteoTeam awayTeam
        )
        {
            FixedGameInfo = fixedGameInfo;
            TurnStage = turnStage;
            HomeTeam = homeTeam;
            AwayTeam = awayTeam;
        }

        public FixedGameInfoNetState FixedGameInfo { get; }
        public int GameId => FixedGameInfo.GameId;
        public int NumberOfTurns => FixedGameInfo.NumberOfTurns;

        public TurnStageNetState TurnStage { get; }
        public int CurrentTurn => TurnStage.CurrentTurn;
        public FoobbuteoGameStatus GameStatus => TurnStage.GameStatus;

        public NetFoobbuteoTeam HomeTeam { get; }
        public NetFoobbuteoTeam AwayTeam { get; }

        public void NetworkSerialize<T>(BufferSerializer<T> s)
            where T : IReaderWriter
        {
            FixedGameInfo.NetworkSerialize(s);
            TurnStage.NetworkSerialize(s);
            HomeTeam.NetworkSerialize(s);
            AwayTeam.NetworkSerialize(s);
        }
    }
}
