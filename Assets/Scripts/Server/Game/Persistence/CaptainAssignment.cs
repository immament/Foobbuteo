// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Foobbuteo.Shared.Game.Model;

namespace Imma.Foobbuteo.Server.Game.Persistence
{
    internal readonly struct CaptainAssignment
    {
        internal CaptainAssignment(TeamIndex teamIndex, byte playerIndex, int userId, int playerId, string name)
        {
            TeamIndex = teamIndex;
            PlayerIndex = playerIndex;
            UserId = userId;
            PlayerId = playerId;
            Name = name;
        }

        public TeamIndex TeamIndex { get; }
        public byte PlayerIndex { get; }
        public int UserId { get; }
        public int PlayerId { get; }
        public string Name { get; }
    }
}
