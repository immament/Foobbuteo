// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Foobbuteo.Shared.Game.Model;

namespace Imma.Foobbuteo.Server.Game.Persistence
{
    internal readonly struct PersistedGameEvent
    {
        public PersistedGameEvent(byte eventType, int playerId, TeamIndex teamIndex)
        {
            EventType = eventType;
            PlayerId = playerId;
            TeamIndex = teamIndex;
        }

        public byte EventType { get; }
        public int PlayerId { get; }
        public TeamIndex TeamIndex { get; }
    }
}
