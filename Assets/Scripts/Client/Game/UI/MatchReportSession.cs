// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System.Collections.Generic;

namespace Imma.Foobbuteo.Client.Game.UI
{
    public sealed class MatchReportSession
    {
        private readonly HashSet<(int turn, int cue)> m_seen = new();
        private readonly List<MatchReportEntry> m_entries = new();
        public IReadOnlyList<MatchReportEntry> Entries => m_entries;

        public bool Add(int cueIndex, MatchReportEntry entry)
        {
            if (!m_seen.Add((entry.Turn, cueIndex)))
                return false;
            m_entries.Add(entry);
            return true;
        }

        public void Clear()
        {
            m_seen.Clear();
            m_entries.Clear();
        }
    }
}
