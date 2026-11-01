// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System.Collections.Generic;

namespace Imma.Foobbuteo.Client.UI
{
    public static class EnglishGameText
    {
        private static readonly IReadOnlyDictionary<string, string> Templates = new Dictionary<string, string>
        {
            [GameTextKeys.MatchReport.DribbleAgainstOpponents] = "{player} is trying to dribble {opponents} opponents.",
            [GameTextKeys.MatchReport.AdvanceUnopposed] = "{player} advances with the ball free of opposition.",
            [GameTextKeys.MatchReport.LowPassToZone] = "{player} makes a low pass to zone {zone}.",
            [GameTextKeys.MatchReport.HighPassToZone] = "{player} makes a high pass to zone {zone}.",
            [GameTextKeys.MatchReport.Shoot] = "{player} shoots...",
            [GameTextKeys.MatchReport.ScoreGoal] = "...GOAL!!! {player} scores!",
            [GameTextKeys.MatchReport.WinBallConfrontation] =
                "{player} won the ball. {teammates} teammates helped him against {opponents} opponents. (Zone: {zone})",
            [GameTextKeys.MatchReport.Offside] = "The referee blows the whistle. Offside!",
            [GameTextKeys.MatchReport.InterceptPass] = "{player} intercepts the pass.",
            [GameTextKeys.MatchReport.GetBall] = "{player} gets the ball.",
        };

        public static string Lookup(string key) => Templates[key];
    }
}
