// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Foobbuteo.Shared.Game.Model;
using Unity.Netcode;
using UnityEngine;

namespace Imma.Foobbuteo.Shared
{
    [Serializable]
    public struct GameSlot : INetworkSerializable, IEquatable<GameSlot>
    {
        public GameSlot(int gameId, TeamIndex team, byte playerIdx)
            : this(gameId, new PlayerKey(team, playerIdx)) { }

        public GameSlot(int gameId, PlayerKey playerKey)
            : this()
        {
            if (gameId <= 0)
                throw new ArgumentOutOfRangeException(nameof(gameId));
            if (!playerKey.IsValid(out string reason))
                throw new ArgumentException(reason, nameof(playerKey));
            m_gameId = gameId;
            m_teamIdx = playerKey.TeamIdx;
            m_playerIdx = playerKey.PlayerIdx;
        }

        public static GameSlot ForPlayer(int gameId, PlayerKey playerKey) => new(gameId, playerKey);

        public static GameSlot ForManager(int gameId, TeamIndex teamIdx)
        {
            var slot = new GameSlot
            {
                m_gameId = gameId,
                m_teamIdx = teamIdx,
                m_playerIdx = k_managerIdx,
            };
            if (!slot.IsValid)
                throw new ArgumentException("A manager slot requires a positive game id and a valid team.");
            return slot;
        }

        public bool IsValid =>
            GameId > 0 && (TeamIdx == TeamIndex.Home || TeamIdx == TeamIndex.Away) && PlayerIdx <= 11;
        public bool IsManager => IsValid && PlayerIdx == k_managerIdx;
        public bool IsPlayer => IsValid && PlayerIdx > k_managerIdx;

        public bool TryGetPlayerKey(out PlayerKey playerKey)
        {
            playerKey = IsPlayer ? new PlayerKey(TeamIdx, PlayerIdx) : default;
            return IsPlayer;
        }

        private const byte k_managerIdx = 0;

        private const byte k_gkIdx = 1;

        [SerializeField]
        private int m_gameId;
        public int GameId => m_gameId;

        [SerializeField]
        private TeamIndex m_teamIdx;

        [SerializeField]
        private byte m_playerIdx;
        public PlayerKey PlayerKey =>
            TryGetPlayerKey(out var key)
                ? key
                : throw new InvalidOperationException("A manager slot has no pitch player key.");

        public TeamIndex TeamIdx => m_teamIdx;
        public byte PlayerIdx => m_playerIdx;

        public bool IsGk => PlayerIdx == k_gkIdx;

        public bool InHomeTeam => TeamIdx == TeamIndex.Home;

        public void NetworkSerialize<T>(BufferSerializer<T> s)
            where T : IReaderWriter
        {
            s.SerializeValue(ref m_gameId);
            s.SerializeValue(ref m_teamIdx);
            s.SerializeValue(ref m_playerIdx);
        }

        public override string ToString()
        {
            return $"(GameId: {m_gameId}, Team: {TeamIdx}, {(IsManager ? "Manager" : $"Player: {PlayerIdx}")})";
        }

        public override bool Equals(object obj)
        {
            return obj is GameSlot slot && Equals(slot);
        }

        public bool Equals(GameSlot other)
        {
            return m_gameId == other.m_gameId && TeamIdx == other.TeamIdx && PlayerIdx == other.PlayerIdx;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(m_gameId, TeamIdx, PlayerIdx);
        }

        public static bool operator ==(GameSlot left, GameSlot right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GameSlot left, GameSlot right)
        {
            return !(left == right);
        }
    }
}
