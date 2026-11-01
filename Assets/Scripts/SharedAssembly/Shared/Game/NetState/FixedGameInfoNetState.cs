// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Foobbuteo.Shared.Game.Model;
using Unity.Netcode;

namespace Imma.Foobbuteo.Shared.Game.NetState
{
    public struct FixedGameInfoNetState : INetworkSerializable
    {
        public FixedGameInfoNetState(
            int gameId,
            FoobbuteoGameType gameType,
            int numberOfTurns,
            DateTimeOffset startDateTime,
            long baseServerTimeUnixSeconds
        )
        {
            m_gameId = gameId;
            m_gameType = gameType;
            m_numberOfTurns = numberOfTurns;
            m_startDateUnixSeconds = startDateTime.ToUnixTimeSeconds();
            m_baseServerTimeUnixSeconds = baseServerTimeUnixSeconds;
        }

        private int m_gameId;
        public int GameId => m_gameId;

        private FoobbuteoGameType m_gameType;
        public FoobbuteoGameType GameType => m_gameType;

        private int m_numberOfTurns;
        public int NumberOfTurns => m_numberOfTurns;

        private long m_startDateUnixSeconds;
        public DateTimeOffset StartDateTime => DateTimeOffset.FromUnixTimeSeconds(m_startDateUnixSeconds).ToLocalTime();

        private long m_baseServerTimeUnixSeconds;
        public long BaseServerTimeUnixSeconds => m_baseServerTimeUnixSeconds;

        public void NetworkSerialize<T>(BufferSerializer<T> s)
            where T : IReaderWriter
        {
            s.SerializeValue(ref m_gameId);
            s.SerializeValue(ref m_gameType);
            s.SerializeValue(ref m_numberOfTurns);
            s.SerializeValue(ref m_startDateUnixSeconds);
            s.SerializeValue(ref m_baseServerTimeUnixSeconds);
        }
    }
}
