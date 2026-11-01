// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game.Model;

public sealed class MockUserConfig
{
    public string Token { get; set; }
    public int UserId { get; set; }
    public int PlayerId { get; set; }
    public string Name { get; set; }
    public UserRoles Role { get; set; } = UserRoles.Player;
    public DateTime? ExpiresAtUtc { get; set; }
    public ManagedTeam[] Teams { get; set; } = Array.Empty<ManagedTeam>();
}
