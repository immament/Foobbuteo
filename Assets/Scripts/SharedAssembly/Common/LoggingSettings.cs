// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Imma.Common
{
    [JsonConverter(typeof(LoggingSettingsJsonConverter))]
    public sealed class LoggingSettings
    {
        public IReadOnlyDictionary<string, ImmLogger.LogLevel> Categories { get; }
        public ImmLogger.LogLevel? LogLevel { get; }
        public ImmLogger.LogLevel? DefaultSubLogLevel { get; }
        public string LogDirectory { get; }

        public LoggingSettings(
            IReadOnlyDictionary<string, ImmLogger.LogLevel> categories = null,
            ImmLogger.LogLevel? logLevel = null,
            ImmLogger.LogLevel? defaultSubLogLevel = null,
            string logDirectory = null
        )
        {
            var categoryLevels = new Dictionary<string, ImmLogger.LogLevel>(StringComparer.OrdinalIgnoreCase);
            if (categories != null)
                foreach (var pair in categories)
                    categoryLevels.Add(pair.Key, pair.Value);
            Categories = new ReadOnlyDictionary<string, ImmLogger.LogLevel>(categoryLevels);
            LogLevel = logLevel;
            DefaultSubLogLevel = defaultSubLogLevel;
            LogDirectory = logDirectory;
        }

        public static LoggingSettings Parse(JToken token)
        {
            if (token == null)
                return null;
            if (token is not JObject data)
                throw new InvalidOperationException("Logging settings must be an object.");
            var logLevel = ParseOptionalLevel(data, "logLevel");
            var defaultSubLogLevel = ParseOptionalLevel(data, "defaultSubLogLevel");
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
                    var level = ParseLevel(property.Value, $"category '{name}'");
                    if (!categories.TryAdd(name, level))
                        throw new InvalidOperationException($"Duplicate logging category '{name}'.");
                }
            }
            return new LoggingSettings(
                categories,
                logLevel,
                defaultSubLogLevel,
                (string)data.GetValue("logDirectory", StringComparison.OrdinalIgnoreCase)
            );
        }

        private static ImmLogger.LogLevel? ParseOptionalLevel(JObject data, string name)
        {
            var token = data.GetValue(name, StringComparison.OrdinalIgnoreCase);
            return token == null ? null : ParseLevel(token, name);
        }

        private static ImmLogger.LogLevel ParseLevel(JToken token, string name)
        {
            if (
                token.Type != JTokenType.String
                || !Enum.TryParse((string)token, true, out ImmLogger.LogLevel level)
                || !Enum.IsDefined(typeof(ImmLogger.LogLevel), level)
                || !string.Equals(
                    Enum.GetName(typeof(ImmLogger.LogLevel), level),
                    (string)token,
                    StringComparison.OrdinalIgnoreCase
                )
            )
                throw new InvalidOperationException($"Logging {name} requires a named log level.");
            return level;
        }
    }
}
