// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using Imma.Foobbuteo.Shared.Game.Model;

namespace Imma.Foobbuteo.Shared.Lobby
{
    public static class NewGameOptionsRules
    {
#if UNITY_EDITOR
        public const int MinStartInMinutes = 0;
#else
        public const int MinStartInMinutes = 2;
#endif
        public const int MaxStartInMinutes = 15;
        public const int MinPlayerPosition = 1;
        public const int MaxPlayerPosition = 11;
        public static IReadOnlyList<int> TurnCounts { get; } = Array.AsReadOnly(new[] { 30, 40, 50, 60 });

        public static bool TryValidate(NewGameOptions options, out string error)
        {
            if (options.StartInMinutes < MinStartInMinutes || options.StartInMinutes > MaxStartInMinutes)
                error = $"Start time must be between {MinStartInMinutes} and {MaxStartInMinutes} minutes.";
            else if (!IsAllowedTurnCount(options.NumberOfTurns))
                error = $"Choose {string.Join(", ", TurnCounts)} turns.";
            else if (
                options.GameType == FoobbuteoGameType.PvP
                && (options.PlayerPosition < MinPlayerPosition || options.PlayerPosition > MaxPlayerPosition)
            )
                error = "Choose a player position between 1 and 11.";
            else if (
                !Enum.IsDefined(typeof(ShirtColorEnum), options.HomeTeamColor)
                || !Enum.IsDefined(typeof(ShirtColorEnum), options.AwayTeamColor)
            )
                error = "Choose an available shirt color.";
            else if (options.HomeTeamColor == options.AwayTeamColor)
                error = "Home and away shirt colors must be different.";
            else if (options.GameType != FoobbuteoGameType.PvP && options.GameType != FoobbuteoGameType.MvM)
                error = "Choose PvP or MvM.";
            else if (options.GameType == FoobbuteoGameType.MvM && !options.Team.IsSupported)
                error = "Choose an eligible Footstar team.";
            else
            {
                error = null;
                return true;
            }
            return false;
        }

        private static bool IsAllowedTurnCount(int count)
        {
            foreach (int allowed in TurnCounts)
                if (count == allowed)
                    return true;
            return false;
        }
    }
}
