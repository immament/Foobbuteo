// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Common;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game.Model;

namespace Imma.Foobbuteo.Server
{
    [Serializable]
    internal class ServerUser : GameUser
    {
        public ServerUser(
            ulong? clientId,
            int userId,
            int playerId,
            string name,
            UserRoles role,
            int inGameRoomId = 0,
            GameSlot? gameSlot = null,
            ManagedTeam[] teams = null
        )
        {
            m_clientId = clientId;
            m_userId = userId;
            m_playerId = playerId;
            m_name = name;
            m_role = role;
            m_inGameRoomId = inGameRoomId;
            m_gameSlot = gameSlot;
            RefreshTeams(teams);
        }

        internal void RefreshTeams(ManagedTeam[] teams) =>
            m_teams = teams == null ? Array.Empty<ManagedTeam>() : (ManagedTeam[])teams.Clone();

        public ServerUser(FoobbuteoPlayerState player, int gameId)
        {
            m_clientId = player.ClientId;
            m_userId = player.UserId;
            m_playerId = player.PlayerId;
            m_name = player.Name;
            m_inGameRoomId = gameId;
            m_gameSlot = player.Slot;
        }

        internal bool IsPlayGame(int gameId)
        {
            return GameSlot.HasValue && GameSlot.Value.GameId == gameId;
        }

        internal void SetDisconnected()
        {
            m_clientId = null;
        }

        internal void Reconnected(ulong newClientId)
        {
            m_clientId = newClientId;
        }

        public bool LeaveRoom(int gameId)
        {
            if (m_inGameRoomId == gameId)
            {
                m_inGameRoomId = 0;
                return true;
            }

            if (m_inGameRoomId > 0)
                this.LWarn($"Can not leave room ${gameId}. User @{m_userId} is in other room ${m_inGameRoomId}");
            else
                this.LWarn($"Can not leave room ${gameId}. User @{m_userId} is not in any room");
            return false;
        }

        internal void StopPlayGame(int gameId)
        {
            if (!m_gameSlot.HasValue)
            {
                this.LError($"Can not stop play in ${gameId}. User @{m_userId} not play in any game.");
                return;
            }

            if (m_gameSlot.Value.GameId == gameId)
            {
                m_gameSlot = null;
                return;
            }
            this.LError($"Can not stop play ${gameId}. User @{m_userId} play in other game ${m_inGameRoomId}.");
        }

        internal void CopyFrom(ServerUser src)
        {
            m_userId = src.UserId;
            m_playerId = src.PlayerId;
            m_name = src.Name;
            // m_role = src.Role; //
            m_inGameRoomId = src.InGameRoomId;
            m_gameSlot = src.GameSlot;
        }
    }
}
