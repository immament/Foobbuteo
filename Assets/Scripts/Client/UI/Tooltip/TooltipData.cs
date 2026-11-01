// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

namespace Imma.Foobbuteo.Client.UI.Tooltip
{
    public struct TooltipData
    {
        public string Title;
        public string Description;

        public TooltipData(string title, string description)
        {
            Title = title;
            Description = description;
        }
    }
}
