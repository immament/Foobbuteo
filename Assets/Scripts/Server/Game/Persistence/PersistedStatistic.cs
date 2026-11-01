// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

namespace Imma.Foobbuteo.Server.Game.Persistence
{
    internal readonly struct PersistedStatistic
    {
        public PersistedStatistic(int playerId, byte statisticType)
        {
            PlayerId = playerId;
            StatisticType = statisticType;
        }

        public int PlayerId { get; }
        public byte StatisticType { get; }
    }
}
