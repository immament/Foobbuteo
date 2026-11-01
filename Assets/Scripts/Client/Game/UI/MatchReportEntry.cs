// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Foobbuteo.Shared.Game.Contract;
using UnityEngine;

namespace Imma.Foobbuteo.Client.Game.UI
{
    public sealed class MatchReportEntry
    {
        public int Turn { get; }
        public MatchCommentaryCue Cue { get; }
        public string Text { get; }
        public Sprite TeamMarker { get; }

        public MatchReportEntry(int turn, MatchCommentaryCue cue, string text, Sprite teamMarker)
        {
            Turn = turn;
            Cue = cue;
            Text = text;
            TeamMarker = teamMarker;
        }
    }
}
