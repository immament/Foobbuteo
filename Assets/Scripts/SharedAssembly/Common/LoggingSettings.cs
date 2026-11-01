// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Newtonsoft.Json.Linq;

namespace Imma.Common
{
    public sealed class LoggingSettings
    {
        public IReadOnlyDictionary<string, ImmLogger.LogLevel> Categories { get; }

        private LoggingSettings(Dictionary<string, ImmLogger.LogLevel> categories) =>
            Categories = new ReadOnlyDictionary<string, ImmLogger.LogLevel>(categories);

        public static LoggingSettings Parse(JToken token)
        {
            if (token == null)
                return null;
            if (token is not JObject data)
                throw new InvalidOperationException("Logging settings must be an object.");
            var categories = new Dictionary<string, ImmLogger.LogLevel>(StringComparer.OrdinalIgnoreCase);
            if (data["categories"] != null)
            {
                if (data["categories"] is not JObject entries)
                    throw new InvalidOperationException("Logging categories must be an object.");
                foreach (var property in entries.Properties())
                {
                    string name = property.Name.Trim();
                    if (name.Length == 0)
                        throw new InvalidOperationException("Logging category name must not be empty.");
                    if (
                        property.Value.Type != JTokenType.String
                        || !Enum.TryParse((string)property.Value, true, out ImmLogger.LogLevel level)
                        || !Enum.IsDefined(typeof(ImmLogger.LogLevel), level)
                        || !string.Equals(
                            Enum.GetName(typeof(ImmLogger.LogLevel), level),
                            (string)property.Value,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                        throw new InvalidOperationException($"Logging category '{name}' requires a named log level.");
                    if (!categories.TryAdd(name, level))
                        throw new InvalidOperationException($"Duplicate logging category '{name}'.");
                }
            }
            return new LoggingSettings(categories);
        }
    }
}
