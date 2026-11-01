// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using Imma.Foobbuteo.Shared.dev;
using Imma.Foobbuteo.Shared.Game.Contract;

namespace Imma.Foobbuteo.Shared.Game.Model
{
    public static class MvMLineupRules
    {
        public const int PlayersPerTeam = 11;

        public static string Validate(int[] lineup, IReadOnlyCollection<int> squad)
        {
            if (lineup == null || lineup.Length != PlayersPerTeam)
                return "A lineup must contain exactly 11 players.";
            if (squad == null)
                return "The match squad is missing.";
            var available = new HashSet<int>(squad);
            var selected = new HashSet<int>();
            for (int index = 0; index < lineup.Length; index++)
            {
                int id = lineup[index];
                if (!selected.Add(id))
                    return $"Position {index + 1} repeats player {id}.";
                if (id <= 0 || !available.Contains(id))
                    return $"Player {id} at position {index + 1} is not in this match's squad.";
            }
            return null;
        }

        public static GameDetails_PlayerContract Assign(GameDetails_PlayerContract player, TeamIndex side, byte slot)
        {
            player.TeamIdx = side;
            player.PlayerIdx = slot;
            player.Position = Formation442.GetPosition(side, slot);
            player.UserId = 0;
            player.ClientId = null;
            player.IsCaptain = player.IsConnected = player.IsInactive = player.HasBall = false;
            player.LastActivityTurn = 0;
            player.ActionPoints = FoobbuteoPlayerState.k_maxActionPoints;
            player.PlayerMode = slot == 1 ? PlayerModeEnum.GkSave : PlayerModeEnum.Confrontation;
            return player;
        }
    }
}
