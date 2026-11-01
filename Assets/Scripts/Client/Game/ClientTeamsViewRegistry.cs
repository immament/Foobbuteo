// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System.Collections.Generic;
using System.Linq;
using Imma.Foobbuteo.Shared.Game.Model;

namespace Imma.Foobbuteo.Client.Game
{
    internal class ClientTeamsViewRegistry
    {
        private readonly ClientPlayerViewRegistry[] m_teams = new ClientPlayerViewRegistry[2] { new(), new() };

        internal void RegisterPlayer(TeamIndex teamIdx, PlayerMB player)
        {
            m_teams[(byte)teamIdx].Add(player);
        }

        internal void Clear()
        {
            foreach (var team in m_teams)
                team.Clear();
        }

        internal PlayerMB GetPlayer(TeamIndex teamIdx, byte playerIdx)
        {
            return m_teams[(byte)teamIdx].Get(playerIdx);
        }

        internal PlayerMB GetPlayer(PlayerKey identify)
        {
            return m_teams[(byte)identify.TeamIdx].Get(identify.PlayerIdx);
        }

        internal void TurnStarted()
        {
            foreach (var team in m_teams)
            {
                team.TurnStarted();
            }
        }

        internal void ApplyViewportScale(float pixelsPerUnit, float density)
        {
            foreach (var team in m_teams)
            {
                team.ApplyViewportScale(pixelsPerUnit, density);
            }
        }

        internal void CleanPlayerWithBall()
        {
            foreach (var team in m_teams)
            {
                team.CleanPlayerWithBall();
            }
        }

        internal bool TryGetPlayerByUserId(int userId, out PlayerMB player)
        {
            foreach (var team in m_teams)
            {
                if (team.TryGetByUserId(userId, out player))
                    return true;
            }
            player = null;
            return false;
        }

        private class ClientPlayerViewRegistry
        {
            private readonly Dictionary<int, PlayerMB> m_players = new();

            internal void Clear() => m_players.Clear();

            internal void Add(PlayerMB player)
            {
                m_players.Add(player.PlayerIdx, player);
            }

            internal PlayerMB Get(byte playerIdx)
            {
                return m_players[playerIdx];
            }

            internal bool TryGetByUserId(int userId, out PlayerMB player)
            {
                player = m_players.Values.FirstOrDefault(p => p.PlayerState.UserId == userId);
                return player != null;
            }

            internal void TurnStarted()
            {
                foreach (var p in m_players.Values)
                {
                    p.StartPlayerTurn();
                }
            }

            internal void ApplyViewportScale(float pixelsPerUnit, float density)
            {
                foreach (var player in m_players.Values)
                {
                    player.ApplyViewportScale(pixelsPerUnit, density);
                }
            }

            internal void CleanPlayerWithBall()
            {
                foreach (var p in m_players.Values)
                {
                    p.SetHasBall(false);
                }
            }
        }
    }
}
