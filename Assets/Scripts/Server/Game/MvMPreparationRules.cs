// SPDX-License-Identifier: AGPL-3.0-only
using System;

namespace Imma.Foobbuteo.Server.Game
{
    internal static class MvMPreparationRules
    {
        internal const int MinimumMvMPreparationMinutes = 3;

        internal static DateTimeOffset GetStart(DateTimeOffset existingStart, DateTimeOffset joinedAt)
        {
            var minimumStart = joinedAt.AddMinutes(MinimumMvMPreparationMinutes);
            return existingStart >= minimumStart ? existingStart : minimumStart;
        }
    }
}
