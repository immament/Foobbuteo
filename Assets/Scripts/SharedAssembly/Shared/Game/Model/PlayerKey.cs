// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Common;
using Unity.Netcode;
using UnityEngine;

namespace Imma.Foobbuteo.Shared.Game.Model
{
    public enum TeamIndex : byte
    {
        Home = 0,
        Away = 1,
    }

    [Serializable]
    public struct PlayerKey : INetworkSerializable, IEquatable<PlayerKey>
    {
        public PlayerKey(TeamIndex teamIdx, byte playerIdx)
        {
            if (playerIdx < 1 || playerIdx > 11)
            {
                ImmLogger.LWarn<PlayerKey>($"Incorrect playerIdx: {playerIdx}. team: {teamIdx}");
                // throw new ArgumentOutOfRangeException($"Incorrect playerIdx: {playerIdx}.");
            }

            m_teamIdx = teamIdx;
            m_playerIdx = playerIdx;
        }

        public static readonly PlayerKey None = new PlayerKey();

        [SerializeField]
        private TeamIndex m_teamIdx;
        public TeamIndex TeamIdx => m_teamIdx;

        [SerializeField]
        private byte m_playerIdx;
        public byte PlayerIdx => m_playerIdx;

        public bool InHomeTeam => m_teamIdx == TeamIndex.Home;

        public bool IsEmpty => Equals(None);

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref m_teamIdx);
            serializer.SerializeValue(ref m_playerIdx);
        }

        public bool IsValid(out string reason)
        {
            if (TeamIdx is not TeamIndex.Home and not TeamIndex.Away)
            {
                reason = $"Invalid team index: {TeamIdx}";
                return false;
            }

            if (PlayerIdx is < 1 or > 11)
            {
                reason = $"Invalid player index: {PlayerIdx}. Expected 1-11";
                return false;
            }

            reason = null;
            return true;
        }

        public bool Equals(PlayerKey other) => this == other;

        public override bool Equals(object obj) => obj is PlayerKey other && Equals(other);

        public bool Equals(TeamIndex teamIdx, byte playerIdx)
        {
            return teamIdx == m_teamIdx && playerIdx == m_playerIdx;
        }

        public override int GetHashCode()
        {
            return 100 * (byte)m_teamIdx + m_playerIdx;
        }

        public override string ToString()
        {
            return $"({m_teamIdx}/{m_playerIdx})";
        }

        public static bool operator ==(PlayerKey x, PlayerKey y) =>
            x.TeamIdx == y.TeamIdx && x.PlayerIdx == y.PlayerIdx;

        public static bool operator !=(PlayerKey x, PlayerKey y) => !(x == y);
    }
}
// Floor(100*(1.08^level))-(80-(level-1)*10)
// Level/ exp
// 1 28
// 2 46
// 3 65
// 4 86
// 5 106
// 6 128
// 7 151
// 8 175
// 9 199
// 10 225
// 11 253
// 12 281
// 13 311
// 14 343
// 15 377
// 16 412
// 17 450
// 18 489
// 19 531
// 20 576
// 21 623
// 22 673
// 23 727
// 24 784
// 25 844
// 26 909
// 27 978
// 28 1052
// 29 1131
// 30 1216
// 31 1306
// 32 1403
// 33 1507
// 34 1619
// 35 1738
// 36 1866
// 37 2004
// 38 2152
// 39 2311
// 40 2482
// 41 2666
// 42 2863
// 43 3076
// 44 3305
// 45 3552
// 46 3817
// 47 4103
// 48 4411
// 49 4742
// 50 ...
