// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Imma.Foobbuteo.Client.UI
{
    public sealed class GameTextFormatter
    {
        private static readonly Regex Placeholder = new Regex(@"\{([a-zA-Z][a-zA-Z0-9_]*)\}");
        private readonly Func<string, string> m_lookup;

        public GameTextFormatter(Func<string, string> lookup = null)
        {
            m_lookup = lookup ?? EnglishGameText.Lookup;
        }

        public string Format(string key, IReadOnlyDictionary<string, object> values)
        {
            return Placeholder.Replace(
                m_lookup(key),
                match =>
                {
                    string name = match.Groups[1].Value;
                    if (values == null || !values.TryGetValue(name, out object value))
                        throw new ArgumentException($"Missing text parameter '{name}' for '{key}'.");
                    return Convert.ToString(value, CultureInfo.InvariantCulture);
                }
            );
        }
    }
}
