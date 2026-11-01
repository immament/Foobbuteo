// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Common;

namespace Imma.Foobbuteo.Client.UI
{
    public class Validation
    {
        public static T CheckQuery<T>(T element, string name)
        {
            if (element == null)
                ImmLogger.LError<Validation>($"Missing element named {name}");

            return element;
        }
    }
}
