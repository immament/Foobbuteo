// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Common;
using Imma.Foobbuteo.Shared.Game.Model;
using Unity.Netcode;
using UnityEngine;

namespace Imma.Foobbuteo.Shared
{
    public enum UserRoles
    {
        Player = 0,
        Admin = 1,
    }

    [Serializable]
    public class GameUser : INetworkSerializable
    {
        protected ulong? m_clientId;
        public ulong? ClientId => m_clientId;

        [SerializeField]
        protected int m_userId;
        public int UserId => m_userId;

        [SerializeField]
        protected int m_playerId;
        public int PlayerId => m_playerId;

        [SerializeField]
        protected string m_name;
        public string Name => m_name;

        [SerializeField]
        protected UserRoles m_role;
        public UserRoles Role => m_role;
        public bool IsAdmin => m_role == UserRoles.Admin;

        // Is In room
        [SerializeField]
        protected int m_inGameRoomId;
        public int InGameRoomId => m_inGameRoomId;

        // Play In room
        [SerializeField]
        protected NullableStruct<GameSlot> m_gameSlot;
        public NullableStruct<GameSlot> GameSlot => m_gameSlot;

        protected ManagedTeam[] m_teams = Array.Empty<ManagedTeam>();
        public System.Collections.Generic.IReadOnlyList<ManagedTeam> Teams => m_teams;

        public bool TryGetManagedTeam(TeamKey key, out ManagedTeam team)
        {
            foreach (var candidate in m_teams)
            {
                if (candidate.Key == key && key.IsSupported)
                {
                    team = candidate;
                    return true;
                }
            }
            team = default;
            return false;
        }

        #region ++ Methods ++

        public void EnterRoom(int gameId)
        {
            m_inGameRoomId = gameId;
        }

        public void RestoreGameSlot(GameSlot gameSlot)
        {
            m_gameSlot = gameSlot;
        }

        internal void AssignPlayer(FoobbuteoPlayerState player)
        {
            m_gameSlot = player.Slot;
        }
        #endregion -- Methods --

        #region ++ Events ++

        #endregion -- Events --


        public override string ToString()
        {
            return $"(ClientId: ^{m_clientId}, UserId: @{m_userId}, PlayerId: {m_playerId}, Role: {m_role}, Name: '{m_name}', Game: #{m_inGameRoomId}, player: {m_gameSlot})";
        }

        #region ++ Serialization ++++++++

        public void NetworkSerialize<T>(BufferSerializer<T> s)
            where T : IReaderWriter
        {
            bool hasClientId = m_clientId.HasValue;

            s.SerializeValue(ref hasClientId);
            if (hasClientId)
            {
                ulong clientId = s.IsWriter ? m_clientId.Value : default;
                s.SerializeValue(ref clientId);
                m_clientId = clientId;
            }

            s.SerializeValue(ref m_userId);
            s.SerializeValue(ref m_playerId);
            s.SerializeValue(ref m_name);
            s.SerializeValue(ref m_role);
            s.SerializeValue(ref m_inGameRoomId);
            SerialzeGameSlot(s);
            s.SerializeValue(ref m_teams);
            m_teams ??= Array.Empty<ManagedTeam>();
        }

        private void SerialzeGameSlot<T>(BufferSerializer<T> s)
            where T : IReaderWriter
        {
            bool hasGameSlot = m_gameSlot.HasValue;
            s.SerializeValue(ref hasGameSlot);
            if (hasGameSlot)
            {
                GameSlot tmpSlot = m_gameSlot.GetValueOrDefault();
                tmpSlot.NetworkSerialize(s);
                if (s.IsReader)
                {
                    m_gameSlot = tmpSlot;
                }
            }
            else if (s.IsReader)
            {
                m_gameSlot = null;
            }
        }
        #endregion -- Serialization --
    }
}
