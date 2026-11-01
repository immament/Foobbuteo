// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

namespace Imma.Foobbuteo.Client.Game.UI.Players
{
    internal sealed class PlayerTableColumn
    {
        public PlayerTableColumn(string heading, string property, string modifier = "skill", string tooltip = null)
        {
            Heading = heading;
            Tooltip = tooltip ?? heading;
            Property = property;
            Modifier = modifier;
        }

        public string Heading { get; }
        public string Tooltip { get; }
        public string Property { get; }
        public string Modifier { get; }
    }
}
