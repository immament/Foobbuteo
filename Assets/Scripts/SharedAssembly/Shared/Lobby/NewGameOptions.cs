// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Foobbuteo.Shared.Game.Model;
using Unity.Netcode;

namespace Imma.Foobbuteo.Shared.Lobby
{
    public readonly struct NewGameOptions : INetworkSerializeByMemcpy
    {
        public static readonly NewGameOptions Default = new(NewGameOptionsRules.MinStartInMinutes);

        public readonly int StartInMinutes;
        public readonly int NumberOfTurns;
        public readonly ShirtColorEnum HomeTeamColor;
        public readonly ShirtColorEnum AwayTeamColor;
        public readonly int PlayerPosition;
        public readonly FoobbuteoGameType GameType;
        public readonly TeamKey Team;

        public NewGameOptions(
            int startInMinutes = NewGameOptionsRules.MinStartInMinutes,
            int numberOfTurns = 30,
            ShirtColorEnum homeTeamColor = ShirtColorEnum.Blue,
            ShirtColorEnum awayTeamColor = ShirtColorEnum.Red,
            int playerPosition = NewGameOptionsRules.MinPlayerPosition,
            FoobbuteoGameType gameType = FoobbuteoGameType.PvP,
            TeamKey team = default
        )
        {
            StartInMinutes = startInMinutes;
            NumberOfTurns = numberOfTurns;
            HomeTeamColor = homeTeamColor;
            AwayTeamColor = awayTeamColor;
            PlayerPosition = playerPosition;
            GameType = gameType;
            Team = team;
        }
    }
}
