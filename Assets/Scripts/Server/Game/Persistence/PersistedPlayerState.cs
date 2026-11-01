// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using UnityEngine;

namespace Imma.Foobbuteo.Server.Game.Persistence
{
    internal readonly struct PersistedPlayerState
    {
        public PersistedPlayerState(
            TeamIndex teamIndex,
            byte playerIndex,
            int playerId,
            Vector2 position,
            bool hasBall,
            PlayerModeEnum playerMode
        )
        {
            TeamIndex = teamIndex;
            PlayerIndex = playerIndex;
            PlayerId = playerId;
            Position = position;
            HasBall = hasBall;
            PlayerMode = playerMode;
        }

        public TeamIndex TeamIndex { get; }
        public byte PlayerIndex { get; }
        public int PlayerId { get; }
        public Vector2 Position { get; }
        public bool HasBall { get; }
        public PlayerModeEnum PlayerMode { get; }
    }
}
