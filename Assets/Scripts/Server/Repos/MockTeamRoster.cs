// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;

namespace Imma.Foobbuteo.Server.Repos
{
    public sealed class MockTeamRoster
    {
        public TeamKey Team { get; set; }
        public GameDetails_PlayerContract[] Reserves { get; set; } = Array.Empty<GameDetails_PlayerContract>();
        public GameDetails_PlayerContract[] Players { get; set; } = Array.Empty<GameDetails_PlayerContract>();
    }
}
