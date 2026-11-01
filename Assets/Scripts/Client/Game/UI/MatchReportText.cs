// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using Imma.Foobbuteo.Client.UI;
using Imma.Foobbuteo.Shared.Game.Contract;

namespace Imma.Foobbuteo.Client.Game.UI
{
    public static class MatchReportText
    {
        public static string Format(GameTextFormatter formatter, MatchCommentaryCue cue, string playerName)
        {
            string key = cue.Kind switch
            {
                MatchCommentaryKind.DribbleAgainstOpponents => GameTextKeys.MatchReport.DribbleAgainstOpponents,
                MatchCommentaryKind.AdvanceUnopposed => GameTextKeys.MatchReport.AdvanceUnopposed,
                MatchCommentaryKind.LowPassToZone => GameTextKeys.MatchReport.LowPassToZone,
                MatchCommentaryKind.HighPassToZone => GameTextKeys.MatchReport.HighPassToZone,
                MatchCommentaryKind.Shoot => GameTextKeys.MatchReport.Shoot,
                MatchCommentaryKind.ScoreGoal => GameTextKeys.MatchReport.ScoreGoal,
                MatchCommentaryKind.WinBallConfrontation => GameTextKeys.MatchReport.WinBallConfrontation,
                MatchCommentaryKind.Offside => GameTextKeys.MatchReport.Offside,
                MatchCommentaryKind.InterceptPass => GameTextKeys.MatchReport.InterceptPass,
                MatchCommentaryKind.GetBall => GameTextKeys.MatchReport.GetBall,
                _ => throw new ArgumentOutOfRangeException(nameof(cue)),
            };
            return formatter.Format(
                key,
                new Dictionary<string, object>
                {
                    ["player"] = playerName,
                    ["zone"] = cue.Zone,
                    ["teammates"] = cue.Teammates,
                    ["opponents"] = cue.Opponents,
                }
            );
        }
    }
}
