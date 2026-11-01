// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;

namespace Imma.Foobbuteo.Client.Game.UI.Players
{
    internal sealed class MvMLineupDraft
    {
        private readonly Dictionary<int, GameDetails_PlayerContract> m_players;
        private readonly List<int> m_order;
        internal int[] Original { get; private set; }
        internal int[] Starting => m_order.Take(MvMLineupRules.PlayersPerTeam).ToArray();
        internal IEnumerable<int> Reserves => m_order.Skip(MvMLineupRules.PlayersPerTeam);

        internal MvMLineupDraft(GameDetails_PlayerContract[] squad, int[] lineup)
        {
            m_players = squad.ToDictionary(player => player.PlayerId);
            string error = MvMLineupRules.Validate(lineup, m_players.Keys.ToArray());
            if (error != null)
                throw new ArgumentException(error);
            Original = lineup.ToArray();
            m_order = lineup.Concat(squad.Select(player => player.PlayerId).Except(lineup)).ToList();
        }

        internal bool Swap(int sourceId, int targetId)
        {
            int source = m_order.IndexOf(sourceId);
            int target = m_order.IndexOf(targetId);
            if (source < 0 || target < 0 || source == target)
                return false;
            (m_order[source], m_order[target]) = (m_order[target], m_order[source]);
            return true;
        }

        internal GameDetails_PlayerContract GetPlayer(int id, TeamIndex side)
        {
            int index = m_order.IndexOf(id);
            // Reserve rows display outfield ratings without an active-slot assignment.
            byte calculationSlot = index < MvMLineupRules.PlayersPerTeam ? (byte)(index + 1) : (byte)2;
            return MvMLineupRules.Assign(m_players[id], side, calculationSlot);
        }

        internal void Rebase(int[] authoritative) => Original = authoritative.ToArray();
    }
}
