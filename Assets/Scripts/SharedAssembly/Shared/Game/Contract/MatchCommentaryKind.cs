// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

namespace Imma.Foobbuteo.Shared.Game.Contract
{
    public enum MatchCommentaryKind : byte
    {
        DribbleAgainstOpponents,
        AdvanceUnopposed,
        LowPassToZone,
        HighPassToZone,
        Shoot,
        ScoreGoal,
        WinBallConfrontation,
        Offside,
        InterceptPass,
        GetBall,
    }
}
