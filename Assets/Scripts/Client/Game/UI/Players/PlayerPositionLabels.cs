// SPDX-License-Identifier: AGPL-3.0-only
using System.Collections.Generic;
using System.Linq;

namespace Imma.Foobbuteo.Client.Game.UI.Players
{
    internal static class PlayerPositionLabels
    {
        private static readonly string[] s_positions =
        {
            "GK",
            "DR",
            "DC",
            "DC",
            "DL",
            "MC",
            "MR",
            "MC",
            "FC",
            "FC",
            "ML",
        };
        internal static readonly List<string> Choices = s_positions
            .Select((position, index) => $"{index + 1} - {position}")
            .ToList();

        internal static string Get(byte? slot) => slot is >= 1 and <= 11 ? s_positions[slot.Value - 1] : "";
    }
}
